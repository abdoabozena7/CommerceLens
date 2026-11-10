using System.Net;
using System.Net.Http.Json;
using System.Text;
using CommerceLens.Api.DTOs;
using CommerceLens.Api.Models;
using CommerceLens.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.ML;

namespace CommerceLens.Tests;

public class PredictionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public void Repeated_users_are_never_shared_between_training_and_testing()
    {
        var rows = Enumerable.Range(1, 20).SelectMany(user => Enumerable.Range(1, 3)
            .Select(row => new AdImpression { Id = user * 3 + row, OriginalUserId = user })).ToList();
        var (train, test) = ClickPredictionService.SplitByUser(rows);
        Assert.Equal(60, train.Count + test.Count);
        Assert.Empty(train.Select(x => x.OriginalUserId).Intersect(test.Select(x => x.OriginalUserId)));
        Assert.Equal(16, train.Select(x => x.OriginalUserId).Distinct().Count());
        Assert.Equal(4, test.Select(x => x.OriginalUserId).Distinct().Count());
        Assert.Equal(test.Select(x => x.Id), ClickPredictionService.SplitByUser(rows).Test.Select(x => x.Id));
    }

    [Fact]
    public async Task Evaluated_model_persists_predicts_missing_features_and_detects_new_imports()
    {
        var token = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        var status = await client.GetFromJsonAsync<ModelStatusDto>("/api/model/status", token);
        Assert.Equal("Evaluated", status!.Status);
        Assert.False(status.IsStale);
        var metrics = status.Metrics!;
        Assert.Equal(10_000, metrics.TrainRows + metrics.TestRows);
        Assert.Equal(3_200, metrics.TrainUsers);
        Assert.Equal(800, metrics.TestUsers);
        Assert.Equal(0, metrics.UserOverlap);
        Assert.Equal(0.65, metrics.HistoricalBaselineCtr);
        Assert.All(new[] { metrics.Accuracy, metrics.Precision, metrics.Recall, metrics.RocAuc, metrics.BaselineAccuracy },
            value => { Assert.True(double.IsFinite(value)); Assert.InRange(value, 0, 1); });
        Assert.Equal(["DeviceType", "AdPosition", "BrowsingHistory", "TimeOfDay"], metrics.Features);
        Assert.Equal(1_555, metrics.TruePositives);
        Assert.Equal(685, metrics.FalsePositives);
        Assert.Equal(0, metrics.TrueNegatives);
        Assert.Equal(0, metrics.FalseNegatives);
        Assert.Equal(metrics.Accuracy, metrics.BaselineAccuracy);
        Assert.InRange(metrics.Accuracy, 0.69419, 0.69420);
        Assert.InRange(metrics.RocAuc, 0.5269, 0.5271);
        var ml = new MLContext(seed: 42);
        var modelDirectory = factory.Services.GetRequiredService<IConfiguration>()["CommerceLens:ModelDirectory"]!;
        var artifact = ml.Model.Load(Path.Combine(modelDirectory, metrics.ArtifactFileName), out _);
        using var engine = ml.Model.CreatePredictionEngine<ClickPredictionService.ModelInput, ClickPredictionService.ModelOutput>(artifact);
        foreach (var input in new[] { new PredictionRequest("Mobile", "Top", "Shopping", "Morning"), new PredictionRequest(null, "", null, null) })
        {
            var response = await client.PostAsJsonAsync("/api/predictions", input, token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var prediction = await response.Content.ReadFromJsonAsync<PredictionDto>(token);
            Assert.True(double.IsFinite(prediction!.Probability));
            Assert.InRange(prediction.Probability, 0, 1);
            Assert.Equal(0.65, prediction.HistoricalBaselineCtr);
            Assert.False(prediction.ModelStale);
            Assert.Equal("ML.NET", prediction.Engine);
            Assert.Equal("ModelPrediction", prediction.SourceType);
            var independentlyScored = engine.Predict(new()
            {
                DeviceType = string.IsNullOrWhiteSpace(input.DeviceType) ? "Unknown" : input.DeviceType,
                AdPosition = string.IsNullOrWhiteSpace(input.AdPosition) ? "Unknown" : input.AdPosition,
                BrowsingHistory = string.IsNullOrWhiteSpace(input.BrowsingHistory) ? "Unknown" : input.BrowsingHistory,
                TimeOfDay = string.IsNullOrWhiteSpace(input.TimeOfDay) ? "Unknown" : input.TimeOfDay
            });
            Assert.Equal((double)independentlyScored.Probability, prediction.Probability);
        }
        await factory.Services.GetRequiredService<ClickPredictionService>().InitializeAsync(token);
        var reloaded = await client.GetFromJsonAsync<ModelStatusDto>("/api/model/status", token);
        Assert.Equal(metrics.ArtifactFileName, reloaded!.Metrics!.ArtifactFileName);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("id,full_name,age,gender,device_type,ad_position,browsing_history,time_of_day,click\n90001,Test User,,,Mobile,Top,Shopping,Morning,1", Encoding.UTF8), "file", "additional.csv");
        var upload = await client.PostAsync("/api/data/imports", form, token);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var stale = await client.GetFromJsonAsync<ModelStatusDto>("/api/model/status", token);
        Assert.True(stale!.IsStale);
        var trained = await client.PostAsync("/api/model/train", null, token);
        trained.EnsureSuccessStatusCode();
        var updated = await trained.Content.ReadFromJsonAsync<ModelMetrics>(token);
        Assert.Equal(10_001, updated!.TrainRows + updated.TestRows);
        Assert.NotEqual(metrics.DatasetFingerprint, updated.DatasetFingerprint);
        Assert.False((await client.GetFromJsonAsync<ModelStatusDto>("/api/model/status", token))!.IsStale);
    }
}
