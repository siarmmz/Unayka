using Microsoft.AspNetCore.Components;

namespace Unayka.Web.Components.Store;

public static class CartNotice
{
    public const string QueryKey = "adicionado";

    public static string ReturnUrl(NavigationManager navigation, string productSlug) =>
        "/" + navigation.ToBaseRelativePath(navigation.GetUriWithQueryParameter(QueryKey, productSlug));
}
