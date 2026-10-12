using Microsoft.EntityFrameworkCore;
using Unayka.Core.Audit;
using Unayka.Core.Catalog;
using Unayka.Core.Data;

namespace Unayka.Core.Orders.Downloads;

public sealed class DigitalDownloads(UnaykaDbContext database, IDigitalFileStore files, IAuditLog audit, TimeProvider time) : IDigitalDownloads
{
    public async Task<DownloadResult> OpenAsync(int orderItemId, int? userId, CancellationToken cancellationToken = default)
    {
        if (userId is null)
        {
            return Refuse(DownloadOutcome.NotSignedIn);
        }

        var item = await database.OrderItems.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == orderItemId && candidate.Kind == ProductKind.Digital, cancellationToken);
        var order = item is null ? null : await database.Orders.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == item.OrderId, cancellationToken);
        if (item is null || order is null || order.UserId != userId)
        {
            return Refuse(DownloadOutcome.NotFound);
        }

        if (!OrderPayments.IsPaid(order.Status))
        {
            return Refuse(DownloadOutcome.NotPaid);
        }

        var asset = await database.DigitalAssets.AsNoTracking()
            .OrderBy(candidate => candidate.Id)
            .FirstOrDefaultAsync(candidate => candidate.ProductId == item.ProductId, cancellationToken);
        var content = asset is null ? null : files.Open(asset.StorageKey);
        if (asset is null || content is null)
        {
            return Refuse(DownloadOutcome.FileMissing);
        }

        await CountAsync(item.Id, userId.Value, cancellationToken);
        await audit.WriteAsync(userId, "download.served", "OrderItem", item.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), $"Digital download of order {order.Number}", cancellationToken);
        return new DownloadResult(DownloadOutcome.Granted, new DigitalFile(content, asset.FileName, asset.ContentType));
    }

    private static DownloadResult Refuse(DownloadOutcome outcome) => new(outcome, null);

    private async Task CountAsync(int orderItemId, int userId, CancellationToken cancellationToken)
    {
        var grant = await database.DigitalDownloadGrants.FirstOrDefaultAsync(
            candidate => candidate.OrderItemId == orderItemId && candidate.UserId == userId, cancellationToken);
        if (grant is null)
        {
            grant = new DigitalDownloadGrant { OrderItemId = orderItemId, UserId = userId, CreatedAt = time.GetUtcNow() };
            database.DigitalDownloadGrants.Add(grant);
        }

        grant.DownloadCount++;
        await database.SaveChangesAsync(cancellationToken);
    }
}
