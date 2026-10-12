using Microsoft.EntityFrameworkCore;
using Unayka.Core.Audit;
using Unayka.Core.Catalog;
using Unayka.Core.Data;
using Unayka.Core.Locations;
using Unayka.Core.Presentation;

namespace Unayka.Core.Administration;

/// <summary>Subpage of the product list.</summary>
public enum ProductListView
{
    All,
    New,
    Featured,
    Inactive,
    Archived,
}

/// <summary>Row of the product list. CanDelete is false when an order references the product.</summary>
public sealed record ProductRow(
    int Id,
    string Name,
    string Slug,
    string CategoryName,
    long PriceMinor,
    long? CompareAtPriceMinor,
    int StockQuantity,
    CatalogStatus Status,
    bool IsFeatured,
    bool IsNew,
    ProductKind Kind,
    string? ImageUrl,
    bool CanDelete);

/// <summary>Category choice for selects, with its section.</summary>
public sealed record CategoryOption(int Id, string Name, int SectionId);

/// <summary>Section choice for selects.</summary>
public sealed record SectionOption(int Id, string Name);

/// <summary>Tag choice for selects, with its section.</summary>
public sealed record TagOption(int Id, string Name, int SectionId);

/// <summary>Editable image row of a product.</summary>
public sealed class ProductMediaForm
{
    public ProductMediaKind Kind { get; set; } = ProductMediaKind.Gallery;
    public string Url { get; set; } = string.Empty;
    public string Alt { get; set; } = string.Empty;
    public string? Credit { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Editable information row of a product.</summary>
public sealed class ProductFactForm
{
    public string Group { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

/// <summary>Editable stock quantity of a product at one location.</summary>
public sealed class LocationStockForm
{
    public int LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

/// <summary>Editable product. Prices are entered in MZN. SectionId only narrows the category and tag choices.</summary>
public sealed class ProductForm
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int SectionId { get; set; }
    public int CategoryId { get; set; }
    public string ShortDescription { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public string? Badge { get; set; }
    public ProductKind Kind { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsNew { get; set; }
    public CatalogStatus Status { get; set; } = CatalogStatus.Active;
    public List<int> TagIds { get; set; } = [];
    public List<LocationStockForm> Stocks { get; set; } = [];
    public string? DigitalFileName { get; set; }
    public long? DigitalFileSizeBytes { get; set; }
    public List<ProductMediaForm> Media { get; set; } = [];
    public List<ProductFactForm> Facts { get; set; } = [];
}

public static class CatalogValidator
{
    public static List<string> ValidateProduct(ProductForm form)
    {
        var errors = new List<string>();
        AddIf(errors, string.IsNullOrWhiteSpace(form.Name), "Indique o nome do produto.");
        AddIf(errors, !Slugs.IsValid(form.Slug), SlugMessage);
        AddIf(errors, form.CategoryId <= 0, "Escolha uma categoria.");
        AddIf(errors, form.Price <= 0, "O preço deve ser superior a zero.");
        AddIf(errors, form.CompareAtPrice is { } compare && compare <= form.Price, "O preço anterior deve ser superior ao preço atual.");
        AddIf(errors, form.Stocks.Any(stock => stock.Quantity < 0), "O stock não pode ser negativo.");
        AddIf(errors, form.Media.Any(media => !IsUsableImageUrl(media.Url)), "Cada imagem precisa de um endereço válido.");
        return errors;
    }

    public static List<string> ValidateSection(SectionForm form)
    {
        var errors = new List<string>();
        AddIf(errors, string.IsNullOrWhiteSpace(form.Name), "Indique o nome da secção.");
        AddIf(errors, !Slugs.IsValid(form.Slug), SlugMessage);
        AddIf(errors, !string.IsNullOrWhiteSpace(form.ImageUrl) && !IsUsableImageUrl(form.ImageUrl), ImageMessage);
        return errors;
    }

    public static List<string> ValidateCategory(CategoryForm form)
    {
        var errors = new List<string>();
        AddIf(errors, string.IsNullOrWhiteSpace(form.Name), "Indique o nome da categoria.");
        AddIf(errors, form.SectionId <= 0, "Escolha uma secção.");
        AddIf(errors, !Slugs.IsValid(form.Slug), SlugMessage);
        AddIf(errors, !string.IsNullOrWhiteSpace(form.ImageUrl) && !IsUsableImageUrl(form.ImageUrl), ImageMessage);
        return errors;
    }

    public static List<string> ValidateTag(TagForm form)
    {
        var errors = new List<string>();
        AddIf(errors, string.IsNullOrWhiteSpace(form.Name), "Indique o nome da etiqueta.");
        AddIf(errors, form.SectionId <= 0, "Escolha uma secção.");
        AddIf(errors, !Slugs.IsValid(form.Slug), SlugMessage);
        return errors;
    }

    private const string SlugMessage = "O endereço (slug) só pode ter letras minúsculas, números e hífens.";
    private const string ImageMessage = "O endereço da imagem não é válido.";

    private static bool IsUsableImageUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url) && SafeUrl.ToUrl(url) != SafeUrl.FallbackUrl;

    private static void AddIf(List<string> errors, bool condition, string message)
    {
        if (condition)
        {
            errors.Add(message);
        }
    }
}

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
            Credit = string.IsNullOrWhiteSpace(media.Credit) ? null : media.Credit.Trim(),
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
                .Select(media => new ProductMediaForm { Kind = media.Kind, Url = media.Url, Alt = media.Alt, Credit = media.Credit, SortOrder = media.SortOrder }).ToList(),
            Facts = product.Facts.OrderBy(fact => fact.SortOrder)
                .Select(fact => new ProductFactForm { Group = fact.Group, Label = fact.Label, Value = fact.Value, SortOrder = fact.SortOrder }).ToList(),
        };
    }
}

/// <summary>Row of the section list. CanDelete is false when the section has categories.</summary>
public sealed record SectionRow(int Id, string Name, string Slug, CatalogStatus Status, int SortOrder, int CategoryCount, int ProductCount, string ImageUrl, bool CanDelete);

/// <summary>Row of the category list. CanDelete is false when the category has products.</summary>
public sealed record CategoryRow(int Id, string Name, string Slug, string SectionName, CatalogStatus Status, int ProductCount, int SortOrder, string ImageUrl, bool CanDelete);

/// <summary>Row of the tag list.</summary>
public sealed record TagRow(int Id, string Name, string Slug, string SectionName, int ProductCount, int SortOrder);

/// <summary>Editable section.</summary>
public sealed class SectionForm
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool RequiresPreparation { get; set; }
    public CatalogStatus Status { get; set; } = CatalogStatus.Active;
}

/// <summary>Editable category.</summary>
public sealed class CategoryForm
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int SectionId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public CatalogStatus Status { get; set; } = CatalogStatus.Active;
}

