namespace Unayka.Core.Orders.Downloads;

public enum DownloadOutcome
{
    Granted,
    NotSignedIn,
    NotFound,
    NotPaid,
    FileMissing,
}

/// <summary>Result of a download request. File is set only when the outcome is Granted and the caller owns and must dispose it.</summary>
public sealed record DownloadResult(DownloadOutcome Outcome, DigitalFile? File);

/// <summary>Opened digital file ready to stream.</summary>
public sealed record DigitalFile(Stream Content, string FileName, string ContentType);

public interface IDigitalDownloads
{
    /// <summary>Opens the file of a digital order line for the signed in buyer of a paid order, counts the download and audits it.</summary>
    Task<DownloadResult> OpenAsync(int orderItemId, int? userId, CancellationToken cancellationToken = default);
}
