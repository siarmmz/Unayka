namespace Unayka.Core.Catalog;

public interface IDigitalFileStore
{
    /// <summary>Writes a private digital file under the given key, replacing any previous file.</summary>
    Task SaveAsync(string storageKey, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Removes a private digital file when it exists.</summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}
