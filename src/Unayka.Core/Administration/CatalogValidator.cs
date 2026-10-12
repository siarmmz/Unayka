using Unayka.Core.Presentation;

namespace Unayka.Core.Administration;

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
