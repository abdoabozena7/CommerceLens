using System.Security.Cryptography;
using System.Text;
using CommerceLens.Api.Data;
using CommerceLens.Api.DTOs;
using CommerceLens.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CommerceLens.Api.Services;

public class OrderService(CommerceDbContext db)
{
    public async Task<OrderDto> CheckoutAsync(CheckoutRequest request, CancellationToken token = default)
    {
        if (request.Items is null || request.Items.Count == 0 || request.Items.Any(x => x is null))
            throw new ApiException(400, "A cart must contain at least one valid product line; null items are not allowed.");
        var items = request.Items.GroupBy(x => x.ProductId)
            .Select(g => new CartItemRequest(g.Key, g.Sum(x => x.Quantity))).OrderBy(x => x.ProductId).ToList();
        if (items.Any(x => x.Quantity > 25)) throw new ApiException(400, "Maximum quantity per product is 25.");
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join(";", items.Select(x => $"{x.ProductId}:{x.Quantity}")))));
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var existing = await db.Orders.Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.RequestKey == request.RequestKey, token);
        if (existing is not null)
        {
            if (existing.RequestHash != fingerprint) throw new ApiException(409, "This checkout key was used for a different cart.");
            return OrderDto.From(existing);
        }
        var ids = items.Select(x => x.ProductId).ToList();
        var products = await db.Products.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync(token);
        if (products.Count != items.Count) throw new ApiException(400, "One or more products do not exist.");
        var order = new Order { RequestKey = request.RequestKey, RequestHash = fingerprint };
        foreach (var item in items)
        {
            var product = products.Single(x => x.Id == item.ProductId);
            // The conditional database update prevents stock going negative on concurrent checkouts.
            var updated = await db.Products.Where(x => x.Id == item.ProductId && x.Stock >= item.Quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Stock, x => x.Stock - item.Quantity), token);
            if (updated != 1) throw new ApiException(409, $"{product.Name} has insufficient stock.");
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id, ProductName = product.Name, Category = product.Category,
                Quantity = item.Quantity, UnitPriceCents = product.PriceCents
            });
        }
        // Store integer cents so SQLite aggregation and checkout totals stay exact.
        order.TotalCents = order.Items.Sum(x => x.Quantity * x.UnitPriceCents);
        db.Orders.Add(order);
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return OrderDto.From(order);
    }
}