/// <summary>Editable tag.</summary>
public sealed class TagForm
{
    public int? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int SectionId { get; set; }
    public int SortOrder { get; set; }
}

public interface IAdminTaxonomy
{
    /// <summary>Lists every section with its counts.</summary>
    Task<IReadOnlyList<SectionRow>> ListSectionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Loads a section for editing, or null when it does not exist.</summary>
    Task<SectionForm?> GetSectionAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a section after validation and audits the change.</summary>
    Task<AdminResult> SaveSectionAsync(int? actorUserId, SectionForm form, CancellationToken cancellationToken = default);

    /// <summary>Changes the status of a section and audits the change.</summary>
    Task<AdminResult> SetSectionStatusAsync(int? actorUserId, int id, CatalogStatus status, CancellationToken cancellationToken = default);

    /// <summary>Deletes a section without categories, or fails telling it can only be archived.</summary>
    Task<AdminResult> DeleteSectionAsync(int? actorUserId, int id, CancellationToken cancellationToken = default);

    /// <summary>Lists the categories of a section, or every category when no section is given, with their product count.</summary>
    Task<IReadOnlyList<CategoryRow>> ListCategoriesAsync(int? sectionId = null, CancellationToken cancellationToken = default);

    /// <summary>Loads a category for editing, or null when it does not exist.</summary>
    Task<CategoryForm?> GetCategoryAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a category after validation and audits the change.</summary>
    Task<AdminResult> SaveCategoryAsync(int? actorUserId, CategoryForm form, CancellationToken cancellationToken = default);

    /// <summary>Changes the status of a category and audits the change.</summary>
    Task<AdminResult> SetCategoryStatusAsync(int? actorUserId, int id, CatalogStatus status, CancellationToken cancellationToken = default);

