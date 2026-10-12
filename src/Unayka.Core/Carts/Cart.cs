namespace Unayka.Core.Carts;

/// <summary>Server side cart identified by the unayka_cart cookie.</summary>
public sealed class Cart
{
    public Guid Id { get; set; }
    public int? UserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<CartItem> Items { get; set; } = [];
}
