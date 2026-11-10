using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using CommerceLens.Api.Data;
using CommerceLens.Api.DTOs;
using CommerceLens.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CommerceLens.Api.Services;

public class StorefrontScraperService(CommerceDbContext db, IConfiguration configuration,
    IWebHostEnvironment environment, ILogger<StorefrontScraperService> logger)
{
    private static readonly SemaphoreSlim RunLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ScrapeDetailDto> RunAsync(CancellationToken token)
    {
        if (!await RunLock.WaitAsync(0, token)) throw new ApiException(409, "A storefront collection is already running.");
        var sourceUrl = configuration["CommerceLens:StorefrontUrl"] ?? "http://127.0.0.1:5173/store";
        var run = new ScrapeRun { SourceUrl = sourceUrl };
        using var process = new Process();
        var started = false;
        try
        {
            if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var source)
                || source.Scheme != "http" || source.Host is not ("localhost" or "127.0.0.1"))
                throw new InvalidOperationException("CommerceLens:StorefrontUrl must point to the local HTTP storefront.");
            db.ScrapeRuns.Add(run);
            await db.SaveChangesAsync(token);
            var root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", ".."));
            var script = Path.Combine(root, "scraper", "src", "cli.ts");
            if (!File.Exists(script)) throw new InvalidOperationException("The storefront scraper script was not found.");
            process.StartInfo = new ProcessStartInfo
            {
                FileName = configuration["CommerceLens:NodeExecutable"] ?? "node",
                WorkingDirectory = root,
                RedirectStandardOutput = true, RedirectStandardError = true,
                UseShellExecute = false, CreateNoWindow = true
            };
            process.StartInfo.ArgumentList.Add(script);
            process.StartInfo.ArgumentList.Add(sourceUrl);
            started = process.Start();
            if (!started) throw new InvalidOperationException("Could not start the storefront scraper.");
            // Read both pipes concurrently so a browser error cannot fill stderr and deadlock stdout.
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(90));
            await process.WaitForExitAsync(timeout.Token);
            var output = await stdout;
            var diagnostic = await stderr;
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Playwright collection failed: {diagnostic[..Math.Min(diagnostic.Length, 2000)]}");
            var payload = JsonSerializer.Deserialize<ScrapePayload>(output, JsonOptions)
                ?? throw new InvalidDataException("The scraper returned no collection.");
            if (!Uri.TryCreate(payload.SourceUrl, UriKind.Absolute, out var collectedUrl)
                || collectedUrl.GetLeftPart(UriPartial.Authority) != source.GetLeftPart(UriPartial.Authority)
                || collectedUrl.AbsolutePath != "/store" || collectedUrl.UserInfo.Length > 0
                || payload.CompletedAt.Kind != DateTimeKind.Utc
                || payload.CompletedAt < run.StartedAt.AddSeconds(-5) || payload.CompletedAt > DateTime.UtcNow.AddSeconds(5))
                throw new InvalidDataException("The scraper returned invalid collection provenance or completion time.");
            if (payload.Records is null || payload.Records.Count is < 1 or > 1000
                || payload.Records.Select(x => x.StorefrontProductId).Distinct().Count() != payload.Records.Count)
                throw new InvalidDataException("The scraper returned an empty or invalid collection.");
            foreach (var record in payload.Records)
            {
                if (record.StorefrontProductId <= 0 || string.IsNullOrWhiteSpace(record.Name)
                    || string.IsNullOrWhiteSpace(record.Category) || record.PriceCents < 0
                    || record.Availability is not ("In stock" or "Sold out") || record.Status != "Collected"
                    || !Uri.TryCreate(record.SourceUrl, UriKind.Absolute, out var recordUrl)
                    || record.SourceUrl != payload.SourceUrl || record.ExtractedAt.Kind != DateTimeKind.Utc
                    || record.ExtractedAt < run.StartedAt.AddSeconds(-5) || record.ExtractedAt > payload.CompletedAt)
                    throw new InvalidDataException("A collected record has invalid fields or provenance.");
                run.Records.Add(new ScrapedProduct
                {
                    StorefrontProductId = record.StorefrontProductId, Name = record.Name, Category = record.Category,
                    PriceCents = record.PriceCents, Availability = record.Availability, AdText = record.AdText,
                    SourceUrl = record.SourceUrl, ExtractedAt = record.ExtractedAt, Status = record.Status
                });
            }
            run.Status = "Completed";
            run.SourceUrl = payload.SourceUrl;
            run.CompletedAt = payload.CompletedAt;
            await db.SaveChangesAsync(token);
            logger.LogInformation("Scrape {RunId} persisted {Count} rendered products", run.Id, run.Records.Count);
            return await DetailsAsync(run.Id, token);
        }
        catch (Exception error) when (error is Win32Exception or IOException or InvalidOperationException or JsonException or OperationCanceledException)
        {
            if (started && !process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
            logger.LogError(error, "Storefront collection {RunId} failed", run.Id);
            if (run.Id != 0)
            {
                // A failed collection must not leave partial records looking like successful evidence.
                run.Records.Clear();
                db.ChangeTracker.Clear();
                await db.ScrapeRuns.Where(x => x.Id == run.Id).ExecuteUpdateAsync(update => update
                    .SetProperty(x => x.Status, "Failed").SetProperty(x => x.Error, error.Message)
                    .SetProperty(x => x.CompletedAt, DateTime.UtcNow), CancellationToken.None);
            }
            throw new ApiException(error is OperationCanceledException ? 504 : 502,
                error is Win32Exception ? "Node.js was not found. Add Node 22.18+ to PATH or set CommerceLens:NodeExecutable."
                : error.Message);
        }
        finally { RunLock.Release(); }
    }

    public async Task<ScrapeDetailDto> DetailsAsync(int id, CancellationToken token)
    {
        var run = await db.ScrapeRuns.AsNoTracking().Include(x => x.Records).SingleOrDefaultAsync(x => x.Id == id, token)
            ?? throw new ApiException(404, "Scrape run not found.");
        List<ProductComparisonDto> comparisons = [];
        if (run.Status == "Completed")
        {
            var products = await db.Products.AsNoTracking().OrderBy(x => x.Name).ToListAsync(token);
            comparisons = Compare(products, run.Records);
        }
        return new(run.Id, run.Status, run.SourceType, run.SourceUrl, run.StartedAt, run.CompletedAt, run.Error,
            run.Records.Count, comparisons.Count(x => x.Status == "Matched"), comparisons.Count(x => x.Status == "Mismatched"),
            comparisons.Count(x => x.Status == "Missing"), comparisons.Count(x => x.Status == "Extra"), comparisons);
    }

    public static List<ProductComparisonDto> Compare(List<Product> products, List<ScrapedProduct> records)
    {
        var originals = products.ToDictionary(x => x.Id);
        var collected = records.ToDictionary(x => x.StorefrontProductId);
        return originals.Keys.Union(collected.Keys).Select(id =>
        {
            originals.TryGetValue(id, out var product);
            collected.TryGetValue(id, out var record);
            var differences = new List<string>();
            var status = "Matched";
            if (product is null) { status = "Extra"; differences.Add("Not in current catalog"); }
            else if (record is null) { status = "Missing"; differences.Add("Not collected"); }
            else
            {
                if (product.Name != record.Name) differences.Add("Name");
                if (product.PriceCents != record.PriceCents) differences.Add("Price");
                if (product.Category != record.Category) differences.Add("Category");
                if ((product.Stock > 0 ? "In stock" : "Sold out") != record.Availability) differences.Add("Availability");
                if (product.Featured != !string.IsNullOrWhiteSpace(record.AdText)) differences.Add("Public ad card");
                if (differences.Count > 0) status = "Mismatched";
            }
            return new ProductComparisonDto(id, product?.Name ?? record!.Name, record?.Name,
                product is null ? null : product.PriceCents / 100m, record is null ? null : record.PriceCents / 100m,
                product?.Category, record?.Category, status, differences, record?.AdText, record?.SourceUrl, record?.ExtractedAt,
                product is null ? null : product.Stock > 0 ? "In stock" : "Sold out", record?.Availability);
        }).OrderBy(x => x.Name).ToList();
    }
}
