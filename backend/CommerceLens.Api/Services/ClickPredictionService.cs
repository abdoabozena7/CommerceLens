using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CommerceLens.Api.Data;
using CommerceLens.Api.DTOs;
using CommerceLens.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace CommerceLens.Api.Services;

public class ClickPredictionService(IServiceScopeFactory scopes, IWebHostEnvironment environment,
    IConfiguration configuration, ILogger<ClickPredictionService> logger)
{
    private const string PipelineVersion = "mlnet-5.0.1-onehot-lbfgs-v2-evaluation";
    private static readonly string[] Features =
        [nameof(ModelInput.DeviceType), nameof(ModelInput.AdPosition), nameof(ModelInput.BrowsingHistory), nameof(ModelInput.TimeOfDay)];
    private readonly MLContext ml = new(seed: 42);
    private readonly SemaphoreSlim trainingLock = new(1, 1);
    private volatile ModelState? state;
    private string ModelDirectory => configuration["CommerceLens:ModelDirectory"]
        ?? Path.Combine(environment.ContentRootPath, "App_Data", "models");
    private sealed record ModelState(ITransformer Transformer, ModelMetrics Metrics);

    public async Task InitializeAsync(CancellationToken token = default)
    {
        Directory.CreateDirectory(ModelDirectory);
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var fingerprint = await DatasetFingerprintAsync(db, token);
        var saved = await db.ModelEvaluations.AsNoTracking().Where(x => x.DatasetFingerprint == fingerprint)
            .OrderByDescending(x => x.Id).FirstOrDefaultAsync(token);
        if (saved is not null)
        {
            var metrics = JsonSerializer.Deserialize<ModelMetrics>(saved.MetricsJson)
                ?? throw new InvalidDataException("Saved model evaluation metadata is invalid.");
            if (Path.GetFileName(metrics.ArtifactFileName) != metrics.ArtifactFileName)
                throw new InvalidDataException("Saved model artifact name is invalid.");
            var artifact = Path.Combine(ModelDirectory, metrics.ArtifactFileName);
            if (File.Exists(artifact))
            {
                state = new(ml.Model.Load(artifact, out _), metrics);
                logger.LogInformation("Loaded evaluated ML.NET model trained at {TrainedAt}", metrics.TrainedAt);
                return;
            }
            logger.LogWarning("Model artifact {File} is missing; a fresh evaluation will be created", metrics.ArtifactFileName);
        }
        await TrainAsync(token);
    }

    public async Task<ModelMetrics> TrainAsync(CancellationToken token = default)
    {
        if (!await trainingLock.WaitAsync(0, token)) throw new ApiException(409, "Model training is already running.");
        try
        {
            var watch = Stopwatch.StartNew();
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
            var fingerprint = await DatasetFingerprintAsync(db, token);
            var rows = await db.AdImpressions.AsNoTracking().OrderBy(x => x.Id).ToListAsync(token);
            var (trainRows, testRows) = SplitByUser(rows);
            if (!trainRows.Any(x => x.Click) || !trainRows.Any(x => !x.Click)
                || !testRows.Any(x => x.Click) || !testRows.Any(x => !x.Click))
                throw new ApiException(422, "Training and testing partitions must each contain both click labels.");
            var trainData = ml.Data.LoadFromEnumerable(trainRows.Select(ToInput));
            var testData = ml.Data.LoadFromEnumerable(testRows.Select(ToInput));
            var encoded = Features.Select(feature => new InputOutputColumnPair($"{feature}Encoded", feature)).ToArray();
            // Only these four encoded categories enter Features; IDs, names, demographics and Label never do.
            var pipeline = ml.Transforms.Categorical.OneHotEncoding(encoded)
                .Append(ml.Transforms.Concatenate("Features", encoded.Select(x => x.OutputColumnName).ToArray()))
                .Append(ml.BinaryClassification.Trainers.LbfgsLogisticRegression(
                    labelColumnName: nameof(ModelInput.Label), featureColumnName: "Features"));
            var transformer = pipeline.Fit(trainData);
            token.ThrowIfCancellationRequested();
            var scored = transformer.Transform(testData);
            var evaluation = ml.BinaryClassification.Evaluate(scored, labelColumnName: nameof(ModelInput.Label));
            var predictions = ml.Data.CreateEnumerable<ModelOutput>(scored, reuseRowObject: false).ToList();
            var trainUsers = trainRows.Select(x => x.OriginalUserId).ToHashSet();
            var testUsers = testRows.Select(x => x.OriginalUserId).ToHashSet();
            var majorityLabel = trainRows.Count(x => x.Click) >= trainRows.Count / 2.0;
            var artifactName = $"ad-click-{Guid.NewGuid():N}.zip";
            var metrics = new ModelMetrics(DateTime.UtcNow, "ML.NET L-BFGS logistic regression", fingerprint, artifactName,
                trainRows.Count, testRows.Count, trainUsers.Count, testUsers.Count, trainUsers.Intersect(testUsers).Count(),
                (double)rows.Count(x => x.Click) / rows.Count,
                (double)testRows.Count(x => x.Click == majorityLabel) / testRows.Count,
                evaluation.Accuracy, evaluation.PositivePrecision, evaluation.PositiveRecall, evaluation.AreaUnderRocCurve,
                evaluation.F1Score, evaluation.LogLoss, watch.Elapsed.TotalSeconds, Features,
                predictions.Count(x => x.Label && x.PredictedClick), predictions.Count(x => !x.Label && x.PredictedClick),
                predictions.Count(x => !x.Label && !x.PredictedClick), predictions.Count(x => x.Label && !x.PredictedClick));
            if (new[] { metrics.Accuracy, metrics.Precision, metrics.Recall, metrics.RocAuc, metrics.F1Score, metrics.LogLoss }
                .Any(value => !double.IsFinite(value)))
                throw new InvalidOperationException("Model evaluation produced non-finite metrics.");
            Directory.CreateDirectory(ModelDirectory);
            // An immutable artifact is written before publishing matching metrics and a new in-memory snapshot.
            ml.Model.Save(transformer, trainData.Schema, Path.Combine(ModelDirectory, artifactName));
            db.ModelEvaluations.Add(new ModelEvaluation
            {
                TrainedAt = metrics.TrainedAt, DatasetFingerprint = fingerprint, MetricsJson = JsonSerializer.Serialize(metrics)
            });
            await db.SaveChangesAsync(token);
            state = new(transformer, metrics);
            logger.LogInformation("Evaluated ML model: {TrainRows}/{TestRows} rows, {Overlap} overlapping users, accuracy {Accuracy:F4}, ROC-AUC {Auc:F4}, baseline accuracy {Baseline:F4}",
                metrics.TrainRows, metrics.TestRows, metrics.UserOverlap, metrics.Accuracy, metrics.RocAuc, metrics.BaselineAccuracy);
            return metrics;
        }
        finally { trainingLock.Release(); }
    }

    public async Task<ModelStatusDto> StatusAsync(CancellationToken token)
    {
        var current = state;
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var fingerprint = await DatasetFingerprintAsync(db, token);
        var count = await db.AdImpressions.CountAsync(token);
        var clicks = await db.AdImpressions.CountAsync(x => x.Click, token);
        return new(current is null ? "Not trained" : "Evaluated", current is not null && current.Metrics.DatasetFingerprint != fingerprint,
            current?.Metrics, count == 0 ? 0 : (double)clicks / count);
    }

    public async Task<PredictionDto> PredictAsync(PredictionRequest request, CancellationToken token)
    {
        var current = state ?? throw new ApiException(503, "No evaluated model is available. Train a model first.");
        // PredictionEngine is not thread-safe, so each request owns and disposes its own instance.
        using var engine = ml.Model.CreatePredictionEngine<ModelInput, ModelOutput>(current.Transformer);
        var predicted = engine.Predict(new ModelInput
        {
            DeviceType = Normalize(request.DeviceType), AdPosition = Normalize(request.AdPosition),
            BrowsingHistory = Normalize(request.BrowsingHistory), TimeOfDay = Normalize(request.TimeOfDay)
        });
        if (!float.IsFinite(predicted.Probability) || predicted.Probability is < 0 or > 1)
            throw new InvalidOperationException("The model produced an invalid probability.");
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var fingerprint = await DatasetFingerprintAsync(db, token);
        var total = await db.AdImpressions.CountAsync(token);
        var clicks = await db.AdImpressions.CountAsync(x => x.Click, token);
        return new(predicted.Probability, predicted.PredictedClick, total == 0 ? 0 : (double)clicks / total,
            current.Metrics.HistoricalBaselineCtr, current.Metrics.TrainedAt, current.Metrics.DatasetFingerprint != fingerprint);
    }

    public static (List<AdImpression> Train, List<AdImpression> Test) SplitByUser(List<AdImpression> rows)
    {
        var users = rows.Select(x => x.OriginalUserId).Distinct().OrderBy(x => x).ToArray();
        if (users.Length < 2) throw new ApiException(422, "At least two distinct users are required for a user-disjoint evaluation.");
        new Random(42).Shuffle(users);
        var testUsers = users.Take(Math.Clamp((int)Math.Round(users.Length * 0.2), 1, users.Length - 1)).ToHashSet();
        return (rows.Where(x => !testUsers.Contains(x.OriginalUserId)).ToList(),
            rows.Where(x => testUsers.Contains(x.OriginalUserId)).ToList());
    }

    public static async Task<string> DatasetFingerprintAsync(CommerceDbContext db, CancellationToken token)
    {
        var hashes = await db.ImportBatches.Where(x => x.Status == "Imported").OrderBy(x => x.Id)
            .Select(x => x.Sha256).ToListAsync(token);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(PipelineVersion + string.Join("|", hashes))));
    }

    private static string Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
    private static ModelInput ToInput(AdImpression row) => new()
    {
        Label = row.Click, DeviceType = Normalize(row.DeviceType), AdPosition = Normalize(row.AdPosition),
        BrowsingHistory = Normalize(row.BrowsingHistory), TimeOfDay = Normalize(row.TimeOfDay)
    };

    public class ModelInput
    {
        public bool Label { get; set; }
        public string DeviceType { get; set; } = "Unknown";
        public string AdPosition { get; set; } = "Unknown";
        public string BrowsingHistory { get; set; } = "Unknown";
        public string TimeOfDay { get; set; } = "Unknown";
    }
    public class ModelOutput
    {
        public bool Label { get; set; }
        [ColumnName("PredictedLabel")] public bool PredictedClick { get; set; }
        public float Probability { get; set; }
    }
}
