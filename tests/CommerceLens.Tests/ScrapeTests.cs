using CommerceLens.Api.Models;
using CommerceLens.Api.Services;

namespace CommerceLens.Tests;

public class ScrapeTests
{
    [Fact]
    public void Comparisons_distinguish_matches_mismatches_missing_and_extra_products()
    {
        var products = new List<Product>
        {
            new() { Id = 1, Name = "Match", Category = "Home", PriceCents = 1000, Stock = 1 },
            new() { Id = 2, Name = "Changed", Category = "Home", PriceCents = 2000, Stock = 1 },
            new() { Id = 3, Name = "Missing", Category = "Home", PriceCents = 3000, Stock = 1 }
        };
        var records = new List<ScrapedProduct>
        {
            new() { StorefrontProductId = 1, Name = "Match", Category = "Home", PriceCents = 1000, Availability = "In stock" },
            new() { StorefrontProductId = 2, Name = "Changed", Category = "Home", PriceCents = 2100, Availability = "In stock" },
            new() { StorefrontProductId = 4, Name = "Extra", Category = "Home", PriceCents = 4000, Availability = "In stock" }
        };
        var compared = StorefrontScraperService.Compare(products, records);
        Assert.Equal("Matched", compared.Single(x => x.ProductId == 1).Status);
        Assert.Equal("Mismatched", compared.Single(x => x.ProductId == 2).Status);
        Assert.Contains("Price", compared.Single(x => x.ProductId == 2).Differences);
        Assert.Equal("Missing", compared.Single(x => x.ProductId == 3).Status);
        Assert.Equal("Extra", compared.Single(x => x.ProductId == 4).Status);
        Assert.Equal(0m, compared.Single(x => x.ProductId == 1).PriceDifference);
        Assert.Equal(1m, compared.Single(x => x.ProductId == 2).PriceDifference);
        Assert.Null(compared.Single(x => x.ProductId == 3).PriceDifference);
        Assert.Null(compared.Single(x => x.ProductId == 4).PriceDifference);
    }

    [Fact]
    public void Changed_product_information_and_price_reductions_preserve_before_and_after_values()
    {
        var products = new List<Product>
        {
            new() { Id = 1, Name = "Catalog name", Category = "Home", PriceCents = 12900, Stock = 1, Featured = true }
        };
        var records = new List<ScrapedProduct>
        {
            new() { StorefrontProductId = 1, Name = "Rendered name", Category = "Workspace", PriceCents = 11900, Availability = "Sold out" }
        };
        var comparison = Assert.Single(StorefrontScraperService.Compare(products, records));
        Assert.Equal("Mismatched", comparison.Status);
        Assert.Equal(["Name", "Price", "Category", "Availability", "Public ad card"], comparison.Differences);
        Assert.Equal(-10m, comparison.PriceDifference);
        Assert.Equal("In stock", comparison.CatalogAvailability);
        Assert.Equal("Sold out", comparison.CollectedAvailability);
        Assert.Equal("Catalog name", comparison.Name);
        Assert.Equal("Rendered name", comparison.CollectedName);
        Assert.Equal("Home", comparison.StorefrontCategory);
        Assert.Equal("Workspace", comparison.CollectedCategory);
    }
}
