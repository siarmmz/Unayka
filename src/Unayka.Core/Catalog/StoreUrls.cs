namespace Unayka.Core.Catalog;

public static class StoreUrls
{
    public const string Shop = "/loja";
    public const string AllProducts = "/loja/todos";
    public const string Cart = "/carrinho";
    public const string Checkout = "/checkout";
    public const string Account = "/conta";
    public const string SignIn = "/conta/entrar";
    public const string SignOut = "/conta/sair";
    public const string Admin = "/admin";

    public static string Product(string slug) => $"/produto/{slug}";

    public static string Section(string slug) => $"{Shop}/{slug}";

    public static string Category(string slug) => $"/loja/categoria/{slug}";

    public static string Search(string term) => $"{AllProducts}?q={Uri.EscapeDataString(term)}";
}
