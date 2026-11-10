using CommerceLens.Api.Data;
using CommerceLens.Api.DTOs;
using CommerceLens.Api.Models;
using CommerceLens.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CommerceLens.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(CommerceDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProductDto>>> List(string? search, string? category, string sort = "name")
    {
        var query = db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search) || x.Description.Contains(search));
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(x => x.Category == category);
        query = sort switch
        {
            "priceAsc" => query.OrderBy(x => x.PriceCents),
            "priceDesc" => query.OrderByDescending(x => x.PriceCents),
            "name" => query.OrderBy(x => x.Name),
            _ => throw new ApiException(400, "Unknown product sort.")
        };
        return (await query.ToListAsync()).Select(ProductDto.From).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDto>> Detail(int id)
    {
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        return product is null ? NotFound() : ProductDto.From(product);
    }
}

[ApiController]
[Route("api/orders")]
public class OrdersController(OrderService orders, CommerceDbContext db) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderDto>> Detail(int id, CancellationToken token)
    {
        var order = await db.Orders.AsNoTracking().Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id, token);
        return order is null ? NotFound() : OrderDto.From(order);
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> Checkout(CheckoutRequest request, CancellationToken token)
    {
        var order = await orders.CheckoutAsync(request, token);
        return Created($"/api/orders/{order.Id}", order);
    }
}

[ApiController]
[Route("api/events")]
public class EventsController(StoreEventService events) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Track(EventRequest request, CancellationToken token)
    {
        await events.TrackAsync(request, token);
        return NoContent();
    }
}
