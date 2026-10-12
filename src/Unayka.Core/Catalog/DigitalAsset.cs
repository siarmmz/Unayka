namespace Unayka.Core.Catalog;

/// <summary>Downloadable file of a digital product, kept in private storage.</summary>
public sealed class DigitalAsset
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}
