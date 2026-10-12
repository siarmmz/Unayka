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
    public static readonly string HeroUrl = UnsplashImage.Wide("1597481499750-3e6b22637e12");
    public static readonly string StoryUrl = UnsplashImage.Wide("1594631252845-29fc4cc8cde9");
    public static readonly string TeaBannerUrl = UnsplashImage.Wide("1576092768241-dec231879fc3");
    public static readonly string CosmeticsBannerUrl = UnsplashImage.Wide("1531123897727-8f129e1688ce");
    public static readonly string LoginUrl = UnsplashImage.Wide("1611432579402-7037e3e2c1e4");
    public static readonly string AboutUrl = UnsplashImage.Wide("1494314671902-399b18174975");
    public static readonly string ContactUrl = UnsplashImage.Wide("1521302080334-4bebac2763a6");
    public static readonly string LocationUrl = UnsplashImage.Wide("1494314671902-399b18174975");
}
