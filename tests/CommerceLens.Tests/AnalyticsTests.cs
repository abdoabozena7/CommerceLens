using System.Net;
using System.Net.Http.Json;
using System.Text;
using CommerceLens.Api.Data;
using CommerceLens.Api.DTOs;
using CommerceLens.Api.Models;
using CommerceLens.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CommerceLens.Tests;

public class AnalyticsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Grouped_ctr_uses_observations_as_denominator_and_combines_filters()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(token);
        await using var db = new CommerceDbContext(new DbContextOptionsBuilder<CommerceDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync(token);
        var batch = new ImportBatch { FileName = "fixture.csv", Sha256 = "fixture", RowCount = 4, ClickCount = 2 };
        db.AdImpressions.AddRange(
            new AdImpression { ImportBatch = batch, SourceRow = 2, OriginalUserId = 1, DeviceType = "Mobile", AdPosition = "Top", Click = true },
            new AdImpression { ImportBatch = batch, SourceRow = 3, OriginalUserId = 1, DeviceType = "Mobile", AdPosition = "Side", Click = false },
            new AdImpression { ImportBatch = batch, SourceRow = 4, OriginalUserId = 2, DeviceType = "Desktop", AdPosition = "Top", Click = true },
            new AdImpression { ImportBatch = batch, SourceRow = 5, OriginalUserId = 3, Click = false });
        await db.SaveChangesAsync(token);
        var analytics = new AnalyticsService(db);
        var all = await analytics.AdvertisingAsync(new(), token);
        Assert.Equal(0.5, all.Ctr);
        Assert.Equal(0.5, all.ByDevice.Single(x => x.Name == "Mobile").Ctr);
        Assert.Equal(1, all.ByDevice.Single(x => x.Name == "Desktop").Ctr);
        Assert.Equal(0, all.ByDevice.Single(x => x.Name == "Unknown").Ctr);
        Assert.Equal(1, all.MissingValues.Single(x => x.Column == "device_type").Missing);
        var filtered = await analytics.AdvertisingAsync(new() { DeviceType = "Mobile", AdPosition = "Top" }, token);
        Assert.Equal(1, filtered.TotalObservations);
        Assert.Equal(1, filtered.Ctr);
        var empty = await analytics.AdvertisingAsync(new() { DeviceType = "Unseen" }, token);
        Assert.Equal(0, empty.TotalObservations);
        Assert.Equal(0, empty.Ctr);
        Assert.Empty(empty.ByDevice);
    }

    [Fact]
    public async Task Overview_and_explorer_return_real_counts_and_no_names()
    {
        var token = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        var overview = await client.GetFromJsonAsync<OverviewDto>("/api/analytics/overview", token);
        Assert.Equal(10_000, overview!.TotalObservations);
        Assert.Equal(6_500, overview.TotalHistoricalClicks);
        Assert.Equal(0.65, overview.HistoricalCtr);
        var rows = await client.GetFromJsonAsync<PageDto<AdRowDto>>("/api/data/rows?page=2&pageSize=10", token);
        Assert.Equal(10_000, rows!.Total);
        Assert.Equal(10, rows.Items.Count);
        Assert.Equal(11, rows.Items[0].Id);
        var raw = await client.GetStringAsync("/api/data/rows?pageSize=1", token);
        Assert.DoesNotContain("fullName", raw, StringComparison.OrdinalIgnoreCase);
        var invalidPage = await client.GetAsync("/api/data/rows?page=0", token);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPage.StatusCode);
    }

    [Fact]
    public async Task Upload_rejects_bad_rows_with_persisted_validation_results()
    {
        var token = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("id,click\n1,2", Encoding.UTF8), "file", "invalid.csv");
        var response = await client.PostAsync("/api/data/imports", form, token);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<ImportResult>(token);
        Assert.Equal("Rejected", report!.Status);
        Assert.NotEmpty(report.Errors);
        var history = await client.GetFromJsonAsync<List<ImportResult>>("/api/data/imports", token);
        Assert.Contains(history!, x => x.Id == report.Id && x.Status == "Rejected");
        var overview = await client.GetFromJsonAsync<OverviewDto>("/api/analytics/overview", token);
        Assert.Equal(10_000, overview!.TotalObservations);
    }

    [Fact]
    public async Task Commerce_analytics_uses_saved_order_timestamps_and_exact_money()
    {
        var token = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        var product = (await client.GetFromJsonAsync<List<ProductDto>>("/api/products", token))![1];
        await client.PostAsJsonAsync("/api/orders", new CheckoutRequest([new(product.Id, 3)], Guid.NewGuid().ToString()), token);
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var sales = await client.GetFromJsonAsync<CommerceAnalyticsDto>($"/api/analytics/commerce?from={today}&to={today}", token);
        Assert.Equal(1, sales!.TotalOrders);
        Assert.Equal(product.Price * 3, sales.Revenue);
        Assert.Equal(product.Price * 3, sales.AverageOrderValue);
        Assert.Equal(3, sales.UnitsSold);
        Assert.Single(sales.DailySales);
        Assert.Equal(product.Category, Assert.Single(sales.ByCategory).Name);
        var future = await client.GetFromJsonAsync<CommerceAnalyticsDto>("/api/analytics/commerce?from=2099-01-01", token);
        Assert.Equal(0, future!.TotalOrders);
        Assert.Equal(0, future.Revenue);
    }

    [Theory]
    [InlineData(CsvImportService.MaxBytes + 1)]
    [InlineData(CsvImportService.MaxBytes + 131072)]
    public async Task Oversized_csv_uploads_return_413_without_importing_rows(int size)
    {
        var token = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(new byte[size]), "file", "oversized.csv");
        var response = await client.PostAsync("/api/data/imports", form, token);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(10_000, (await client.GetFromJsonAsync<OverviewDto>("/api/analytics/overview", token))!.TotalObservations);
    }
}
