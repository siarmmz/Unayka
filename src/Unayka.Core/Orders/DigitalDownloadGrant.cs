namespace Unayka.Core.Orders;

/// <summary>Right of a buyer to download a digital line, with a download counter for audit only.</summary>
public sealed class DigitalDownloadGrant
{
    public int Id { get; set; }
    public int OrderItemId { get; set; }
    public int UserId { get; set; }
    public int DownloadCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
