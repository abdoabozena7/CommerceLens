using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CommerceLens.Api.Data;
using CommerceLens.Api.DTOs;
using CommerceLens.Api.Models;
using CsvHelper;
using Microsoft.EntityFrameworkCore;

namespace CommerceLens.Api.Services;

public class CsvImportService(CommerceDbContext db, ILogger<CsvImportService> logger)
{
    public const int MaxBytes = 20 * 1024 * 1024;
    private static readonly SemaphoreSlim ImportLock = new(1, 1);
    private static readonly string[] Columns =
        ["id", "full_name", "age", "gender", "device_type", "ad_position", "browsing_history", "time_of_day", "click"];

    public async Task<ImportResult> ImportAsync(Stream source, string fileName, CancellationToken token = default)
    {
        var bytes = await UploadReader.ReadAsync(source, MaxBytes, "CSV files must be at most 20 MB.", token);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        await ImportLock.WaitAsync(token);
        try
        {
            var existing = await db.ImportBatches.AsNoTracking().SingleOrDefaultAsync(x => x.Sha256 == hash, token);
            if (existing is not null) return ToResult(existing, true);

            var errors = new List<ImportError>();
            var observations = new List<AdImpression>();
            using var csv = CsvReader.Create(new MemoryStream(bytes), new CsvReaderOptions
            {
                Separator = ',',
                TrimWhitespace = true,
                TrackLineNumbers = true,
                IncludeValuesInExceptions = false,
                MalformedData = CsvMalformedDataHandling.Throw,
                FieldCountMismatch = CsvFieldCountHandling.Throw
            });
            try
            {
                await csv.ReadHeaderAsync(token);
                var actual = csv.Headers;
                if (actual.Count == 0) errors.Add(new(1, "header", "The CSV is empty."));
                else
                {
                    if (!Columns.SequenceEqual(actual))
                        errors.Add(new(1, "header", $"Expected columns in this order: {string.Join(", ", Columns)}."));
                    else
                    {
                        while (await csv.ReadAsync(token))
                        {
                            token.ThrowIfCancellationRequested();
                            var row = checked((int)csv.LineNumber);
                            if (observations.Count >= 100_000)
                            {
                                errors.Add(new(row, "file", "A CSV can contain at most 100,000 observations."));
                                break;
                            }
                            var validId = long.TryParse(csv.GetString("id"), out var userId) && userId > 0;
                            var clickText = csv.GetString("click");
                            if (!validId) AddError(row, "id", "Expected a positive integer user ID.");
                            if (clickText is not ("0" or "1")) AddError(row, "click", "Expected 0 or 1.");
                            var ageText = Clean(csv.GetString("age"));
                            int? age = null;
                            if (ageText is not null)
                            {
                                if (decimal.TryParse(ageText, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                                    && value is >= 0 and <= 120 && decimal.Truncate(value) == value) age = (int)value;
                                else AddError(row, "age", "Expected a whole-number age between 0 and 120, or a missing value.");
                            }
                            observations.Add(new AdImpression
                            {
                                SourceRow = row, OriginalUserId = userId,
                                FullName = Clean(csv.GetString("full_name")), Age = age,
                                Gender = Clean(csv.GetString("gender")), DeviceType = Clean(csv.GetString("device_type")),
                                AdPosition = Clean(csv.GetString("ad_position")),
                                BrowsingHistory = Clean(csv.GetString("browsing_history")),
                                TimeOfDay = Clean(csv.GetString("time_of_day")), Click = clickText == "1"
                            });
                        }
                    }
                }
            }
            catch (CsvFormatException)
            {
                AddError(checked((int)csv.LineNumber), "csv", "Malformed CSV. Check quoting and the number of fields.");
            }
            if (observations.Count == 0 && errors.Count == 0)
                errors.Add(new(2, "file", "The CSV contains no observations."));

            var batch = new ImportBatch
            {
                FileName = Path.GetFileName(fileName), Sha256 = hash,
                Status = errors.Count == 0 ? "Imported" : "Rejected",
                RowCount = errors.Count == 0 ? observations.Count : 0,
                ClickCount = errors.Count == 0 ? observations.Count(x => x.Click) : 0,
                ErrorsJson = JsonSerializer.Serialize(errors)
            };
            // Validate the entire file before one atomic commit; partial imports are misleading.
            await using var transaction = await db.Database.BeginTransactionAsync(token);
            db.ImportBatches.Add(batch);
            if (errors.Count == 0)
            {
                foreach (var observation in observations) observation.ImportBatch = batch;
                db.AdImpressions.AddRange(observations);
            }
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            logger.LogInformation("CSV {Status}: {File}, {Rows} rows, {Clicks} clicks, {Errors} reported errors",
                batch.Status, batch.FileName, batch.RowCount, batch.ClickCount, errors.Count);
            return ToResult(batch, false);

            void AddError(int row, string column, string message)
            {
                if (errors.Count < 100) errors.Add(new(row, column, message));
            }
        }
        finally { ImportLock.Release(); }
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Equals("NaN", StringComparison.OrdinalIgnoreCase)
            || value.Equals("null", StringComparison.OrdinalIgnoreCase) ? null : value.Trim();

    public static ImportResult ToResult(ImportBatch batch, bool alreadyImported = false) =>
        new(batch.Id, batch.FileName, batch.Status, batch.RowCount, batch.ClickCount, alreadyImported,
            batch.ImportedAt, JsonSerializer.Deserialize<List<ImportError>>(batch.ErrorsJson) ?? []);
}
