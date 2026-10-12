using Microsoft.EntityFrameworkCore;
using Unayka.Core.Audit;
using Unayka.Core.Catalog;
using Unayka.Core.Data;
using Unayka.Core.Locations;

namespace Unayka.Core.Administration;

public interface IAdminCatalog
{
    /// <summary>Lists the products of one subpage with an optional text search.</summary>
    Task<PagedResult<ProductRow>> ListProductsAsync(ProductListView view, string? search, int page, CancellationToken cancellationToken = default);

    /// <summary>Loads a product for editing, or null when it does not exist.</summary>
    Task<ProductForm?> GetProductAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Builds an empty product with one stock line per active location.</summary>
    Task<ProductForm> NewProductAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a product after validation and audits the change.</summary>
    Task<AdminResult> SaveProductAsync(int? actorUserId, ProductForm form, CancellationToken cancellationToken = default);

    /// <summary>Changes the status of a product (active, inactive or archived) and audits the change.</summary>
    Task<AdminResult> SetProductStatusAsync(int? actorUserId, int id, CatalogStatus status, CancellationToken cancellationToken = default);

    /// <summary>Deletes a product that no order references, or fails telling it can only be archived.</summary>
    Task<AdminResult> DeleteProductAsync(int? actorUserId, int id, CancellationToken cancellationToken = default);

    /// <summary>Stores the downloadable file of a digital product in private storage and audits the change.</summary>
    Task<AdminResult> SetDigitalFileAsync(int? actorUserId, int productId, DigitalFileUpload upload, CancellationToken cancellationToken = default);

    /// <summary>Removes the downloadable file of a digital product and audits the change.</summary>
    Task<AdminResult> RemoveDigitalFileAsync(int? actorUserId, int productId, CancellationToken cancellationToken = default);
}

/// <summary>File chosen for a digital product.</summary>
public sealed record DigitalFileUpload(string FileName, string ContentType, long SizeBytes, Stream Content);

public sealed class AdminCatalogService(IDbContextFactory<UnaykaDbContext> factory, IAuditLog audit, IDigitalFileStore files, TimeProvider time) : IAdminCatalog
{
    public const long MaxDigitalFileBytes = 100L * 1024 * 1024;

    private const string DefaultContentType = "application/octet-stream";
    private const string ProductNotFound = "Produto não encontrado.";

    public async Task<PagedResult<ProductRow>> ListProductsAsync(ProductListView view, string? search, int page, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var query = ApplyView(db.Products.AsNoTracking(), view);
        if (SearchPattern.Contains(search) is { } pattern)
        {
            query = query.Where(product => EF.Functions.Like(product.Name, pattern) || EF.Functions.Like(product.Slug, pattern));
        }

        var pageNumber = AdminPaging.NormalizePage(page);
        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(product => product.Name)
            .Skip((pageNumber - 1) * AdminPaging.DefaultPageSize)
            .Take(AdminPaging.DefaultPageSize)
            .Select(product => new ProductRow(
                product.Id,
                product.Name,
                product.Slug,
                product.Category!.Name,
                product.PriceMinor,
                product.CompareAtPriceMinor,
                product.Stocks.Sum(stock => stock.Quantity),
                product.Status,
                product.IsFeatured,
                product.IsNew,
                product.Kind,
                product.Media.OrderBy(media => media.SortOrder).Select(media => media.Url).FirstOrDefault(),
                !db.OrderItems.Any(item => item.ProductId == product.Id)))
            .ToListAsync(cancellationToken);
        return new PagedResult<ProductRow>(items, pageNumber, AdminPaging.DefaultPageSize, total);
    }

