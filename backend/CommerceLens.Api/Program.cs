using CommerceLens.Api.Data;
using CommerceLens.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
var storefrontUrl = builder.Configuration["CommerceLens:StorefrontUrl"] ?? "http://127.0.0.1:5173/store";
if (!Uri.TryCreate(storefrontUrl, UriKind.Absolute, out var storefront)
    || storefront.Scheme != "http" || storefront.Host is not ("localhost" or "127.0.0.1") || storefront.UserInfo.Length > 0)
    throw new InvalidOperationException("CommerceLens:StorefrontUrl must point to the local HTTP storefront, without credentials.");
var origins = new[] { "http://127.0.0.1:5173", "http://localhost:5173", "http://127.0.0.1:4173", "http://localhost:4173",
    storefront.GetLeftPart(UriPartial.Authority) }.Distinct().ToArray();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(dataDirectory);
var connection = builder.Configuration.GetConnectionString("CommerceLens")
    ?? $"Data Source={Path.Combine(dataDirectory, "commerce-lens.db")}";
builder.Services.AddDbContext<CommerceDbContext>(options => options.UseSqlite(connection));
builder.Services.AddScoped<CsvImportService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<StorefrontScraperService>();
builder.Services.AddScoped<StoreEventService>();
builder.Services.AddSingleton<ClickPredictionService>();

var app = builder.Build();
app.UseExceptionHandler();
app.UseCors();
// This is a local learning app, not an authenticated public analytics service.
app.Use(async (context, next) =>
{
    var origin = context.Request.Headers.Origin.ToString();
    if (context.Request.Method is not ("GET" or "HEAD" or "OPTIONS")
        && origin.Length > 0 && !origins.Contains(origin))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new { title = "This origin is not allowed to change local data." });
        return;
    }
    await next();
});
app.MapGet("/api/health", () => Results.Ok(new { status = "healthy", application = "CommerceLens" }));
app.MapControllers();
await DatabaseInitializer.InitializeAsync(app);
await app.Services.GetRequiredService<ClickPredictionService>().InitializeAsync();
app.Run();

public partial class Program;
