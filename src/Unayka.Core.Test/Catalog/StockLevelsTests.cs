using Unayka.Core.Catalog;

namespace Unayka.Core.Test.Catalog;

public sealed class StockLevelsTests
{
    [Theory]
    [InlineData(-1, StockLevel.OutOfStock)]
    [InlineData(0, StockLevel.OutOfStock)]
    [InlineData(1, StockLevel.LowStock)]
    [InlineData(5, StockLevel.LowStock)]
    [InlineData(6, StockLevel.InStock)]
    public void Stock_level_follows_quantity(int quantity, StockLevel expected) =>
        Assert.Equal(expected, StockLevels.For(quantity));
}
