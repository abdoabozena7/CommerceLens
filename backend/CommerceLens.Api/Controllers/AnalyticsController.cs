using CommerceLens.Api.Data;
using CommerceLens.Api.DTOs;
using CommerceLens.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CommerceLens.Api.Controllers;

[ApiController]
[Route("api/analytics")]
public class AnalyticsController(AnalyticsService analytics, CommerceDbContext db) : ControllerBase
{
    [HttpGet("overview")]
    public Task<OverviewDto> Overview(CancellationToken token) => analytics.OverviewAsync(token);

    [HttpGet("advertising")]
    public Task<AdAnalyticsDto> Advertising([FromQuery] AdFilter filter, CancellationToken token) =>
        analytics.AdvertisingAsync(filter, token);

    [HttpGet("commerce")]
    public Task<CommerceAnalyticsDto> Commerce(DateOnly? from, DateOnly? to, CancellationToken token) =>
        analytics.CommerceAsync(from, to, token);

    [HttpGet("options")]
    public async Task<FilterOptionsDto> Options(CancellationToken token) => new(
        await db.AdImpressions.Select(x => x.DeviceType ?? "Unknown").Distinct().OrderBy(x => x).ToListAsync(token),
        await db.AdImpressions.Select(x => x.AdPosition ?? "Unknown").Distinct().OrderBy(x => x).ToListAsync(token),
        await db.AdImpressions.Select(x => x.BrowsingHistory ?? "Unknown").Distinct().OrderBy(x => x).ToListAsync(token),
        await db.AdImpressions.Select(x => x.TimeOfDay ?? "Unknown").Distinct().OrderBy(x => x).ToListAsync(token),
        (await db.ImportBatches.AsNoTracking().Where(x => x.Status == "Imported")
            .OrderByDescending(x => x.Id).ToListAsync(token)).Select(x => CsvImportService.ToResult(x)).ToList());
}
