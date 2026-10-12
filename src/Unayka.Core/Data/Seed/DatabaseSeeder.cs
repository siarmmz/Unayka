using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Unayka.Core.Accounts;
using Unayka.Core.Catalog;
using Unayka.Core.Coupons;

namespace Unayka.Core.Data.Seed;

public static class DatabaseSeeder
{
    private const char FactSeparator = '|';
    private const int PercentageBasisPoints = 1000;
    private const long FixedCouponMinor = 5000;
    private const long FixedCouponMinSubtotalMinor = 30000;
    private const long MinorUnitsPerUnit = 100;

    private static readonly string[] MediaLabels = ["preparado", "embalagem", "ingredientes", "galeria"];

    public static async Task SeedAsync(UnaykaDbContext database, SeedOptions options, TimeProvider time)
    {
        var now = time.GetUtcNow();
        await SeedCatalogAsync(database, now);
        await SeedCouponsAsync(database, now);
        await SeedUsersAsync(database, options, now);
        if (options.IncludeDemoData)
        {
            await DemoOrdersSeed.SeedAsync(database, now);
        }
    }

    private static async Task SeedCatalogAsync(UnaykaDbContext database, DateTimeOffset now)
    {
        if (await database.Categories.AnyAsync())
        {
            return;
        }

        var categories = CatalogSeed.Categories
            .Select((seed, index) => new Category
            {
                Slug = seed.Slug,
                Name = seed.Name,
                Description = seed.Description,
                Kind = seed.Kind,
                ImageUrl = UnsplashImage.Url(seed.ImageId),
                SortOrder = index,
            })
            .ToList();
        database.Categories.AddRange(categories);
        await database.SaveChangesAsync();

        var categoryIds = categories.ToDictionary(category => category.Slug, category => category.Id);
        var products = CatalogSeed.Products.Select((seed, index) => CreateProduct(seed, index, categoryIds, now));
        database.Products.AddRange(products);
        await database.SaveChangesAsync();
    }

    private static Product CreateProduct(
        SeedProduct seed, int index, Dictionary<string, int> categoryIds, DateTimeOffset now) =>
        new()
        {
            Slug = seed.Slug,
            Name = seed.Name,
            ShortDescription = seed.ShortDescription,
            Description = seed.Description,
            PriceMinor = seed.PriceMzn * MinorUnitsPerUnit,
            CompareAtPriceMinor = seed.CompareAtPriceMzn * MinorUnitsPerUnit,
            CategoryId = categoryIds[seed.CategorySlug],
            Badge = seed.Badge,
            IsFeatured = seed.IsFeatured,
            StockQuantity = seed.Stock,
            SalesCount = seed.Sales,
            CreatedAt = now.AddDays(-(CatalogSeed.Products.Count - index)),
            Media = seed.PhotoIds
                .Select((photoId, position) => new ProductMedia
                {
                    Kind = (ProductMediaKind)position,
                    Url = UnsplashImage.Url(photoId),
                    Alt = $"{seed.Name}, {MediaLabels[position]}",
                    SortOrder = position,
                })
                .ToList(),
            Facts = seed.Facts.Select(CreateFact).ToList(),
        };

    private static ProductFact CreateFact(string line, int position)
    {
        var parts = line.Split(FactSeparator);
        return new ProductFact { Group = parts[0], Label = parts[1], Value = parts[2], SortOrder = position };
    }

    private static async Task SeedCouponsAsync(UnaykaDbContext database, DateTimeOffset now)
    {
        var coupons = new[]
        {
            new Coupon
            {
                Code = "BEMVINDO10",
                Description = "10 por cento de desconto na primeira compra",
                Kind = CouponKind.Percentage,
                Value = PercentageBasisPoints,
                CreatedAt = now,
            },
            new Coupon
            {
                Code = "CAFE50",
                Description = "50 MZN de desconto em compras acima de 300 MZN",
                Kind = CouponKind.Fixed,
                Value = FixedCouponMinor,
                MinSubtotalMinor = FixedCouponMinSubtotalMinor,
                CreatedAt = now,
            },
            new Coupon
            {
                Code = "NATAL2025",
                Description = "Campanha de Natal terminada",
                Kind = CouponKind.Percentage,
                Value = 1500,
                StartsAt = new DateTimeOffset(2025, 12, 1, 0, 0, 0, TimeSpan.Zero),
                EndsAt = new DateTimeOffset(2025, 12, 31, 23, 59, 59, TimeSpan.Zero),
                CreatedAt = now,
            },
        };

        var existing = await database.Coupons.Select(coupon => coupon.Code).ToListAsync();
        database.Coupons.AddRange(coupons.Where(coupon => !existing.Contains(coupon.Code)));
        await database.SaveChangesAsync();
    }

    private static async Task SeedUsersAsync(UnaykaDbContext database, SeedOptions options, DateTimeOffset now)
    {
        var hasher = new PasswordHasher<User>();
        var users = new List<User>();
        if (options.SeedStaff)
        {
            users.Add(CreateStaff("Administração Unayka", options.AdminEmail, UserRole.Administrator));
            users.Add(CreateStaff("Gerente Unayka", "gerente@unayka.co.mz", UserRole.Manager));
            users.Add(CreateStaff("Caixa Unayka", "caixa@unayka.co.mz", UserRole.Cashier));
        }

        if (options.IncludeDemoData)
        {
            users.AddRange(DemoOrdersSeed.Customers.Select(customer => new User
            {
                Name = customer.Name,
                Phone = customer.Phone,
                Role = UserRole.Customer,
                CreatedAt = now.AddDays(-30),
            }));
        }

        var emails = await database.Users.Where(user => user.Email != null).Select(user => user.Email!).ToListAsync();
        var phones = await database.Users.Where(user => user.Phone != null).Select(user => user.Phone!).ToListAsync();
        var missing = users.Where(user =>
            (user.Email is null || !emails.Contains(user.Email)) && (user.Phone is null || !phones.Contains(user.Phone)));
        database.Users.AddRange(missing);
        await database.SaveChangesAsync();
        return;

        User CreateStaff(string name, string email, UserRole role)
        {
            var user = new User { Name = name, Email = email, Role = role, CreatedAt = now };
            user.PasswordHash = hasher.HashPassword(user, options.StaffPassword);
            return user;
        }
    }
}
