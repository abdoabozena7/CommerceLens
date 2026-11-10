using System.Net.Http.Json;
using CommerceLens.Api.Data;
using CommerceLens.Api.DTOs;
using CommerceLens.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceLens.Tests;

public class ProductMonitoringTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Saved_collection_comparison_reports_exact_price_delta_missing_products_and_changed_information()
    {
        var token = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var products = await db.Products.AsNoTracking().OrderBy(x => x.Id).ToListAsync(token);
        var changed = products[1];
        var run = new ScrapeRun
        {
            Status = "Completed", SourceUrl = "http://127.0.0.1:5173/store?collection=1",
            CompletedAt = DateTime.UtcNow,
            Records = products.Take(2).Select(product => new ScrapedProduct
            {
                StorefrontProductId = product.Id, Name = product.Name, Category = product.Category,
                PriceCents = product.PriceCents, Availability = "In stock",
                AdText = product.Featured ? "Visible test offer" : null,
                SourceUrl = "http://127.0.0.1:5173/store?collection=1", ExtractedAt = DateTime.UtcNow
            }).ToList()
        };
        run.Records[1].Name = "Changed rendered title";
        run.Records[1].PriceCents += 125;
        run.Records[1].Category = "Observed category";
        run.Records[1].Availability = "Sold out";
        db.ScrapeRuns.Add(run);
        await db.SaveChangesAsync(token);

        var detail = (await client.GetFromJsonAsync<ScrapeDetailDto>($"/api/scrapes/{run.Id}", token))!;
        Assert.Equal(2, detail.RecordCount);
        Assert.Equal(1, detail.MatchedCount);
        Assert.Equal(1, detail.MismatchedCount);
        Assert.Equal(1, detail.PriceMismatchCount);
        Assert.Equal(6, detail.MissingCount);
        Assert.Equal(0, detail.ExtraCount);
        var comparison = detail.Comparisons.Single(x => x.ProductId == changed.Id);
        Assert.Equal(1.25m, comparison.PriceDifference);
        Assert.Equal(changed.PriceCents / 100m, comparison.StorefrontPrice);
        Assert.Equal("Changed rendered title", comparison.CollectedName);
        Assert.Equal("Observed category", comparison.CollectedCategory);
        Assert.Equal("Sold out", comparison.CollectedAvailability);
        var overview = (await client.GetFromJsonAsync<OverviewDto>("/api/analytics/overview", token))!;
        Assert.Equal(10_000, overview.TotalObservations);
        Assert.Equal(6_500, overview.TotalHistoricalClicks);
        Assert.Equal(0, overview.TotalOrders);
        Assert.Equal(0, overview.TotalEvents);
        Assert.Equal(2, overview.TotalScrapedRecords);
    }
}
