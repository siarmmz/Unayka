using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Unayka.Core.Administration;
using Unayka.Core.Audit;
using Unayka.Core.Data;
using Unayka.Core.Payments;
using Unayka.Core.Presentation;

namespace Unayka.Core.Site;

/// <summary>Editable piece of site content (hero slide, step, social link, page text and similar), grouped by page and placement and managed in the dashboard.</summary>
public sealed class SiteBlock
{
    public int Id { get; set; }
    public string Page { get; set; } = string.Empty;
    public string Placement { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public string? Eyebrow { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? ImageUrl { get; set; }
    public string? ActionLabel { get; set; }
    public string? ActionUrl { get; set; }
    public string? Icon { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>Active site block as rendered on public pages.</summary>
public sealed record SiteBlockView(
    int Id,
    string Placement,
    string Eyebrow,
    string Title,
    string Body,
    string ImageUrl,
    string ActionLabel,
    string ActionUrl,
    string Icon);

public static class SitePages
{
    public const string Shared = "geral";
    public const string Home = "inicio";
    public const string About = "sobre";
    public const string Contact = "contacto";
    public const string Shop = "loja";
    public const string Login = "entrar";

    public static readonly IReadOnlyList<string> All = [Shared, Home, About, Contact, Shop, Login];
}

public static class SitePlacements
{
    public const string Seo = "seo";
    public const string Hero = "destaque";
    public const string Featured = "favoritos";
    public const string SectionRow = "linha-seccao";
    public const string Steps = "passos";
    public const string Step = "passo";
    public const string Contact = "contacto";
    public const string Story = "historia";
    public const string Values = "valores";
    public const string Value = "valor";
    public const string Brand = "marca";
    public const string Announcement = "anuncio";
    public const string Social = "rede-social";
    public const string HelpLink = "ajuda";
    public const string PaymentNote = "nota-pagamento";
}

/// <summary>Grouped blocks of one page with lookups by placement.</summary>
public sealed record SitePage(IReadOnlyList<SiteBlockView> Blocks)
{
    private static readonly SiteBlockView Empty = new(0, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);

    public SiteBlockView One(string placement) => Blocks.FirstOrDefault(block => block.Placement == placement) ?? Empty;

    public IReadOnlyList<SiteBlockView> Many(string placement) => Blocks.Where(block => block.Placement == placement).ToList();
}

public interface ISiteContent
{
    /// <summary>Active blocks of a page in display order.</summary>
    Task<SitePage> PageAsync(string page, CancellationToken cancellationToken = default);
}

public sealed class SiteContent(IDbContextFactory<UnaykaDbContext> factory) : ISiteContent
{
    public async Task<SitePage> PageAsync(string page, CancellationToken cancellationToken = default)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var blocks = await database.SiteBlocks.AsNoTracking()
            .Where(block => block.Page == page && block.IsActive)
            .OrderBy(block => block.Placement).ThenBy(block => block.SortOrder)
            .Select(block => new SiteBlockView(
                block.Id,
                block.Placement,
                block.Eyebrow ?? string.Empty,
                block.Title ?? string.Empty,
                block.Body ?? string.Empty,
                block.ImageUrl ?? string.Empty,
                block.ActionLabel ?? string.Empty,
                block.ActionUrl ?? string.Empty,
                block.Icon ?? string.Empty))
            .ToListAsync(cancellationToken);
        return new SitePage(blocks);
    }
}

/// <summary>Site block edit form.</summary>
public sealed record SiteBlockForm(
    int? Id,
    string Page,
    string Placement,
    int SortOrder,
    string? Eyebrow,
    string? Title,
    string? Body,
    string? ImageUrl,
    string? ActionLabel,
    string? ActionUrl,
    string? Icon,
    bool IsActive);

/// <summary>Payment option edit form; Method identifies the option.</summary>
public sealed record PaymentOptionForm(
    PaymentMethod Method,
    string Label,
    string? LogoUrl,
    string Icon,
    string Provider,
    string? ProviderCode,
    bool IsOnline,
    bool IsCounter,
    bool IsActive,
    int SortOrder);

public interface IAdminSite
{
    /// <summary>Every block of a page, active or not, in display order.</summary>
    Task<IReadOnlyList<SiteBlockForm>> BlocksAsync(string page, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a block after validation and audits it.</summary>
    Task<AdminResult> SaveBlockAsync(SiteBlockForm form, int? actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Deletes a block and audits it.</summary>
    Task<AdminResult> DeleteBlockAsync(int id, int? actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Every payment option in display order.</summary>
    Task<IReadOnlyList<PaymentOptionForm>> PaymentOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Updates an existing payment option after validation and audits it.</summary>
    Task<AdminResult> SavePaymentOptionAsync(PaymentOptionForm form, int? actorUserId, CancellationToken cancellationToken = default);
}

public sealed class AdminSite(IDbContextFactory<UnaykaDbContext> factory, IAuditLog audit) : IAdminSite
{
    private const string BlockEntity = "SiteBlock";
    private const string PaymentOptionEntity = "PaymentOption";
    private const int MaxShortText = 160;
    private const int MaxBodyText = 2000;
    private const string InvalidPage = "Página inválida.";
    private const string InvalidPlacement = "Indique o lugar do bloco.";
    private const string TextTooLong = "Um dos textos é demasiado longo.";
    private const string InvalidUrl = "Use um endereço que comece por https:// ou /.";
    private const string InvalidIcon = "O ícone deve ser uma classe Bootstrap Icons, por exemplo bi-facebook.";
    private const string InvalidLabel = "Indique o nome do meio de pagamento.";
    private const string NotFound = "O registo já não existe.";
    private const string IconPrefix = "bi-";

    public async Task<IReadOnlyList<SiteBlockForm>> BlocksAsync(string page, CancellationToken cancellationToken = default)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        return await database.SiteBlocks.AsNoTracking()
            .Where(block => block.Page == page)
            .OrderBy(block => block.Placement).ThenBy(block => block.SortOrder)
            .Select(block => new SiteBlockForm(block.Id, block.Page, block.Placement, block.SortOrder, block.Eyebrow, block.Title, block.Body, block.ImageUrl, block.ActionLabel, block.ActionUrl, block.Icon, block.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminResult> SaveBlockAsync(SiteBlockForm form, int? actorUserId, CancellationToken cancellationToken = default)
    {
        var errors = ValidateBlock(form);
        if (errors.Count > 0)
        {
            return AdminResult.Fail(errors);
        }

        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var block = form.Id is { } id ? await database.SiteBlocks.FindAsync([id], cancellationToken) : new SiteBlock();
        if (block is null)
        {
            return AdminResult.Fail(NotFound);
        }

        if (form.Id is null)
        {
            database.SiteBlocks.Add(block);
        }

        block.Page = form.Page;
        block.Placement = form.Placement.Trim();
        block.SortOrder = form.SortOrder;
        block.Eyebrow = Clean(form.Eyebrow);
        block.Title = Clean(form.Title);
        block.Body = Clean(form.Body);
        block.ImageUrl = Clean(form.ImageUrl);
        block.ActionLabel = Clean(form.ActionLabel);
        block.ActionUrl = Clean(form.ActionUrl);
        block.Icon = Clean(form.Icon);
        block.IsActive = form.IsActive;
        await database.SaveChangesAsync(cancellationToken);
        await audit.WriteEntityAsync(actorUserId, form.Id is null ? "site_block.created" : "site_block.updated", BlockEntity, block.Id, $"Site block {block.Page}/{block.Placement} saved", cancellationToken);
        return AdminResult.Ok(block.Id);
    }

    public async Task<AdminResult> DeleteBlockAsync(int id, int? actorUserId, CancellationToken cancellationToken = default)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var block = await database.SiteBlocks.FindAsync([id], cancellationToken);
        if (block is null)
        {
            return AdminResult.Fail(NotFound);
        }

        database.SiteBlocks.Remove(block);
        await database.SaveChangesAsync(cancellationToken);
        await audit.WriteEntityAsync(actorUserId, "site_block.deleted", BlockEntity, id, $"Site block {block.Page}/{block.Placement} deleted", cancellationToken);
        return AdminResult.Ok();
    }

    public async Task<IReadOnlyList<PaymentOptionForm>> PaymentOptionsAsync(CancellationToken cancellationToken = default)
    {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        return await database.PaymentOptions.AsNoTracking()
            .OrderBy(option => option.SortOrder)
            .Select(option => new PaymentOptionForm(option.Method, option.Label, option.LogoUrl, option.Icon, option.Provider, option.ProviderCode, option.IsOnline, option.IsCounter, option.IsActive, option.SortOrder))
            .ToListAsync(cancellationToken);
    }

    public async Task<AdminResult> SavePaymentOptionAsync(PaymentOptionForm form, int? actorUserId, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(form.Label) || form.Label.Length > MaxShortText)
        {
            errors.Add(InvalidLabel);
        }

        foreach (var logoUrl in PaymentLogos.Split(form.LogoUrl))
        {
            AddUrlError(errors, logoUrl);
        }

        AddIconError(errors, form.Icon);
        if (errors.Count > 0)
        {
            return AdminResult.Fail(errors);
        }

        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var option = await database.PaymentOptions.FirstOrDefaultAsync(candidate => candidate.Method == form.Method, cancellationToken);
        if (option is null)
        {
            return AdminResult.Fail(NotFound);
        }

        option.Label = form.Label.Trim();
        option.LogoUrl = Clean(string.Join(' ', PaymentLogos.Split(form.LogoUrl)));
        option.Icon = form.Icon.Trim();
        option.Provider = form.Provider.Trim();
        option.ProviderCode = Clean(form.ProviderCode);
        option.IsOnline = form.IsOnline;
        option.IsCounter = form.IsCounter;
        option.IsActive = form.IsActive;
        option.SortOrder = form.SortOrder;
        await database.SaveChangesAsync(cancellationToken);
        await audit.WriteEntityAsync(actorUserId, "payment_option.updated", PaymentOptionEntity, option.Id, $"Payment option {option.Method} saved", cancellationToken);
        return AdminResult.Ok(option.Id);
    }

    private static List<string> ValidateBlock(SiteBlockForm form)
    {
        var errors = new List<string>();
        if (!SitePages.All.Contains(form.Page))
        {
            errors.Add(InvalidPage);
        }

        if (string.IsNullOrWhiteSpace(form.Placement))
        {
            errors.Add(InvalidPlacement);
        }

        string?[] shortTexts = [form.Eyebrow, form.Title, form.ActionLabel];
        if (shortTexts.Any(text => text?.Length > MaxShortText) || form.Body?.Length > MaxBodyText)
        {
            errors.Add(TextTooLong);
        }

        AddUrlError(errors, form.ImageUrl);
        AddUrlError(errors, form.ActionUrl);
        if (!string.IsNullOrWhiteSpace(form.Icon))
        {
            AddIconError(errors, form.Icon);
        }

        return errors;
    }

    private static void AddUrlError(List<string> errors, string? url)
    {
        if (!string.IsNullOrWhiteSpace(url) && SafeUrl.ToUrl(url) != url.Trim() && !errors.Contains(InvalidUrl))
        {
            errors.Add(InvalidUrl);
        }
    }

    private static void AddIconError(List<string> errors, string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon) || !icon.Trim().StartsWith(IconPrefix, StringComparison.Ordinal) || icon.Trim().Contains(' ', StringComparison.Ordinal))
        {
            errors.Add(InvalidIcon);
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public static class SiteServiceExtensions
{
    public static IServiceCollection AddUnaykaSite(this IServiceCollection services)
    {
        services.AddScoped<ISiteContent, SiteContent>();
        services.AddScoped<IAdminSite, AdminSite>();
        return services;
    }
}
