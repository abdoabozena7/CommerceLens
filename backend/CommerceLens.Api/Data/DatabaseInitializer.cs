using CommerceLens.Api.Models;
using CommerceLens.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace CommerceLens.Api.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        await db.Database.MigrateAsync();
        var interrupted = await db.ScrapeRuns.Where(x => x.Status == "Running").ExecuteUpdateAsync(update => update
            .SetProperty(x => x.Status, "Failed")
            .SetProperty(x => x.Error, "The backend stopped before this collection completed.")
            .SetProperty(x => x.CompletedAt, DateTime.UtcNow));
        if (interrupted > 0) app.Logger.LogWarning("Marked {Count} interrupted scrape runs as failed", interrupted);
        var configured = app.Configuration["CommerceLens:DatasetPath"] ?? @"..\..\dataset\ad_click_dataset.csv";
        var dataset = Path.GetFullPath(configured, app.Environment.ContentRootPath);
        if (!File.Exists(dataset))
            throw new FileNotFoundException("The original CSV is required. Set CommerceLens:DatasetPath if it was moved.", dataset);
        await using var stream = File.OpenRead(dataset);
        var result = await scope.ServiceProvider.GetRequiredService<CsvImportService>()
            .ImportAsync(stream, Path.GetFileName(dataset));
        if (result.Status != "Imported")
            throw new InvalidDataException($"Original CSV validation failed: {System.Text.Json.JsonSerializer.Serialize(result.Errors)}");

        if (!await db.Products.AnyAsync())
        {
            // This catalog is independent seed data, never inferred from advertising observations.
            db.Products.AddRange(
                Catalog("arc-headphones", "Arc Wireless Headphones", "Electronics", 12900, "headphones", "lilac", true,
                    "Comfortable wireless listening, soft ear cushions and 30-hour battery life."),
                Catalog("studio-lamp", "Studio Desk Lamp", "Workspace", 6800, "lamp", "sand", true,
                    "A warm, adjustable light for a more focused workspace."),
                Catalog("everyday-backpack", "Everyday Backpack", "Lifestyle", 8900, "backpack", "mint", true,
                    "A lightweight everyday carry with a padded laptop compartment."),
                Catalog("ceramic-mug", "Ceramic Coffee Mug", "Home", 2400, "mug", "rose", false,
                    "A generously sized ceramic mug with a tactile matte finish."),
                Catalog("mechanical-keyboard", "Compact Mechanical Keyboard", "Electronics", 11500, "keyboard", "blue", false,
                    "A compact keyboard with tactile switches and a clean, quiet profile."),
                Catalog("desk-organizer", "Modular Desk Organizer", "Workspace", 3900, "organizer", "sand", false,
                    "Keep your daily tools within reach with a modular desk tray."),
                Catalog("insulated-bottle", "Insulated Water Bottle", "Lifestyle", 3200, "bottle", "mint", false,
                    "A durable double-wall bottle for cold water or hot coffee."),
                Catalog("woven-throw", "Woven Cotton Throw", "Home", 5400, "throw", "lilac", false,
                    "A soft cotton layer for a slower, more comfortable evening."));
            await db.SaveChangesAsync();
        }
    }

    private static Product Catalog(string slug, string name, string category, int cents,
        string visual, string accent, bool featured, string description) => new()
        {
            Slug = slug, Name = name, Category = category, PriceCents = cents, Stock = 50,
            Visual = visual, Accent = accent, Featured = featured, Description = description
        };
}
