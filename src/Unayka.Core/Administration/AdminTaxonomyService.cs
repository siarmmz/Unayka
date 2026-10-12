using Microsoft.EntityFrameworkCore;
using Unayka.Core.Audit;
using Unayka.Core.Catalog;
using Unayka.Core.Data;

namespace Unayka.Core.Administration;

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

    /// <summary>Lists every category with its product count.</summary>
    Task<IReadOnlyList<CategoryRow>> ListCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Loads a category for editing, or null when it does not exist.</summary>
    Task<CategoryForm?> GetCategoryAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a category after validation and audits the change.</summary>
    Task<AdminResult> SaveCategoryAsync(int? actorUserId, CategoryForm form, CancellationToken cancellationToken = default);

    /// <summary>Changes the status of a category and audits the change.</summary>
    Task<AdminResult> SetCategoryStatusAsync(int? actorUserId, int id, CatalogStatus status, CancellationToken cancellationToken = default);

    /// <summary>Deletes a category without products, or fails telling it can only be archived.</summary>
    Task<AdminResult> DeleteCategoryAsync(int? actorUserId, int id, CancellationToken cancellationToken = default);

    /// <summary>Lists every tag with its section and product count.</summary>
    Task<IReadOnlyList<TagRow>> ListTagsAsync(CancellationToken cancellationToken = default);

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

    public async Task<IReadOnlyList<CategoryRow>> ListCategoriesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Categories.AsNoTracking()
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

    public async Task<IReadOnlyList<TagRow>> ListTagsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        return await db.Tags.AsNoTracking()
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