    /// <summary>Deletes a category without products, or fails telling it can only be archived.</summary>
    Task<AdminResult> DeleteCategoryAsync(int? actorUserId, int id, CancellationToken cancellationToken = default);

    /// <summary>Lists the tags of a section, or every tag when no section is given, with their product count.</summary>
    Task<IReadOnlyList<TagRow>> ListTagsAsync(int? sectionId = null, CancellationToken cancellationToken = default);

    /// <summary>Loads a tag for editing, or null when it does not exist.</summary>
    Task<TagForm?> GetTagAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a tag after validation and audits the change.</summary>
    Task<AdminResult> SaveTagAsync(int? actorUserId, TagForm form, CancellationToken cancellationToken = default);

    /// <summary>Deletes a tag and unlinks it from its products; tags are never referenced by orders.</summary>
    Task<AdminResult> DeleteTagAsync(int? actorUserId, int id, CancellationToken cancellationToken = default);

    /// <summary>Lists sections for selects.</summary>
    Task<IReadOnlyList<SectionOption>> ListSectionOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists categories for selects.</summary>
    Task<IReadOnlyList<CategoryOption>> ListCategoryOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists tags for selects.</summary>
    Task<IReadOnlyList<TagOption>> ListTagOptionsAsync(CancellationToken cancellationToken = default);
}

public sealed class AdminTaxonomyService(IDbContextFactory<UnaykaDbContext> factory, IAuditLog audit) : IAdminTaxonomy
{
    private const string SectionNotFound = "Secção não encontrada.";
    private const string CategoryNotFound = "Categoria não encontrada.";
    private const string TagNotFound = "Etiqueta não encontrada.";
    private const string SectionMissing = "A secção escolhida não existe.";

