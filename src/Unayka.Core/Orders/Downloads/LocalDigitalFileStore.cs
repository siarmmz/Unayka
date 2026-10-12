using Unayka.Core.Configuration;
using Unayka.Core.Data.Seed;

namespace Unayka.Core.Orders.Downloads;

public sealed class LocalDigitalFileStore : IDigitalFileStore
{
    public Stream? Open(string storageKey)
    {
        if (string.IsNullOrEmpty(storageKey) || Path.GetFileName(storageKey) != storageKey)
        {
            return null;
        }

        var path = Path.Combine(AppEnvConfig.LocalUploadsPath, DigitalSeedFiles.DigitalFolder, storageKey);
        return File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
    }
}
