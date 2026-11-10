using System.Text;
using System.Security.Cryptography;
using CommerceLens.Api.Data;
using CommerceLens.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CommerceLens.Tests;

public class ImportTests
{
    [Fact]
    public async Task Original_csv_is_imported_exactly_once_with_independent_observation_ids()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(token);
        await using var db = new CommerceDbContext(new DbContextOptionsBuilder<CommerceDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync(token);
        var service = new CsvImportService(db, NullLogger<CsvImportService>.Instance);
        await using var source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "ad_click_dataset.csv"));
        Assert.Equal("0C7A029BB57F281BC5C8FCE6940C75B54397F75749BF9A2D5CC1F6151113E78F",
            Convert.ToHexString(await SHA256.HashDataAsync(source, token)));
        source.Position = 0;
        var result = await service.ImportAsync(source, "ad_click_dataset.csv", token);
        Assert.Equal("Imported", result.Status);
        Assert.Equal(10_000, result.RowCount);
        Assert.Equal(6_500, result.ClickCount);
        Assert.Equal(10_000, await db.AdImpressions.CountAsync(token));
        Assert.Equal(4_000, await db.AdImpressions.Select(x => x.OriginalUserId).Distinct().CountAsync(token));
        Assert.Equal(10_000, await db.AdImpressions.Select(x => x.Id).Distinct().CountAsync(token));
        Assert.Equal(4_766, await db.AdImpressions.CountAsync(x => x.Age == null, token));
        Assert.Equal(2_000, await db.AdImpressions.CountAsync(x => x.DeviceType == null, token));
        Assert.Equal(4_693, await db.AdImpressions.CountAsync(x => x.Gender == null, token));
        Assert.Equal(2_000, await db.AdImpressions.CountAsync(x => x.AdPosition == null, token));
        Assert.Equal(4_782, await db.AdImpressions.CountAsync(x => x.BrowsingHistory == null, token));
        Assert.Equal(2_000, await db.AdImpressions.CountAsync(x => x.TimeOfDay == null, token));
        Assert.Equal(500, await db.AdImpressions.GroupBy(x => x.OriginalUserId).CountAsync(x => x.Count() > 1, token));
        source.Position = 0;
        var repeated = await service.ImportAsync(source, "renamed.csv", token);
        Assert.True(repeated.AlreadyImported);
        Assert.Equal(DateTimeKind.Utc, repeated.ImportedAt.Kind);
        Assert.Equal(10_000, await db.AdImpressions.CountAsync(token));
    }

    [Theory]
    [InlineData("id,click\n1,1", "header")]
    [InlineData("id,full_name,age,gender,device_type,ad_position,browsing_history,time_of_day,click\n1,Test,30,,Mobile,Top,,Morning,2", "click")]
    [InlineData("id,full_name,age,gender,device_type,ad_position,browsing_history,time_of_day,click\n1,Test,invalid,,Mobile,Top,,Morning,1", "age")]
    [InlineData("id,full_name,age,gender,device_type,ad_position,browsing_history,time_of_day,click\n1,Test,30,Mobile,Top,1", "csv")]
    [InlineData("id,full_name,age,gender,device_type,ad_position,browsing_history,time_of_day,click\n1,\"Unclosed,30,,Mobile,Top,,Morning,1", "csv")]
    public async Task Invalid_imports_report_errors_without_partial_observations(string csv, string column)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(token);
        await using var db = new CommerceDbContext(new DbContextOptionsBuilder<CommerceDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync(token);
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var result = await new CsvImportService(db, NullLogger<CsvImportService>.Instance).ImportAsync(stream, "invalid.csv", token);
        Assert.Equal("Rejected", result.Status);
        Assert.Contains(result.Errors, x => x.Column == column);
        Assert.Equal(0, await db.AdImpressions.CountAsync(token));
        Assert.Equal("Rejected", (await db.ImportBatches.SingleAsync(token)).Status);
    }
}
