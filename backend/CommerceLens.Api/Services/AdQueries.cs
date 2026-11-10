using CommerceLens.Api.DTOs;
using CommerceLens.Api.Models;

namespace CommerceLens.Api.Services;

public static class AdQueries
{
    public static IQueryable<AdImpression> ApplyFilters(this IQueryable<AdImpression> query, AdFilter filter)
    {
        // Coalescing here matches the Unknown groups used by charts and ML preprocessing.
        if (!string.IsNullOrEmpty(filter.DeviceType)) query = query.Where(x => (x.DeviceType ?? "Unknown") == filter.DeviceType);
        if (!string.IsNullOrEmpty(filter.AdPosition)) query = query.Where(x => (x.AdPosition ?? "Unknown") == filter.AdPosition);
        if (!string.IsNullOrEmpty(filter.BrowsingHistory)) query = query.Where(x => (x.BrowsingHistory ?? "Unknown") == filter.BrowsingHistory);
        if (!string.IsNullOrEmpty(filter.TimeOfDay)) query = query.Where(x => (x.TimeOfDay ?? "Unknown") == filter.TimeOfDay);
        if (filter.Click.HasValue) query = query.Where(x => x.Click == filter.Click);
        if (filter.ImportBatchId.HasValue) query = query.Where(x => x.ImportBatchId == filter.ImportBatchId);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(x => x.OriginalUserId.ToString().Contains(search)
                || (x.DeviceType ?? "Unknown").Contains(search) || (x.AdPosition ?? "Unknown").Contains(search)
                || (x.BrowsingHistory ?? "Unknown").Contains(search) || (x.TimeOfDay ?? "Unknown").Contains(search));
        }
        return query;
    }
}
