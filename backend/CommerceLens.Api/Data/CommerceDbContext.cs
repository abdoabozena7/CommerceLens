using CommerceLens.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CommerceLens.Api.Data;

public class CommerceDbContext(DbContextOptions<CommerceDbContext> options) : DbContext(options)
{
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<AdImpression> AdImpressions => Set<AdImpression>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<StoreEvent> StoreEvents => Set<StoreEvent>();
    public DbSet<ScrapeRun> ScrapeRuns => Set<ScrapeRun>();
    public DbSet<ScrapedProduct> ScrapedProducts => Set<ScrapedProduct>();
    public DbSet<ModelEvaluation> ModelEvaluations => Set<ModelEvaluation>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        // SQLite drops DateTime.Kind; restore UTC so persisted timestamps retain JSON's Z marker.
        var utc = new ValueConverter<DateTime, DateTime>(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        foreach (var property in model.Model.GetEntityTypes().SelectMany(x => x.GetProperties())
            .Where(x => x.ClrType == typeof(DateTime) || x.ClrType == typeof(DateTime?)))
            property.SetValueConverter(utc);
        // A user ID can repeat: only the import identity and CSV row identify an observation.
        model.Entity<AdImpression>().HasIndex(x => new { x.ImportBatchId, x.SourceRow }).IsUnique();
        model.Entity<AdImpression>().HasIndex(x => x.OriginalUserId);
        model.Entity<ImportBatch>().HasIndex(x => x.Sha256).IsUnique();
        model.Entity<Product>().HasIndex(x => x.Slug).IsUnique();
        model.Entity<Order>().HasIndex(x => x.RequestKey).IsUnique();
        model.Entity<Order>().HasIndex(x => x.CreatedAt);
        model.Entity<StoreEvent>().HasIndex(x => x.CollectedAt);
        model.Entity<StoreEvent>().HasIndex(x => x.EventKey).IsUnique();
        model.Entity<ScrapedProduct>().HasIndex(x => new { x.ScrapeRunId, x.StorefrontProductId }).IsUnique();
    }
}

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CommerceDbContext>
{
    public CommerceDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CommerceDbContext>()
            .UseSqlite("Data Source=commerce-lens-design.db").Options);
}
