using Unayka.Core.Catalog;

namespace Unayka.Core.Test.Administration;

public sealed class InMemoryDigitalFileStore : IDigitalFileStore
{
    public Dictionary<string, byte[]> Files { get; } = [];

    public async Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        Files[storageKey] = buffer.ToArray();
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        Files.Remove(storageKey);
        return Task.CompletedTask;
    }
}
