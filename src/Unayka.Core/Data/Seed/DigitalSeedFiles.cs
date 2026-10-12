using System.Text;
using Unayka.Core.Catalog;
using Unayka.Core.Configuration;

namespace Unayka.Core.Data.Seed;

public static class DigitalSeedFiles
{
    public const string DigitalFolder = "digital";
    public const string PdfContentType = "application/pdf";

    private const string EbookStorageKey = "receitas-com-cha.pdf";
    private const string PlaceholderPdf =
        "%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 200 200]>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n";

    public static DigitalAsset CreateEbookAsset()
    {
        var bytes = Encoding.ASCII.GetBytes(PlaceholderPdf);
        WriteOnce(EbookStorageKey, bytes);
        return new DigitalAsset
        {
            StorageKey = EbookStorageKey,
            FileName = EbookStorageKey,
            ContentType = PdfContentType,
            SizeBytes = bytes.Length,
        };
    }

    private static void WriteOnce(string storageKey, byte[] bytes)
    {
        var folder = Path.Combine(AppEnvConfig.LocalUploadsPath, DigitalFolder);
        Directory.CreateDirectory(folder);
        try
        {
            using var stream = new FileStream(Path.Combine(folder, storageKey), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            stream.Write(bytes);
        }
        catch (IOException)
        {
            // Another seed run created the file first, which is fine.
        }
    }
}
