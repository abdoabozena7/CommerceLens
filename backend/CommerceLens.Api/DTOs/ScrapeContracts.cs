namespace CommerceLens.Api.DTOs;

public record CollectedProductDto(int StorefrontProductId, string Name, string Category, int PriceCents,
    string Availability, string? AdText, string SourceUrl, DateTime ExtractedAt, string Status);
public record ScrapePayload(string SourceUrl, DateTime CompletedAt, List<CollectedProductDto> Records);
public record ProductComparisonDto(int ProductId, string Name, string? CollectedName,
    decimal? StorefrontPrice, decimal? CollectedPrice, string? StorefrontCategory, string? CollectedCategory,
    string Status, List<string> Differences, string? AdText, string? SourceUrl, DateTime? ExtractedAt,
    string? CatalogAvailability, string? CollectedAvailability)
{
    public decimal? PriceDifference => CollectedPrice - StorefrontPrice;
}
public record ScrapeDetailDto(int Id, string Status, string SourceType, string SourceUrl,
    DateTime StartedAt, DateTime? CompletedAt, string? Error, int RecordCount, int MatchedCount,
    int MismatchedCount, int MissingCount, int ExtraCount, List<ProductComparisonDto> Comparisons)
{
    public int PriceMismatchCount => Comparisons.Count(x => x.Differences.Contains("Price"));
}
