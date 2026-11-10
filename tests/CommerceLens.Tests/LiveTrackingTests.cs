using System.Net;
using System.Net.Http.Json;
using CommerceLens.Api.DTOs;

namespace CommerceLens.Tests;

public class LiveTrackingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Impression_retries_are_idempotent_and_live_ctr_is_separate_from_historical_ctr()
    {
        var token = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        var before = (await client.GetFromJsonAsync<OverviewDto>("/api/analytics/overview", token))!;
        Assert.Equal(0, before.LiveAdImpressions);
        Assert.Null(before.LiveCtr);
        var products = (await client.GetFromJsonAsync<List<ProductDto>>("/api/products", token))!;
        var featured = products.First(x => x.Featured);
        var key = Guid.NewGuid().ToString();
        var impression = new EventRequest(featured.Id, "AdImpression", key);
        var retries = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => client.PostAsJsonAsync("/api/events", impression, token)));
        foreach (var response in retries)
        {
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            response.Dispose();
        }
        var click = new EventRequest(featured.Id, "AdClick", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/events", click, token)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/events", click, token)).StatusCode);
        var after = (await client.GetFromJsonAsync<OverviewDto>("/api/analytics/overview", token))!;
        Assert.Equal(1, after.LiveAdImpressions);
        Assert.Equal(1, after.LiveAdClicks);
        Assert.Equal(1d, after.LiveCtr);
        Assert.Equal(2, after.TotalEvents);
        Assert.Equal(10_000, after.TotalObservations);
        Assert.Equal(6_500, after.TotalHistoricalClicks);
        Assert.Equal(0.65, after.HistoricalCtr);
        Assert.Equal(before.TotalOrders, after.TotalOrders);
        Assert.Equal(before.Revenue, after.Revenue);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/events",
            impression with { EventType = "AdClick" }, token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/events",
            new EventRequest(featured.Id, "AdImpression"), token)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/events",
            new EventRequest(products.First(x => !x.Featured).Id, "AdImpression", Guid.NewGuid().ToString()), token)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/events",
            new EventRequest(int.MaxValue, "ProductView", Guid.NewGuid().ToString()), token)).StatusCode);
    }
}
