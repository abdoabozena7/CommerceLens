using System.Net;
using System.Net.Http.Json;
using CommerceLens.Api.DTOs;
using Microsoft.AspNetCore.Hosting;

namespace CommerceLens.Tests;

public class MissingNodeFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("CommerceLens:NodeExecutable", Path.Combine(Path.GetTempPath(), "commercelens-deliberately-missing-node.exe"));
    }
}

public class ScrapeFailureTests(MissingNodeFactory factory) : IClassFixture<MissingNodeFactory>
{
    [Fact]
    public async Task Missing_node_is_reported_as_a_failed_run_not_fake_collected_data()
    {
        var token = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        var failed = await client.PostAsync("/api/scrapes", null, token);
        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        Assert.Contains("Node.js was not found", await failed.Content.ReadAsStringAsync(token));
        var runs = await client.GetFromJsonAsync<List<ScrapeSummaryDto>>("/api/scrapes", token);
        var run = Assert.Single(runs!);
        Assert.Equal("Failed", run.Status);
        Assert.Equal(0, run.RecordCount);
        Assert.NotEmpty(run.Error!);
        var detail = (await client.GetFromJsonAsync<ScrapeDetailDto>($"/api/scrapes/{run.Id}", token))!;
        Assert.Empty(detail.Comparisons);
        Assert.Equal(0, detail.MissingCount);
        Assert.Equal(0, detail.PriceMismatchCount);
    }
}
