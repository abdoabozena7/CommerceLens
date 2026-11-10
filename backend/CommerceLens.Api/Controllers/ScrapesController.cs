using CommerceLens.Api.Data;
using CommerceLens.Api.DTOs;
using CommerceLens.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CommerceLens.Api.Controllers;

[ApiController]
[Route("api/scrapes")]
public class ScrapesController(StorefrontScraperService scraper, CommerceDbContext db) : ControllerBase
{
    [HttpGet]
    public Task<List<ScrapeSummaryDto>> List(CancellationToken token) =>
        db.ScrapeRuns.AsNoTracking().OrderByDescending(x => x.Id).Take(20)
            .Select(x => new ScrapeSummaryDto(x.Id, x.Status, x.StartedAt, x.CompletedAt, x.Records.Count, x.Error)).ToListAsync(token);

    [HttpGet("{id:int}")]
    public Task<ScrapeDetailDto> Details(int id, CancellationToken token) => scraper.DetailsAsync(id, token);

    [HttpPost]
    public async Task<ActionResult<ScrapeDetailDto>> Run(CancellationToken token)
    {
        var result = await scraper.RunAsync(token);
        return Created($"/api/scrapes/{result.Id}", result);
    }
}
