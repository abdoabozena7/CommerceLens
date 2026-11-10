using CommerceLens.Api.Data;
using CommerceLens.Api.Models;
using CommerceLens.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace CommerceLens.Tests;

public class MigrationTests
{
    [Fact]
    public async Task Upgrade_preserves_original_import_orders_scrapes_and_legacy_events()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(token);
        await using var db = new CommerceDbContext(new DbContextOptionsBuilder<CommerceDbContext>().UseSqlite(connection).Options);
        await db.GetService<IMigrator>().MigrateAsync("20261110095150_InitialCreate", token);
        await using var csv = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "ad_click_dataset.csv"));
        var batch = await new CsvImportService(db, NullLogger<CsvImportService>.Instance).ImportAsync(csv, "original.csv", token);
        var product = new Product { Name = "Migration fixture", Slug = "migration-fixture", Category = "Test", PriceCents = 1200, Stock = 7 };
        db.Products.Add(product);
        var order = new Order
        {
            RequestKey = Guid.NewGuid().ToString(), RequestHash = "migration-fixture", TotalCents = 1200,
            Items = [new() { Product = product, ProductName = product.Name, Category = product.Category, Quantity = 1, UnitPriceCents = 1200 }]
        };
        db.Orders.Add(order);
        db.ScrapeRuns.Add(new()
        {
            Status = "Completed", SourceUrl = "http://127.0.0.1:5173/store",
            Records = [new() { StorefrontProductId = 1, Name = product.Name, Category = product.Category, PriceCents = 1200 }]
        });
        await db.SaveChangesAsync(token);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO StoreEvents (ProductId, EventType, SourceType, CollectedAt) VALUES ({product.Id}, {"AdClick"}, {"LiveApplication"}, {DateTime.UtcNow})", token);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO StoreEvents (ProductId, EventType, SourceType, CollectedAt) VALUES ({product.Id}, {"ProductView"}, {"LiveApplication"}, {DateTime.UtcNow})", token);
        await db.Database.MigrateAsync(token);
        db.ChangeTracker.Clear();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(token));
        Assert.Equal(10_000, await db.AdImpressions.CountAsync(token));
        Assert.Equal(6_500, await db.AdImpressions.CountAsync(x => x.Click, token));
        Assert.Equal(batch.Id, (await db.ImportBatches.SingleAsync(token)).Id);
        Assert.Equal(1200, (await db.Orders.Include(x => x.Items).SingleAsync(token)).TotalCents);
        Assert.Single((await db.Orders.Include(x => x.Items).SingleAsync(token)).Items);
        Assert.Equal(7, (await db.Products.SingleAsync(token)).Stock);
        Assert.Equal(1, await db.ScrapedProducts.CountAsync(token));
        Assert.Equal(2, await db.StoreEvents.CountAsync(token));
        Assert.All(await db.StoreEvents.ToListAsync(token), x => Assert.Null(x.EventKey));
        Assert.Equal(9, db.Model.GetEntityTypes().Count());
    }

    [Fact]
    public async Task Retiring_unused_models_preserves_legacy_rows_and_migration_history_in_both_directions()
    {
        var token = TestContext.Current.CancellationToken;
        const string previousMigration = "20261110112212_LiveTrackingAndSocialFeedback";
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(token);
        await using var db = new CommerceDbContext(new DbContextOptionsBuilder<CommerceDbContext>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(previousMigration, token);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO SocialImportBatches
                (FileName, Sha256, SourceType, ImportedAt, TotalRecords, ImportedComments, DuplicateComments, IgnoredPosts, FixtureComments)
            VALUES ({"retained.json"}, {"retained-hash"}, {"ArachneJsonExport"}, {DateTime.UtcNow}, {1}, {1}, {0}, {0}, {1})
            """, token);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO SocialComments
                (ImportBatchId, RecordId, RunId, SourceMode, SourceUrl, Text, CapturedAt, PublishedAtRaw, ReferenceKind, Truncated)
            VALUES ({1}, {"retained-id"}, {"retained-run"}, {"fixture"}, {"https://example.com/archive"},
                {"Preserve every archived value."}, {DateTime.UtcNow.AddDays(-1)}, {"2026-11-09T09:30:00"}, {"observed_element"}, {1})
            """, token);
        var before = new Dictionary<string, string>();
        foreach (var table in new[] { "SocialImportBatches", "SocialComments" })
            before[table] = await SnapshotAsync(connection, table, token);

        await db.Database.MigrateAsync(token);
        var history = (await db.Database.GetAppliedMigrationsAsync(token)).ToList();
        Assert.Contains("20261110095150_InitialCreate", history);
        Assert.Contains(previousMigration, history);
        Assert.Equal(3, history.Count);
        Assert.Equal(9, db.Model.GetEntityTypes().Count());
        foreach (var table in before.Keys) Assert.Equal(before[table], await SnapshotAsync(connection, table, token));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(token));

        await migrator.MigrateAsync(previousMigration, token);
        foreach (var table in before.Keys) Assert.Equal(before[table], await SnapshotAsync(connection, table, token));
        await db.Database.MigrateAsync(token);
        foreach (var table in before.Keys) Assert.Equal(before[table], await SnapshotAsync(connection, table, token));
    }

    private static async Task<string> SnapshotAsync(SqliteConnection connection, string table, CancellationToken token)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM \"{table}\" ORDER BY Id";
        await using var reader = await command.ExecuteReaderAsync(token);
        var rows = new List<object?[]>();
        while (await reader.ReadAsync(token))
        {
            var row = new object?[reader.FieldCount];
            for (var index = 0; index < row.Length; index++)
                row[index] = reader.IsDBNull(index) ? null : reader.GetValue(index);
            rows.Add(row);
        }
        return JsonSerializer.Serialize(rows);
    }
}
