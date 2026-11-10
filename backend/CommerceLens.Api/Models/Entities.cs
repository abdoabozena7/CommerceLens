namespace CommerceLens.Api.Models;

public class ImportBatch
{
    public int Id { get; set; }
    public string FileName { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string Status { get; set; } = "Imported";
    public string SourceType { get; set; } = "HistoricalCsv";
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public int RowCount { get; set; }
    public int ClickCount { get; set; }
    public string ErrorsJson { get; set; } = "[]";
}

public class AdImpression
{
    public long Id { get; set; }
    public int ImportBatchId { get; set; }
    public ImportBatch ImportBatch { get; set; } = null!;
    public int SourceRow { get; set; }
    public long OriginalUserId { get; set; }
    public string? FullName { get; set; }
    public int? Age { get; set; }
    public string? Gender { get; set; }
    public string? DeviceType { get; set; }
    public string? AdPosition { get; set; }
    public string? BrowsingHistory { get; set; }
    public string? TimeOfDay { get; set; }
    public bool Click { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public int PriceCents { get; set; }
    public int Stock { get; set; }
    public string Visual { get; set; } = "";
    public string Accent { get; set; } = "";
    public bool Featured { get; set; }
    public string SourceType { get; set; } = "DemoCatalog";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Order
{
    public int Id { get; set; }
    public string RequestKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string SourceType { get; set; } = "LocalDemoCheckout";
    public int TotalCents { get; set; }
    public List<OrderItem> Items { get; set; } = [];
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string ProductName { get; set; } = "";
    public string Category { get; set; } = "";
    public int UnitPriceCents { get; set; }
    public int Quantity { get; set; }
}

public class StoreEvent
{
    public long Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string EventType { get; set; } = "";
    public string? EventKey { get; set; }
    public string SourceType { get; set; } = "LiveApplication";
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;
}

public class ScrapeRun
{
    public int Id { get; set; }
    public string SourceUrl { get; set; } = "";
    public string SourceType { get; set; } = "RenderedStorefront";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string Status { get; set; } = "Running";
    public string? Error { get; set; }
    public List<ScrapedProduct> Records { get; set; } = [];
}

public class ScrapedProduct
{
    public int Id { get; set; }
    public int ScrapeRunId { get; set; }
    public ScrapeRun ScrapeRun { get; set; } = null!;
    public int StorefrontProductId { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public int PriceCents { get; set; }
    public string Availability { get; set; } = "";
    public string? AdText { get; set; }
    public string SourceUrl { get; set; } = "";
    public DateTime ExtractedAt { get; set; }
    public string Status { get; set; } = "Collected";
}

public class ModelEvaluation
{
    public int Id { get; set; }
    public DateTime TrainedAt { get; set; } = DateTime.UtcNow;
    public string DatasetFingerprint { get; set; } = "";
    public string MetricsJson { get; set; } = "";
}
