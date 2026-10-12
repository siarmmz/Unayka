namespace Unayka.Core.Data.Seed;

public static class UnsplashImage
{
    private const int DefaultWidth = 900;
    private const int WideWidth = 1600;

    public static string Url(string photoId, int width = DefaultWidth) =>
        $"https://images.unsplash.com/photo-{photoId}?w={width}&q=80&auto=format&fit=crop";

    public static string Wide(string photoId) => Url(photoId, WideWidth);
}

public static class SiteImages
{
    public static readonly string HeroUrl = UnsplashImage.Wide("1521302080334-4bebac2763a6");
    public static readonly string StoryUrl = UnsplashImage.Wide("1442975631115-c4f7b05b8a2c");
    public static readonly string SalonUrl = UnsplashImage.Wide("1560066984-138dadb4c035");
    public static readonly string CoffeeBannerUrl = UnsplashImage.Wide("1495474472287-4d71bcdd2085");
    public static readonly string CosmeticsBannerUrl = UnsplashImage.Wide("1531123897727-8f129e1688ce");
    public static readonly string LoginUrl = UnsplashImage.Wide("1611432579402-7037e3e2c1e4");
    public static readonly string AboutUrl = UnsplashImage.Wide("1494314671902-399b18174975");
    public static readonly string ContactUrl = UnsplashImage.Wide("1521302080334-4bebac2763a6");
    public static readonly string LocationUrl = UnsplashImage.Wide("1494314671902-399b18174975");
}
