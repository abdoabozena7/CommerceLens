using System.Net;
using System.Net.Http.Json;
using CommerceLens.Api.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CommerceLens.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string database = Path.Combine(Path.GetTempPath(), $"commercelens-tests-{Guid.NewGuid():N}.db");
    private readonly string models = Path.Combine(Path.GetTempPath(), $"commercelens-test-models-{Guid.NewGuid():N}");
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:CommerceLens", $"Data Source={database};Pooling=False");
        builder.UseSetting("CommerceLens:DatasetPath", Path.Combine(AppContext.BaseDirectory, "ad_click_dataset.csv"));
        builder.UseSetting("CommerceLens:ModelDirectory", models);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) Cleanup();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        Cleanup();
    }

    private void Cleanup()
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" }) File.Delete(database + suffix);
        if (Directory.Exists(models))
        {
            foreach (var file in Directory.GetFiles(models, "ad-click-*.zip")) File.Delete(file);
            Directory.Delete(models);
        }
    }
}

public class StoreTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Products_and_checkout_use_the_database_and_retry_does_not_duplicate_orders()
    {
        var token = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        var products = await client.GetFromJsonAsync<List<ProductDto>>("/api/products", token);
        Assert.NotNull(products);
        Assert.Equal(8, products.Count);
        Assert.All(products, product => Assert.Equal("DemoCatalog", product.SourceType));
        var product = products[0];
        var request = new CheckoutRequest([new(product.Id, 2)], Guid.NewGuid().ToString());
        var placed = await client.PostAsJsonAsync("/api/orders", request, token);
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        var order = await placed.Content.ReadFromJsonAsync<OrderDto>(token);
        Assert.NotNull(order);
        Assert.Equal(product.Price * 2, order.Total);
        Assert.Equal("LocalDemoCheckout", order.SourceType);
        Assert.NotNull(placed.Headers.Location);
        var retrieved = await client.GetFromJsonAsync<OrderDto>(placed.Headers.Location, token);
        Assert.Equal(order.Id, retrieved!.Id);
        Assert.Equal(order.Total, retrieved.Total);
        Assert.Equal(DateTimeKind.Utc, retrieved.CreatedAt.Kind);
        var repeat = await client.PostAsJsonAsync("/api/orders", request, token);
        var repeatedOrder = await repeat.Content.ReadFromJsonAsync<OrderDto>(token);
        Assert.Equal(order.Id, repeatedOrder!.Id);
        Assert.Equal(DateTimeKind.Utc, repeatedOrder.CreatedAt.Kind);
        var after = await client.GetFromJsonAsync<ProductDto>($"/api/products/{product.Id}", token);
        Assert.Equal(product.Stock - 2, after!.Stock);
        var changed = await client.PostAsJsonAsync("/api/orders",
            request with { Items = [new(product.Id, 3)] }, token);
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
    }

    [Fact]
    public async Task Search_filters_and_invalid_checkout_and_events_are_validated()
    {
        var token = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        var filtered = await client.GetFromJsonAsync<List<ProductDto>>("/api/products?category=Electronics&search=Arc", token);
        Assert.Single(filtered!);
        Assert.Equal("Arc Wireless Headphones", filtered![0].Name);
        var invalid = await client.PostAsJsonAsync("/api/orders",
            new CheckoutRequest([new(1, 0)], Guid.NewGuid().ToString()), token);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var badEvent = await client.PostAsJsonAsync("/api/events", new EventRequest(1, "Purchase"), token);
        Assert.Equal(HttpStatusCode.BadRequest, badEvent.StatusCode);
        var adClick = await client.PostAsJsonAsync("/api/events", new EventRequest(1, "AdClick"), token);
        Assert.Equal(HttpStatusCode.NoContent, adClick.StatusCode);
    }

    [Fact]
    public async Task Out_of_stock_checkout_rolls_back_other_product_updates()
    {
        var token = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        var products = (await client.GetFromJsonAsync<List<ProductDto>>("/api/products", token))!;
        var last = products.Single(x => x.Slug == "woven-throw");
        var other = products.Single(x => x.Slug == "studio-lamp");
        for (var i = 0; i < 2; i++)
        {
            var response = await client.PostAsJsonAsync("/api/orders",
                new CheckoutRequest([new(last.Id, 25)], Guid.NewGuid().ToString()), token);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
        var rejected = await client.PostAsJsonAsync("/api/orders",
            new CheckoutRequest([new(other.Id, 1), new(last.Id, 1)], Guid.NewGuid().ToString()), token);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Equal(other.Stock, (await client.GetFromJsonAsync<ProductDto>($"/api/products/{other.Id}", token))!.Stock);
        Assert.Equal(0, (await client.GetFromJsonAsync<ProductDto>($"/api/products/{last.Id}", token))!.Stock);
    }

    [Theory]
    [InlineData("{\"items\":[null],\"requestKey\":\"1234567890123456\"}")]
    [InlineData("{\"items\":null,\"requestKey\":\"1234567890123456\"}")]
    public async Task Null_cart_input_is_rejected_with_validation_errors(string payload)
    {
        var token = TestContext.Current.CancellationToken;
        using var client = factory.CreateClient();
        using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/orders", content, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