    public async Task<IReadOnlyList<SectionRow>> ListSectionsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Sections.AsNoTracking()
            .OrderBy(section => section.SortOrder)
            .ThenBy(section => section.Name)
            .Select(section => new SectionRow(
                section.Id,
                section.Name,
                section.Slug,
                section.Status,
                section.SortOrder,
                section.Categories.Count,
                section.Categories.SelectMany(category => category.Products).Count(),
                section.ImageUrl,
                !section.Categories.Any()))
            .ToListAsync(cancellationToken);
    }

    public async Task<SectionForm?> GetSectionAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Sections.AsNoTracking()
            .Where(section => section.Id == id)
            .Select(section => new SectionForm
            {
                Id = section.Id,
                Name = section.Name,
                Slug = section.Slug,
                Description = section.Description,
                ImageUrl = section.ImageUrl,
                SortOrder = section.SortOrder,
                RequiresPreparation = section.RequiresPreparation,
                Status = section.Status,
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AdminResult> SaveSectionAsync(int? actorUserId, SectionForm form, CancellationToken cancellationToken = default)
    {
        form.Slug = form.Slug.Trim().ToLowerInvariant();
        var errors = CatalogValidator.ValidateSection(form);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (await db.Sections.AnyAsync(section => section.Slug == form.Slug && section.Id != (form.Id ?? 0), cancellationToken))
        {
            errors.Add("Já existe uma secção com este endereço (slug).");
        }

        if (errors.Count > 0)
        {
            return AdminResult.Fail(errors);
        }

        var section = form.Id is { } id ? await db.Sections.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken) : null;
        if (form.Id is not null && section is null)
        {
            return AdminResult.Fail(SectionNotFound);
        }

        var isNew = section is null;
        if (section is null)
        {
            section = new Section();
            db.Sections.Add(section);
        }

        section.Name = form.Name.Trim();
        section.Slug = form.Slug;
        section.Description = form.Description.Trim();
        section.ImageUrl = form.ImageUrl.Trim();
        section.SortOrder = form.SortOrder;
        section.RequiresPreparation = form.RequiresPreparation;
        section.Status = form.Status;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteEntityAsync(
            actorUserId,
            isNew ? AdminAuditActions.SectionCreated : AdminAuditActions.SectionUpdated,
            AdminEntityTypes.Section,
            section.Id,
            isNew ? $"Secção {section.Name} criada." : $"Secção {section.Name} atualizada.",
            cancellationToken);
        return AdminResult.Ok(section.Id);
    }

    public async Task<AdminResult> SetSectionStatusAsync(int? actorUserId, int id, CatalogStatus status, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var section = await db.Sections.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (section is null)
        {
            return AdminResult.Fail(SectionNotFound);
        }

        section.Status = status;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteEntityAsync(actorUserId, AdminAuditActions.SectionStatusChanged, AdminEntityTypes.Section, id, $"Secção {section.Name}: {StatusNames.For(status)}.", cancellationToken);
        return AdminResult.Ok(id);
    }

    public async Task<AdminResult> DeleteSectionAsync(int? actorUserId, int id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var section = await db.Sections.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (section is null)
        {
            return AdminResult.Fail(SectionNotFound);
        }

        if (await db.Categories.AnyAsync(category => category.SectionId == id, cancellationToken))
        {
            return AdminResult.Fail("Esta secção tem categorias e só pode ser arquivada.");
        }

        db.Sections.Remove(section);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteEntityAsync(actorUserId, AdminAuditActions.SectionDeleted, AdminEntityTypes.Section, id, $"Secção {section.Name} eliminada.", cancellationToken);
        return AdminResult.Ok(id);
    }

    public async Task<IReadOnlyList<CategoryRow>> ListCategoriesAsync(int? sectionId = null, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Categories.AsNoTracking()
            .Where(category => sectionId == null || category.SectionId == sectionId)
            .OrderBy(category => category.Section!.SortOrder)
            .ThenBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .Select(category => new CategoryRow(
                category.Id,
                category.Name,
                category.Slug,
                category.Section!.Name,
                category.Status,
                category.Products.Count,
                category.SortOrder,
                category.ImageUrl,
                !category.Products.Any()))
            .ToListAsync(cancellationToken);
    }

    public async Task<CategoryForm?> GetCategoryAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Categories.AsNoTracking()
            .Where(category => category.Id == id)
            .Select(category => new CategoryForm
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                SectionId = category.SectionId,
                Description = category.Description,
                ImageUrl = category.ImageUrl,
                SortOrder = category.SortOrder,
                Status = category.Status,
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AdminResult> SaveCategoryAsync(int? actorUserId, CategoryForm form, CancellationToken cancellationToken = default)
    {
        form.Slug = form.Slug.Trim().ToLowerInvariant();
        var errors = CatalogValidator.ValidateCategory(form);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (form.SectionId > 0 && !await db.Sections.AnyAsync(section => section.Id == form.SectionId, cancellationToken))
        {
            errors.Add(SectionMissing);
        }

        if (await db.Categories.AnyAsync(category => category.Slug == form.Slug && category.Id != (form.Id ?? 0), cancellationToken))
        {
            errors.Add("Já existe uma categoria com este endereço (slug).");
        }

        if (errors.Count > 0)
        {
            return AdminResult.Fail(errors);
        }

        var category = form.Id is { } id ? await db.Categories.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken) : null;
        if (form.Id is not null && category is null)
        {
            return AdminResult.Fail(CategoryNotFound);
        }

        var isNew = category is null;
        if (category is null)
        {
            category = new Category();
            db.Categories.Add(category);
        }

        category.Name = form.Name.Trim();
        category.Slug = form.Slug;
        category.SectionId = form.SectionId;
        category.Description = form.Description.Trim();
        category.ImageUrl = form.ImageUrl.Trim();
        category.SortOrder = form.SortOrder;
        category.Status = form.Status;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteEntityAsync(
            actorUserId,
            isNew ? AdminAuditActions.CategoryCreated : AdminAuditActions.CategoryUpdated,
            AdminEntityTypes.Category,
            category.Id,
            isNew ? $"Categoria {category.Name} criada." : $"Categoria {category.Name} atualizada.",
            cancellationToken);
        return AdminResult.Ok(category.Id);
    }

    public async Task<AdminResult> SetCategoryStatusAsync(int? actorUserId, int id, CatalogStatus status, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var category = await db.Categories.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (category is null)
        {
            return AdminResult.Fail(CategoryNotFound);
        }

        category.Status = status;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteEntityAsync(actorUserId, AdminAuditActions.CategoryStatusChanged, AdminEntityTypes.Category, id, $"Categoria {category.Name}: {StatusNames.For(status)}.", cancellationToken);
        return AdminResult.Ok(id);
    }

    public async Task<AdminResult> DeleteCategoryAsync(int? actorUserId, int id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var category = await db.Categories.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (category is null)
        {
            return AdminResult.Fail(CategoryNotFound);
        }

        if (await db.Products.AnyAsync(product => product.CategoryId == id, cancellationToken))
        {
            return AdminResult.Fail("Esta categoria tem produtos e só pode ser arquivada.");
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteEntityAsync(actorUserId, AdminAuditActions.CategoryDeleted, AdminEntityTypes.Category, id, $"Categoria {category.Name} eliminada.", cancellationToken);
        return AdminResult.Ok(id);
    }

    public async Task<IReadOnlyList<TagRow>> ListTagsAsync(int? sectionId = null, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Tags.AsNoTracking()
            .Where(tag => sectionId == null || tag.SectionId == sectionId)
            .OrderBy(tag => tag.Section!.SortOrder)
            .ThenBy(tag => tag.SortOrder)
            .ThenBy(tag => tag.Name)
            .Select(tag => new TagRow(tag.Id, tag.Name, tag.Slug, tag.Section!.Name, tag.Products.Count, tag.SortOrder))
            .ToListAsync(cancellationToken);
    }

    public async Task<TagForm?> GetTagAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Tags.AsNoTracking()
            .Where(tag => tag.Id == id)
            .Select(tag => new TagForm { Id = tag.Id, Name = tag.Name, Slug = tag.Slug, SectionId = tag.SectionId, SortOrder = tag.SortOrder })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AdminResult> SaveTagAsync(int? actorUserId, TagForm form, CancellationToken cancellationToken = default)
    {
        form.Slug = form.Slug.Trim().ToLowerInvariant();
        var errors = CatalogValidator.ValidateTag(form);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (form.SectionId > 0 && !await db.Sections.AnyAsync(section => section.Id == form.SectionId, cancellationToken))
        {
            errors.Add(SectionMissing);
        }

        if (await db.Tags.AnyAsync(tag => tag.SectionId == form.SectionId && tag.Slug == form.Slug && tag.Id != (form.Id ?? 0), cancellationToken))
        {
            errors.Add("Já existe uma etiqueta com este endereço (slug) nesta secção.");
        }

        if (errors.Count > 0)
        {
            return AdminResult.Fail(errors);
        }

        var tag = form.Id is { } id ? await db.Tags.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken) : null;
        if (form.Id is not null && tag is null)
        {
            return AdminResult.Fail(TagNotFound);
        }

        var isNew = tag is null;
        if (tag is null)
        {
            tag = new Tag();
            db.Tags.Add(tag);
        }

        tag.Name = form.Name.Trim();
        tag.Slug = form.Slug;
        tag.SectionId = form.SectionId;
        tag.SortOrder = form.SortOrder;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteEntityAsync(
            actorUserId,
            isNew ? AdminAuditActions.TagCreated : AdminAuditActions.TagUpdated,
            AdminEntityTypes.Tag,
            tag.Id,
            isNew ? $"Etiqueta {tag.Name} criada." : $"Etiqueta {tag.Name} atualizada.",
            cancellationToken);
        return AdminResult.Ok(tag.Id);
    }

    public async Task<AdminResult> DeleteTagAsync(int? actorUserId, int id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var tag = await db.Tags.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (tag is null)
        {
            return AdminResult.Fail(TagNotFound);
        }

        db.Tags.Remove(tag);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteEntityAsync(actorUserId, AdminAuditActions.TagDeleted, AdminEntityTypes.Tag, id, $"Etiqueta {tag.Name} eliminada.", cancellationToken);
        return AdminResult.Ok(id);
    }

    public async Task<IReadOnlyList<SectionOption>> ListSectionOptionsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Sections.AsNoTracking()
            .Where(section => section.Status != CatalogStatus.Archived)
            .OrderBy(section => section.SortOrder)
            .Select(section => new SectionOption(section.Id, section.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryOption>> ListCategoryOptionsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Categories.AsNoTracking()
            .Where(category => category.Status != CatalogStatus.Archived)
            .OrderBy(category => category.Section!.SortOrder)
            .ThenBy(category => category.SortOrder)
            .Select(category => new CategoryOption(category.Id, category.Name, category.SectionId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TagOption>> ListTagOptionsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Tags.AsNoTracking()
            .OrderBy(tag => tag.SortOrder)
            .ThenBy(tag => tag.Name)
            .Select(tag => new TagOption(tag.Id, tag.Name, tag.SectionId))
            .ToListAsync(cancellationToken);
    }
}
