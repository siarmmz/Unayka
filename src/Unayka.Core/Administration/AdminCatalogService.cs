using Microsoft.EntityFrameworkCore;
using Unayka.Core.Audit;
using Unayka.Core.Catalog;
using Unayka.Core.Data;

namespace Unayka.Core.Administration;

public interface IAdminCatalog
{
    /// <summary>Lists products with an optional text search.</summary>
    Task<PagedResult<ProductRow>> ListProductsAsync(string? search, int page, CancellationToken cancellationToken = default);

    /// <summary>Loads a product for editing, or null when it does not exist.</summary>
    Task<ProductForm?> GetProductAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a product after validation and audits the change.</summary>
    Task<AdminResult> SaveProductAsync(int? actorUserId, ProductForm form, CancellationToken cancellationToken = default);

    /// <summary>Shows or hides a product in the shop and audits the change.</summary>
    Task<AdminResult> SetProductActiveAsync(int? actorUserId, int id, bool isActive, CancellationToken cancellationToken = default);

    /// <summary>Lists every category with its product count.</summary>
    Task<IReadOnlyList<CategoryRow>> ListCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists categories for selects.</summary>
    Task<IReadOnlyList<CategoryOption>> ListCategoryOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Loads a category for editing, or null when it does not exist.</summary>
    Task<CategoryForm?> GetCategoryAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a category after validation and audits the change.</summary>
    Task<AdminResult> SaveCategoryAsync(int? actorUserId, CategoryForm form, CancellationToken cancellationToken = default);
}

public sealed class AdminCatalogService(IDbContextFactory<UnaykaDbContext> factory, IAuditLog audit, TimeProvider time) : IAdminCatalog
{
    public async Task<PagedResult<ProductRow>> ListProductsAsync(string? search, int page, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var query = db.Products.AsNoTracking().AsQueryable();
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
                product.StockQuantity,
                product.IsActive,
                product.IsFeatured,
                product.Media.OrderBy(media => media.SortOrder).Select(media => media.Url).FirstOrDefault()))
            .ToListAsync(cancellationToken);
        return new PagedResult<ProductRow>(items, pageNumber, AdminPaging.DefaultPageSize, total);
    }

    public async Task<ProductForm?> GetProductAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var product = await db.Products.AsNoTracking()
            .Include(candidate => candidate.Media)
            .Include(candidate => candidate.Facts)
            .AsSplitQuery()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        return product is null ? null : ToForm(product);
    }

    public async Task<AdminResult> SaveProductAsync(int? actorUserId, ProductForm form, CancellationToken cancellationToken = default)
    {
        form.Slug = form.Slug.Trim().ToLowerInvariant();
        var errors = CatalogValidator.ValidateProduct(form);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        if (form.CategoryId > 0 && !await db.Categories.AnyAsync(category => category.Id == form.CategoryId, cancellationToken))
        {
            errors.Add("A categoria escolhida não existe.");
        }

        if (await db.Products.AnyAsync(product => product.Slug == form.Slug && product.Id != (form.Id ?? 0), cancellationToken))
        {
            errors.Add("Já existe um produto com este endereço (slug).");
        }

        if (errors.Count > 0)
        {
            return AdminResult.Fail(errors);
        }

        var product = form.Id is { } id
            ? await db.Products.Include(candidate => candidate.Media).Include(candidate => candidate.Facts).FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            : null;
        if (form.Id is not null && product is null)
        {
            return AdminResult.Fail("Produto não encontrado.");
        }

        var isNew = product is null;
        if (product is null)
        {
            product = new Product { CreatedAt = time.GetUtcNow() };
            db.Products.Add(product);
        }

        Apply(form, product);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(
            actorUserId,
            isNew ? AdminAuditActions.ProductCreated : AdminAuditActions.ProductUpdated,
            AdminEntityTypes.Product,
            product.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            isNew ? $"Produto {product.Name} criado." : $"Produto {product.Name} atualizado.",
            cancellationToken);
        return AdminResult.Ok(product.Id);
    }

    public async Task<AdminResult> SetProductActiveAsync(int? actorUserId, int id, bool isActive, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var product = await db.Products.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (product is null)
        {
            return AdminResult.Fail("Produto não encontrado.");
        }

        product.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(
            actorUserId,
            AdminAuditActions.ProductToggled,
            AdminEntityTypes.Product,
            product.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            isActive ? $"Produto {product.Name} ativado." : $"Produto {product.Name} desativado.",
            cancellationToken);
        return AdminResult.Ok(product.Id);
    }

    public async Task<IReadOnlyList<CategoryRow>> ListCategoriesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Categories.AsNoTracking()
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .Select(category => new CategoryRow(category.Id, category.Name, category.Slug, category.Kind, category.Products.Count, category.SortOrder, category.ImageUrl))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryOption>> ListCategoryOptionsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Categories.AsNoTracking()
            .OrderBy(category => category.SortOrder)
            .Select(category => new CategoryOption(category.Id, category.Name))
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
                Kind = category.Kind,
                Description = category.Description,
                ImageUrl = category.ImageUrl,
                SortOrder = category.SortOrder,
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AdminResult> SaveCategoryAsync(int? actorUserId, CategoryForm form, CancellationToken cancellationToken = default)
    {
        form.Slug = form.Slug.Trim().ToLowerInvariant();
        var errors = CatalogValidator.ValidateCategory(form);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
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
            return AdminResult.Fail("Categoria não encontrada.");
        }

        var isNew = category is null;
        if (category is null)
        {
            category = new Category();
            db.Categories.Add(category);
        }

        category.Name = form.Name.Trim();
        category.Slug = form.Slug;
        category.Kind = form.Kind;
        category.Description = form.Description.Trim();
        category.ImageUrl = form.ImageUrl.Trim();
        category.SortOrder = form.SortOrder;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(
            actorUserId,
            isNew ? AdminAuditActions.CategoryCreated : AdminAuditActions.CategoryUpdated,
            AdminEntityTypes.Category,
            category.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            isNew ? $"Categoria {category.Name} criada." : $"Categoria {category.Name} atualizada.",
            cancellationToken);
        return AdminResult.Ok(category.Id);
    }

    private static void Apply(ProductForm form, Product product)
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
        product.IsActive = form.IsActive;
        product.StockQuantity = form.StockQuantity;

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

    private static ProductForm ToForm(Product product) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Slug = product.Slug,
        CategoryId = product.CategoryId,
        ShortDescription = product.ShortDescription,
        Description = product.Description,
        Price = MoneyInput.ToAmount(product.PriceMinor),
        CompareAtPrice = product.CompareAtPriceMinor is { } compare ? MoneyInput.ToAmount(compare) : null,
        Badge = product.Badge,
        IsFeatured = product.IsFeatured,
        IsActive = product.IsActive,
        StockQuantity = product.StockQuantity,
        Media = product.Media.OrderBy(media => media.SortOrder)
            .Select(media => new ProductMediaForm { Kind = media.Kind, Url = media.Url, Alt = media.Alt, SortOrder = media.SortOrder }).ToList(),
        Facts = product.Facts.OrderBy(fact => fact.SortOrder)
            .Select(fact => new ProductFactForm { Group = fact.Group, Label = fact.Label, Value = fact.Value, SortOrder = fact.SortOrder }).ToList(),
    };
}
