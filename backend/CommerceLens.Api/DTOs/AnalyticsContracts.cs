using System.ComponentModel.DataAnnotations;

namespace CommerceLens.Api.DTOs;

public class AdFilter
{
    [StringLength(100)] public string? DeviceType { get; set; }
    [StringLength(100)] public string? AdPosition { get; set; }
    [StringLength(100)] public string? BrowsingHistory { get; set; }
    [StringLength(100)] public string? TimeOfDay { get; set; }
    public bool? Click { get; set; }
    [Range(1, int.MaxValue)] public int? ImportBatchId { get; set; }
    [StringLength(120)] public string? Search { get; set; }
}

public record GroupCtrDto(string Name, int Observations, int Clicks)
{
    public double Ctr => Observations == 0 ? 0 : (double)Clicks / Observations;
}
public record MissingValueDto(string Column, int Missing, int Total)
{
    public int Present => Total - Missing;
    public double MissingRate => Total == 0 ? 0 : (double)Missing / Total;
}
public record AdAnalyticsDto(int TotalObservations, int TotalClicks, double Ctr, int DistinctUsers,
    int RowsWithMissingValues, List<GroupCtrDto> ByDevice, List<GroupCtrDto> ByPosition,
    List<GroupCtrDto> ByTimeOfDay, List<GroupCtrDto> ByBrowsingHistory, List<MissingValueDto> MissingValues);
public record FilterOptionsDto(List<string> DeviceTypes, List<string> AdPositions,
    List<string> BrowsingHistories, List<string> TimesOfDay, List<ImportResult> Imports);
public record ScrapeSummaryDto(int Id, string Status, DateTime StartedAt, DateTime? CompletedAt, int RecordCount, string? Error);
public record ModelSummaryDto(DateTime TrainedAt, string Status, bool IsStale);
public record OverviewDto(int TotalObservations, int TotalHistoricalClicks, double HistoricalCtr,
    int TotalProducts, int TotalOrders, decimal Revenue, int TotalEvents, int LiveAdClicks,
    int ProductViews, int LiveAdImpressions, double? LiveCtr, int TotalScrapedRecords, DateTime? LastImportedAt,
    ScrapeSummaryDto? LastScrape, ModelSummaryDto? LatestModel);
public record AdRowDto(long Id, long OriginalUserId, int? Age, string? Gender, string DeviceType,
    string AdPosition, string BrowsingHistory, string TimeOfDay, bool Click, int ImportBatchId,
    string SourceType, DateTime ImportedAt);
public record PageDto<T>(List<T> Items, int Total, int Page, int PageSize);
public record CategorySaleDto(string Name, int Units, long RevenueCents)
{
    public decimal Revenue => RevenueCents / 100m;
}
public record DailySaleDto(DateTime Date, int Orders, long RevenueCents)
{
    public decimal Revenue => RevenueCents / 100m;
}
public record CommerceAnalyticsDto(int TotalOrders, decimal Revenue, decimal AverageOrderValue,
    int UnitsSold, List<CategorySaleDto> TopProducts, List<CategorySaleDto> ByCategory,
    List<DailySaleDto> DailySales, List<OrderDto> RecentOrders, string SourceType);
