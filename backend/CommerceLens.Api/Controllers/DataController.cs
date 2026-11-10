using System.ComponentModel.DataAnnotations;
using CommerceLens.Api.Data;
using CommerceLens.Api.DTOs;
using CommerceLens.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CommerceLens.Api.Controllers;

[ApiController]
[Route("api/data")]
public class DataController(CommerceDbContext db, CsvImportService importer) : ControllerBase
{
    [HttpGet("rows")]
    public async Task<PageDto<AdRowDto>> Rows([FromQuery] AdFilter filter,
        [Range(1, 1_000_000)] int page = 1, [Range(1, 100)] int pageSize = 25,
        string sort = "id", bool descending = false, CancellationToken token = default)
    {
        var query = db.AdImpressions.AsNoTracking().ApplyFilters(filter);
        var total = await query.CountAsync(token);
        var ordered = (sort, descending) switch
        {
            ("id", false) => query.OrderBy(x => x.Id),
            ("id", true) => query.OrderByDescending(x => x.Id),
            ("userId", false) => query.OrderBy(x => x.OriginalUserId),
            ("userId", true) => query.OrderByDescending(x => x.OriginalUserId),
            ("age", false) => query.OrderBy(x => x.Age),
            ("age", true) => query.OrderByDescending(x => x.Age),
            ("device", false) => query.OrderBy(x => x.DeviceType),
            ("device", true) => query.OrderByDescending(x => x.DeviceType),
            ("click", false) => query.OrderBy(x => x.Click),
            ("click", true) => query.OrderByDescending(x => x.Click),
            _ => throw new ApiException(400, "Unknown dataset sort column.")
        };
        // Names stay private; the explorer exposes analytical fields, not personal names.
        var items = await ordered.ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AdRowDto(x.Id, x.OriginalUserId, x.Age, x.Gender, x.DeviceType ?? "Unknown",
                x.AdPosition ?? "Unknown", x.BrowsingHistory ?? "Unknown", x.TimeOfDay ?? "Unknown", x.Click,
                x.ImportBatchId, x.ImportBatch.SourceType, x.ImportBatch.ImportedAt)).ToListAsync(token);
        return new(items, total, page, pageSize);
    }

    [HttpGet("imports")]
    public async Task<List<ImportResult>> Imports(CancellationToken token) =>
        (await db.ImportBatches.AsNoTracking().OrderByDescending(x => x.Id).Take(20).ToListAsync(token))
        .Select(x => CsvImportService.ToResult(x)).ToList();

    [HttpPost("imports")]
    [RequestSizeLimit(CsvImportService.MaxBytes + 65536)]
    public async Task<ActionResult<ImportResult>> Import(IFormFile file, CancellationToken token)
    {
        if (!Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            throw new ApiException(400, "Choose a .csv file using the supplied nine-column schema.");
        await using var stream = file.OpenReadStream();
        var result = await importer.ImportAsync(stream, file.FileName, token);
        return result.Status == "Rejected" ? UnprocessableEntity(result) : Ok(result);
    }
}
