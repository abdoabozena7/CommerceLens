using System.Linq.Expressions;
using CommerceLens.Api.Data;
using CommerceLens.Api.DTOs;
using CommerceLens.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CommerceLens.Api.Services;

public class AnalyticsService(CommerceDbContext db)
{
    public async Task<OverviewDto> OverviewAsync(CancellationToken token)
    {
        var total = await db.AdImpressions.CountAsync(token);
        var clicks = await db.AdImpressions.CountAsync(x => x.Click, token);
        var latestScrape = await db.ScrapeRuns.AsNoTracking().OrderByDescending(x => x.Id)
            .Select(x => new ScrapeSummaryDto(x.Id, x.Status, x.StartedAt, x.CompletedAt, x.Records.Count, x.Error))
            .FirstOrDefaultAsync(token);
        var modelRecord = await db.ModelEvaluations.AsNoTracking().OrderByDescending(x => x.Id).FirstOrDefaultAsync(token);
        var fingerprint = await ClickPredictionService.DatasetFingerprintAsync(db, token);
        var latestModel = modelRecord is null ? null : new ModelSummaryDto(modelRecord.TrainedAt,
            modelRecord.DatasetFingerprint == fingerprint ? "Evaluated" : "Stale - retrain",
            modelRecord.DatasetFingerprint != fingerprint);
        var revenueCents = await db.Orders.SumAsync(x => (long)x.TotalCents, token);
        var liveClicks = await db.StoreEvents.CountAsync(x => x.EventType == "AdClick", token);
        var liveImpressions = await db.StoreEvents.CountAsync(x => x.EventType == "AdImpression", token);
        return new(total, clicks, total == 0 ? 0 : (double)clicks / total,
            await db.Products.CountAsync(token), await db.Orders.CountAsync(token), revenueCents / 100m,
            await db.StoreEvents.CountAsync(token), liveClicks,
            await db.StoreEvents.CountAsync(x => x.EventType == "ProductView", token),
            liveImpressions, liveImpressions == 0 ? null : (double)liveClicks / liveImpressions,
            await db.ScrapedProducts.CountAsync(token),
            await db.ImportBatches.Where(x => x.Status == "Imported").MaxAsync(x => (DateTime?)x.ImportedAt, token),
            latestScrape, latestModel);
    }

    public async Task<AdAnalyticsDto> AdvertisingAsync(AdFilter filter, CancellationToken token)
    {
        var query = db.AdImpressions.AsNoTracking().ApplyFilters(filter);
        var total = await query.CountAsync(token);
        var clicks = await query.CountAsync(x => x.Click, token);
        var missing = new List<MissingValueDto>
        {
            new("id", 0, total),
            new("full_name", await query.CountAsync(x => x.FullName == null, token), total),
            new("age", await query.CountAsync(x => x.Age == null, token), total),
            new("gender", await query.CountAsync(x => x.Gender == null, token), total),
            new("device_type", await query.CountAsync(x => x.DeviceType == null, token), total),
            new("ad_position", await query.CountAsync(x => x.AdPosition == null, token), total),
            new("browsing_history", await query.CountAsync(x => x.BrowsingHistory == null, token), total),
            new("time_of_day", await query.CountAsync(x => x.TimeOfDay == null, token), total),
            new("click", 0, total)
        };
        return new(total, clicks, total == 0 ? 0 : (double)clicks / total,
            await query.Select(x => x.OriginalUserId).Distinct().CountAsync(token),
            await query.CountAsync(x => x.FullName == null || x.Age == null || x.Gender == null
                || x.DeviceType == null || x.AdPosition == null || x.BrowsingHistory == null || x.TimeOfDay == null, token),
            await GroupAsync(query, x => x.DeviceType ?? "Unknown", token),
            await GroupAsync(query, x => x.AdPosition ?? "Unknown", token),
            await GroupAsync(query, x => x.TimeOfDay ?? "Unknown", token),
            await GroupAsync(query, x => x.BrowsingHistory ?? "Unknown", token), missing);
    }

    private static Task<List<GroupCtrDto>> GroupAsync(IQueryable<AdImpression> query,
        Expression<Func<AdImpression, string>> key, CancellationToken token) =>
        query.GroupBy(key).OrderByDescending(group => group.Count()).ThenBy(group => group.Key)
            .Select(group => new GroupCtrDto(group.Key, group.Count(), group.Count(x => x.Click))).ToListAsync(token);

    public async Task<CommerceAnalyticsDto> CommerceAsync(DateOnly? from, DateOnly? to, CancellationToken token)
    {
        if (from > to) throw new ApiException(400, "The start date must not be after the end date.");
        var query = db.Orders.AsNoTracking();
        if (from.HasValue)
        {
            var start = from.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(x => x.CreatedAt >= start);
        }
        if (to.HasValue)
        {
            var end = to.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(x => x.CreatedAt <= end);
        }
        var count = await query.CountAsync(token);
        var cents = await query.SumAsync(x => (long)x.TotalCents, token);
        var orderIds = query.Select(x => x.Id);
        var items = db.OrderItems.AsNoTracking().Where(x => orderIds.Contains(x.OrderId));
        var top = await items.GroupBy(x => new { x.ProductId, x.ProductName })
            .OrderByDescending(g => g.Sum(x => x.Quantity)).ThenBy(g => g.Key.ProductName)
            .Select(g => new CategorySaleDto(g.Key.ProductName, g.Sum(x => x.Quantity),
                g.Sum(x => (long)x.Quantity * x.UnitPriceCents)))
            .Take(8).ToListAsync(token);
        var categories = await items.GroupBy(x => x.Category)
            .OrderByDescending(g => g.Sum(x => (long)x.Quantity * x.UnitPriceCents))
            .Select(g => new CategorySaleDto(g.Key, g.Sum(x => x.Quantity), g.Sum(x => (long)x.Quantity * x.UnitPriceCents)))
            .ToListAsync(token);
        var daily = await query.GroupBy(x => x.CreatedAt.Date).OrderBy(g => g.Key)
            .Select(g => new DailySaleDto(g.Key, g.Count(), g.Sum(x => (long)x.TotalCents)))
            .ToListAsync(token);
        var recent = await query.Include(x => x.Items).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Take(10).ToListAsync(token);
        return new(count, cents / 100m, count == 0 ? 0 : cents / 100m / count,
            await items.SumAsync(x => x.Quantity, token), top, categories, daily,
            recent.Select(OrderDto.From).ToList(), "LocalDemoCheckout");
    }
}
