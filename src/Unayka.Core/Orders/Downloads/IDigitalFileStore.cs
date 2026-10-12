namespace Unayka.Core.Orders.Downloads;

public interface IDigitalFileStore
{
    /// <summary>Opens a private digital file by storage key, or returns null when it does not exist.</summary>
    Stream? Open(string storageKey);
}
