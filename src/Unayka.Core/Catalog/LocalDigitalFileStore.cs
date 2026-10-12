using Unayka.Core.Configuration;
using Unayka.Core.Data.Seed;

namespace Unayka.Core.Catalog;

public sealed class LocalDigitalFileStore : IDigitalFileStore
{
    public async Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default)
    {
        var path = PathFor(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var target = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(target, cancellationToken);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var path = PathFor(storageKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private static string PathFor(string storageKey) =>
        Path.Combine(AppEnvConfig.LocalUploadsPath, DigitalSeedFiles.DigitalFolder, Path.GetFileName(storageKey));
}
