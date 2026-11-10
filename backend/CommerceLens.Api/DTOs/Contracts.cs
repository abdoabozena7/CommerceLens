using System.ComponentModel.DataAnnotations;
using CommerceLens.Api.Models;

namespace CommerceLens.Api.DTOs;

public record ImportError(int Row, string Column, string Message);
public record ImportResult(int Id, string FileName, string Status, int RowCount, int ClickCount,
    bool AlreadyImported, DateTime ImportedAt, IReadOnlyList<ImportError> Errors);

public record ProductDto(int Id, string Slug, string Name, string Category, string Description,
    decimal Price, int Stock, string Visual, string Accent, bool Featured, string SourceType)
{
    public static ProductDto From(Product p) => new(p.Id, p.Slug, p.Name, p.Category,
        p.Description, p.PriceCents / 100m, p.Stock, p.Visual, p.Accent, p.Featured, p.SourceType);
}

public record CartItemRequest([Range(1, int.MaxValue)] int ProductId, [Range(1, 25)] int Quantity);
public record CheckoutRequest(
    [Required, MinLength(1), MaxLength(30)] List<CartItemRequest> Items,
    [Required, StringLength(64, MinimumLength = 16)] string RequestKey);
public record OrderItemDto(int ProductId, string Name, string Category, int Quantity, decimal UnitPrice);
public record OrderDto(int Id, DateTime CreatedAt, string SourceType, decimal Total, List<OrderItemDto> Items)
{
    public static OrderDto From(Order order) => new(order.Id, order.CreatedAt, order.SourceType,
        order.TotalCents / 100m, order.Items.Select(i => new OrderItemDto(
            i.ProductId, i.ProductName, i.Category, i.Quantity, i.UnitPriceCents / 100m)).ToList());
}

public record EventRequest([Range(1, int.MaxValue)] int ProductId, [Required] string EventType,
    [StringLength(160, MinimumLength = 16)] string? EventKey = null);
