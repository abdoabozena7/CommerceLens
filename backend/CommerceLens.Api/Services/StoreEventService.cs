using CommerceLens.Api.Data;
using CommerceLens.Api.DTOs;
using CommerceLens.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CommerceLens.Api.Services;

public class StoreEventService(CommerceDbContext db)
{
    private static readonly SemaphoreSlim EventLock = new(1, 1);

    public async Task TrackAsync(EventRequest request, CancellationToken token)
    {
        if (request.EventType is not ("ProductView" or "AdImpression" or "AdClick"))
            throw new ApiException(400, "EventType must be ProductView, AdImpression or AdClick.");
        if (request.EventType == "AdImpression" && string.IsNullOrWhiteSpace(request.EventKey))
            throw new ApiException(400, "Ad impressions require a page-visit event key to prevent duplicates.");
        if (request.EventKey is not null && string.IsNullOrWhiteSpace(request.EventKey))
            throw new ApiException(400, "An event key must not be blank.");

        await EventLock.WaitAsync(token);
        try
        {
            if (request.EventKey is not null)
            {
                var existing = await db.StoreEvents.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.EventKey == request.EventKey, token);
                if (existing is not null)
                {
                    if (existing.ProductId != request.ProductId || existing.EventType != request.EventType)
                        throw new ApiException(409, "This event key was already used for a different event.");
                    return;
                }
            }
            var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.ProductId, token)
                ?? throw new ApiException(404, "Product not found.");
            if (request.EventType is "AdImpression" or "AdClick" && !product.Featured)
                throw new ApiException(400, "Only products with a public advertising card can have ad events.");
            db.StoreEvents.Add(new StoreEvent
            {
                ProductId = product.Id, EventType = request.EventType, EventKey = request.EventKey
            });
            await db.SaveChangesAsync(token);
        }
        finally { EventLock.Release(); }
    }
}
