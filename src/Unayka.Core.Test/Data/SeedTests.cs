using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Unayka.Core.Accounts;
using Unayka.Core.Data.Seed;

namespace Unayka.Core.Test.Data;

public sealed class SeedTests : IDisposable
{
    private const string StaffPassword = "unayka2026";

    private readonly TestDatabase _database = new();
    private readonly SeedOptions _options = new(StaffPassword, "admin@unayka.co.mz", true);

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Seeding_twice_creates_the_same_rows_once()
    {
        await DatabaseSeeder.SeedAsync(_database.Context, _options, TimeProvider.System);
        var first = await CountRowsAsync();

        await DatabaseSeeder.SeedAsync(_database.Context, _options, TimeProvider.System);

        Assert.Equal(first, await CountRowsAsync());
    }

    [Fact]
    public async Task Catalog_has_categories_products_media_and_facts()
    {
        await DatabaseSeeder.SeedAsync(_database.Context, _options, TimeProvider.System);

        var products = await _database.Context.Products.Include(product => product.Media).Include(product => product.Facts).ToListAsync();
        Assert.Equal(7, await _database.Context.Categories.CountAsync());
        Assert.Equal(28, products.Count);
        Assert.All(products, product =>
        {
            Assert.InRange(product.Media.Count, 2, 4);
            Assert.NotEmpty(product.Facts);
            Assert.All(product.Media, media => Assert.StartsWith("https://images.unsplash.com/photo-", media.Url, StringComparison.Ordinal));
        });
    }

    [Fact]
    public async Task Products_can_be_ordered_by_creation_date_on_sqlite()
    {
        await DatabaseSeeder.SeedAsync(_database.Context, _options, TimeProvider.System);

        var newest = await _database.Context.Products.OrderByDescending(product => product.CreatedAt).Select(product => product.Slug).FirstAsync();

        Assert.Equal("cha-de-capim-principe", newest);
    }

    [Fact]
    public async Task Staff_are_seeded_with_a_verifiable_password()
    {
        await DatabaseSeeder.SeedAsync(_database.Context, _options, TimeProvider.System);

        var admin = await _database.Context.Users.SingleAsync(user => user.Email == "admin@unayka.co.mz");
        var result = new PasswordHasher<User>().VerifyHashedPassword(admin, admin.PasswordHash!, StaffPassword);
        Assert.Equal(UserRole.Administrator, admin.Role);
        Assert.NotEqual(PasswordVerificationResult.Failed, result);
        Assert.True(await _database.Context.Users.AnyAsync(user => user.Role == UserRole.Manager));
        Assert.True(await _database.Context.Users.AnyAsync(user => user.Role == UserRole.Cashier));
    }

    [Fact]
    public async Task Staff_are_not_seeded_without_a_password()
    {
        await DatabaseSeeder.SeedAsync(_database.Context, new SeedOptions(string.Empty, "admin@unayka.co.mz", false), TimeProvider.System);

        Assert.False(await _database.Context.Users.AnyAsync());
        Assert.False(await _database.Context.Orders.AnyAsync());
        Assert.Equal(28, await _database.Context.Products.CountAsync());
    }

    [Fact]
    public async Task Demo_orders_cover_different_payment_statuses()
    {
        await DatabaseSeeder.SeedAsync(_database.Context, _options, TimeProvider.System);

        var statuses = await _database.Context.Payments.Select(payment => payment.Status).Distinct().CountAsync();
        Assert.Equal(20, await _database.Context.Orders.CountAsync());
        Assert.Equal(4, statuses);
        Assert.Equal(3, await _database.Context.Coupons.CountAsync());
    }

    private async Task<int[]> CountRowsAsync() =>
    [
        await _database.Context.Categories.CountAsync(),
        await _database.Context.Products.CountAsync(),
        await _database.Context.ProductMedia.CountAsync(),
        await _database.Context.ProductFacts.CountAsync(),
        await _database.Context.Users.CountAsync(),
        await _database.Context.Coupons.CountAsync(),
        await _database.Context.Orders.CountAsync(),
        await _database.Context.Payments.CountAsync(),
    ];
}
