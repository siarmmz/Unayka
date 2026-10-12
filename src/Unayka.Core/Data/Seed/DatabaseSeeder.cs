using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Unayka.Core.Accounts;
using Unayka.Core.Catalog;
using Unayka.Core.Coupons;
using Unayka.Core.Locations;

namespace Unayka.Core.Data.Seed;

public static class DatabaseSeeder
{
    private const char FactSeparator = '|';
    private const int PercentageBasisPoints = 1000;
    private const long FixedCouponMinor = 5000;
    private const long FixedCouponMinSubtotalMinor = 30000;
    private const long MinorUnitsPerUnit = 100;
    private const int OnlySecondLocationStep = 5;
    private const int OnlyFirstLocationStep = 7;
    private const int OnlyFirstLocationOffset = 3;
    private const int FirstLocationSharePercent = 60;
    private const int PercentBase = 100;

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
        if (await database.Sections.AnyAsync())
        {
            return;
        }

        var locations = await SeedLocationsAsync(database);
        var sections = CatalogSeed.Sections
            .Select((seed, index) => new Section
            {
                Slug = seed.Slug,
                Name = seed.Name,
                Description = seed.Description,
                ImageUrl = UnsplashImage.Url(seed.ImageId),
                SortOrder = index,
            })
            .ToList();
        database.Sections.AddRange(sections);
        await database.SaveChangesAsync();

        var sectionIds = sections.ToDictionary(section => section.Slug, section => section.Id);
        var tags = CatalogSeed.Tags
            .Select((seed, index) => new Tag { SectionId = sectionIds[seed.SectionSlug], Slug = seed.Slug, Name = seed.Name, SortOrder = index })
            .ToList();
        database.Tags.AddRange(tags);
        var categories = CatalogSeed.Categories
            .Select((seed, index) => new Category
            {
                SectionId = sectionIds[seed.SectionSlug],
                Slug = seed.Slug,
                Name = seed.Name,
                Description = seed.Description,
                ImageUrl = UnsplashImage.Url(seed.ImageId),
                SortOrder = index,
            })
            .ToList();
        database.Categories.AddRange(categories);
        await database.SaveChangesAsync();

        var categoryIds = categories.ToDictionary(category => category.Slug, category => category.Id);
        var tagsBySlug = tags.ToDictionary(tag => tag.Slug);
        var products = CatalogSeed.Products.Select((seed, index) => CreateProduct(seed, index, categoryIds, tagsBySlug, locations, now));
        database.Products.AddRange(products);
        await database.SaveChangesAsync();
    }

    private static async Task<List<Location>> SeedLocationsAsync(UnaykaDbContext database)
    {
        var locations = LocationSeed.Locations
            .Select((seed, index) => new Location
            {
                Slug = seed.Slug,
                Name = seed.Name,
                City = seed.City,
                Address = seed.Address,
                OpeningHours = seed.OpeningHours,
                Latitude = seed.Latitude,
                Longitude = seed.Longitude,
                MapEmbedUrl = seed.MapEmbedUrl,
                SortOrder = index,
                Contacts = seed.Contacts
                    .Select((contact, position) => new LocationContact
                    {
                        Kind = contact.Kind,
                        Label = contact.Label,
                        Value = contact.Value,
                        SortOrder = position,
                    })
                    .ToList(),
            })
            .ToList();
        database.Locations.AddRange(locations);
        await database.SaveChangesAsync();
        return locations;
    }

    private static List<LocationStock> StockFor(SeedProduct seed, int index, IReadOnlyList<Location> locations)
    {
        if (seed.Kind == ProductKind.Digital)
        {
            return [];
        }

        var first = locations[0];
        var second = locations[1];
        if (index % OnlySecondLocationStep == OnlySecondLocationStep - 1)
        {
            return [new LocationStock { LocationId = second.Id, Quantity = seed.Stock }];
        }

        if (index % OnlyFirstLocationStep == OnlyFirstLocationOffset)
        {
            return [new LocationStock { LocationId = first.Id, Quantity = seed.Stock }];
        }

        var atFirst = seed.Stock * FirstLocationSharePercent / PercentBase;
        return
        [
            new LocationStock { LocationId = first.Id, Quantity = atFirst },
            new LocationStock { LocationId = second.Id, Quantity = seed.Stock - atFirst },
        ];
    }

    private static Product CreateProduct(
        SeedProduct seed,
        int index,
        Dictionary<string, int> categoryIds,
        Dictionary<string, Tag> tagsBySlug,
        IReadOnlyList<Location> locations,
        DateTimeOffset now) =>
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
            Kind = seed.Kind,
            IsFeatured = seed.IsFeatured,
            IsNew = seed.Badge == CatalogSeed.NewBadge,
            Stocks = StockFor(seed, index, locations),
            Tags = CatalogSeed.ProductTags.GetValueOrDefault(seed.Slug, []).Select(slug => tagsBySlug[slug]).ToList(),
            DigitalAssets = seed.Kind == ProductKind.Digital ? [DigitalSeedFiles.CreateEbookAsset()] : [],
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
