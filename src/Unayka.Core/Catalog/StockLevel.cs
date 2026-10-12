namespace Unayka.Core.Catalog;

public enum StockLevel
{
    InStock,
    LowStock,
    OutOfStock,
}

public static class StockLevels
{
    public const int LowStockThreshold = 5;

    public static StockLevel For(int stockQuantity) => stockQuantity switch
    {
        <= 0 => StockLevel.OutOfStock,
        <= LowStockThreshold => StockLevel.LowStock,
        _ => StockLevel.InStock,
    };
}