    public async Task<ProductForm?> GetProductAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var product = await db.Products.AsNoTracking()
            .Include(candidate => candidate.Category)
            .Include(candidate => candidate.Media)
            .Include(candidate => candidate.Facts)
            .Include(candidate => candidate.Tags)
            .Include(candidate => candidate.Stocks)
            .Include(candidate => candidate.DigitalAssets)
            .AsSplitQuery()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        return product is null ? null : ToForm(product, await ActiveLocationsAsync(db, cancellationToken));
    }

    public async Task<ProductForm> NewProductAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var locations = await ActiveLocationsAsync(db, cancellationToken);
        return new ProductForm { Stocks = locations.Select(location => new LocationStockForm { LocationId = location.Id, LocationName = location.Name }).ToList() };
    }

    public async Task<AdminResult> SaveProductAsync(int? actorUserId, ProductForm form, CancellationToken cancellationToken = default)
    {
        form.Slug = form.Slug.Trim().ToLowerInvariant();
        var errors = CatalogValidator.ValidateProduct(form);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var category = form.CategoryId > 0 ? await db.Categories.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == form.CategoryId, cancellationToken) : null;
        if (form.CategoryId > 0 && category is null)
        {
            errors.Add("A categoria escolhida não existe.");
        }

        if (await db.Products.AnyAsync(product => product.Slug == form.Slug && product.Id != (form.Id ?? 0), cancellationToken))
        {
            errors.Add("Já existe um produto com este endereço (slug).");
        }

        var locationIds = form.Stocks.Select(stock => stock.LocationId).Distinct().ToList();
        if (await db.Locations.CountAsync(location => locationIds.Contains(location.Id), cancellationToken) != locationIds.Count)
        {
            errors.Add("Uma das lojas indicadas no stock não existe.");
        }

        if (errors.Count > 0)
        {
            return AdminResult.Fail(errors);
        }

        var product = form.Id is { } id
            ? await db.Products
                .Include(candidate => candidate.Media)
                .Include(candidate => candidate.Facts)
                .Include(candidate => candidate.Stocks)
                .Include(candidate => candidate.Tags)
                .AsSplitQuery()
                .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            : null;
        if (form.Id is not null && product is null)
        {
            return AdminResult.Fail(ProductNotFound);
        }

        var isNew = product is null;
        if (product is null)
        {
            product = new Product { CreatedAt = time.GetUtcNow() };
            db.Products.Add(product);
        }

        var tags = await db.Tags.Where(tag => form.TagIds.Contains(tag.Id) && tag.SectionId == category!.SectionId).ToListAsync(cancellationToken);
        Apply(form, product, tags);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteEntityAsync(
            actorUserId,
            isNew ? AdminAuditActions.ProductCreated : AdminAuditActions.ProductUpdated,
            AdminEntityTypes.Product,
            product.Id,
            isNew ? $"Produto {product.Name} criado." : $"Produto {product.Name} atualizado.",
            cancellationToken);
        return AdminResult.Ok(product.Id);
    }

    public async Task<AdminResult> SetProductStatusAsync(int? actorUserId, int id, CatalogStatus status, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var product = await db.Products.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (product is null)
        {
            return AdminResult.Fail(ProductNotFound);
        }

        product.Status = status;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteEntityAsync(
            actorUserId,
            AdminAuditActions.ProductStatusChanged,
            AdminEntityTypes.Product,
            product.Id,
            $"Produto {product.Name}: {StatusNames.For(status)}.",
            cancellationToken);
        return AdminResult.Ok(product.Id);
    }

    public async Task<AdminResult> DeleteProductAsync(int? actorUserId, int id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var product = await db.Products.Include(candidate => candidate.DigitalAssets).FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (product is null)
        {
            return AdminResult.Fail(ProductNotFound);
        }

        if (await db.OrderItems.AnyAsync(item => item.ProductId == id, cancellationToken))
        {
            return AdminResult.Fail("Este produto tem encomendas e só pode ser arquivado.");
        }

        var storageKeys = product.DigitalAssets.Select(asset => asset.StorageKey).ToList();
        await db.CartItems.Where(item => item.ProductId == id).ExecuteDeleteAsync(cancellationToken);
        db.Products.Remove(product);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var storageKey in storageKeys)
        {
            await files.DeleteAsync(storageKey, cancellationToken);
        }

        await audit.WriteEntityAsync(actorUserId, AdminAuditActions.ProductDeleted, AdminEntityTypes.Product, id, $"Produto {product.Name} eliminado.", cancellationToken);
        return AdminResult.Ok(id);
    }

    public async Task<AdminResult> SetDigitalFileAsync(int? actorUserId, int productId, DigitalFileUpload upload, CancellationToken cancellationToken = default)
    {
        var fileName = Path.GetFileName(upload.FileName.Trim());
        var errors = new List<string>();
        if (fileName.Length == 0)
        {
            errors.Add("Escolha um ficheiro.");
        }

        if (upload.SizeBytes <= 0 || upload.SizeBytes > MaxDigitalFileBytes)
        {
            errors.Add("O ficheiro deve ter entre 1 byte e 100 MB.");
        }

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var product = await db.Products.Include(candidate => candidate.DigitalAssets).FirstOrDefaultAsync(candidate => candidate.Id == productId, cancellationToken);
        if (product is null)
        {
            return AdminResult.Fail(ProductNotFound);
        }

        if (product.Kind != ProductKind.Digital)
        {
            errors.Add("Só os produtos digitais têm ficheiro para descarregar.");
        }

        if (errors.Count > 0)
        {
            return AdminResult.Fail(errors);
        }

        var storageKey = $"{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
        await files.SaveAsync(storageKey, upload.Content, cancellationToken);
        var replacedKeys = product.DigitalAssets.Select(asset => asset.StorageKey).ToList();
        db.DigitalAssets.RemoveRange(product.DigitalAssets);
        product.DigitalAssets.Add(new DigitalAsset
        {
            StorageKey = storageKey,
            FileName = fileName,
            ContentType = string.IsNullOrWhiteSpace(upload.ContentType) ? DefaultContentType : upload.ContentType,
            SizeBytes = upload.SizeBytes,
        });
        await db.SaveChangesAsync(cancellationToken);
        foreach (var replacedKey in replacedKeys)
        {
            await files.DeleteAsync(replacedKey, cancellationToken);
        }

        await audit.WriteEntityAsync(actorUserId, AdminAuditActions.ProductFileChanged, AdminEntityTypes.Product, product.Id, $"Ficheiro de {product.Name} atualizado.", cancellationToken);
        return AdminResult.Ok(product.Id);
    }

    public async Task<AdminResult> RemoveDigitalFileAsync(int? actorUserId, int productId, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var product = await db.Products.Include(candidate => candidate.DigitalAssets).FirstOrDefaultAsync(candidate => candidate.Id == productId, cancellationToken);
        if (product is null)
        {
            return AdminResult.Fail(ProductNotFound);
        }

        var storageKeys = product.DigitalAssets.Select(asset => asset.StorageKey).ToList();
        db.DigitalAssets.RemoveRange(product.DigitalAssets);
        await db.SaveChangesAsync(cancellationToken);
        foreach (var storageKey in storageKeys)
        {
            await files.DeleteAsync(storageKey, cancellationToken);
        }

        await audit.WriteEntityAsync(actorUserId, AdminAuditActions.ProductFileChanged, AdminEntityTypes.Product, product.Id, $"Ficheiro de {product.Name} removido.", cancellationToken);
        return AdminResult.Ok(product.Id);
    }

    private static IQueryable<Product> ApplyView(IQueryable<Product> products, ProductListView view) => view switch
    {
        ProductListView.New => products.Where(product => product.IsNew && product.Status != CatalogStatus.Archived),
        ProductListView.Featured => products.Where(product => product.IsFeatured && product.Status != CatalogStatus.Archived),
        ProductListView.Inactive => products.Where(product => product.Status == CatalogStatus.Inactive),
        ProductListView.Archived => products.Where(product => product.Status == CatalogStatus.Archived),
        _ => products.Where(product => product.Status != CatalogStatus.Archived),
    };

    private static void Apply(ProductForm form, Product product, List<Tag> tags)
    {
        product.Name = form.Name.Trim();
        product.Slug = form.Slug;
        product.CategoryId = form.CategoryId;
        product.ShortDescription = form.ShortDescription.Trim();
        product.Description = form.Description.Trim();
        product.PriceMinor = MoneyInput.ToMinor(form.Price);
        product.CompareAtPriceMinor = form.CompareAtPrice is { } compare ? MoneyInput.ToMinor(compare) : null;
        product.Badge = string.IsNullOrWhiteSpace(form.Badge) ? null : form.Badge.Trim();
        product.IsFeatured = form.IsFeatured;
        product.IsNew = form.IsNew;
        product.Kind = form.Kind;
        product.Status = form.Status;
        product.Tags.Clear();
        product.Tags.AddRange(tags);
        ApplyStocks(product, form);

        product.Media.Clear();
        product.Media.AddRange(form.Media.Select(media => new ProductMedia
        {
            Kind = media.Kind,
            Url = media.Url.Trim(),
            Alt = string.IsNullOrWhiteSpace(media.Alt) ? product.Name : media.Alt.Trim(),
            SortOrder = media.SortOrder,
        }));
        product.Facts.Clear();
        product.Facts.AddRange(form.Facts
            .Where(fact => !string.IsNullOrWhiteSpace(fact.Label))
            .Select(fact => new ProductFact { Group = fact.Group.Trim(), Label = fact.Label.Trim(), Value = fact.Value.Trim(), SortOrder = fact.SortOrder }));
    }

    private static void ApplyStocks(Product product, ProductForm form)
    {
        if (product.Kind == ProductKind.Digital)
        {
            return;
        }

        foreach (var line in form.Stocks)
        {
            var stock = product.Stocks.FirstOrDefault(candidate => candidate.LocationId == line.LocationId);
            if (stock is null)
            {
                product.Stocks.Add(new LocationStock { LocationId = line.LocationId, Quantity = line.Quantity });
                continue;
            }

            stock.Quantity = line.Quantity;
        }
    }

    private static async Task<List<Location>> ActiveLocationsAsync(UnaykaDbContext db, CancellationToken cancellationToken) =>
        await db.Locations.AsNoTracking()
            .Where(location => location.Status == CatalogStatus.Active)
            .OrderBy(location => location.SortOrder)
            .ToListAsync(cancellationToken);

    private static ProductForm ToForm(Product product, List<Location> activeLocations)
    {
        var asset = product.DigitalAssets.FirstOrDefault();
        return new ProductForm
        {
            Id = product.Id,
            Name = product.Name,
            Slug = product.Slug,
            SectionId = product.Category!.SectionId,
            CategoryId = product.CategoryId,
            ShortDescription = product.ShortDescription,
            Description = product.Description,
            Price = MoneyInput.ToAmount(product.PriceMinor),
            CompareAtPrice = product.CompareAtPriceMinor is { } compare ? MoneyInput.ToAmount(compare) : null,
            Badge = product.Badge,
            Kind = product.Kind,
            IsFeatured = product.IsFeatured,
            IsNew = product.IsNew,
            Status = product.Status,
            TagIds = product.Tags.Select(tag => tag.Id).ToList(),
            Stocks = activeLocations.Select(location => new LocationStockForm
            {
                LocationId = location.Id,
                LocationName = location.Name,
                Quantity = product.Stocks.FirstOrDefault(stock => stock.LocationId == location.Id)?.Quantity ?? 0,
            }).ToList(),
            DigitalFileName = asset?.FileName,
            DigitalFileSizeBytes = asset?.SizeBytes,
            Media = product.Media.OrderBy(media => media.SortOrder)
                .Select(media => new ProductMediaForm { Kind = media.Kind, Url = media.Url, Alt = media.Alt, SortOrder = media.SortOrder }).ToList(),
            Facts = product.Facts.OrderBy(fact => fact.SortOrder)
                .Select(fact => new ProductFactForm { Group = fact.Group, Label = fact.Label, Value = fact.Value, SortOrder = fact.SortOrder }).ToList(),
        };
    }
}
