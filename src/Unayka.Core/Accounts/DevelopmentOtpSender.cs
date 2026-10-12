using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Unayka.Core.Accounts;

public sealed class DevelopmentOtpInbox : IDevelopmentOtpInbox
{
    private readonly ConcurrentDictionary<string, string> _codes = new();

    public void Store(string phone, string code) => _codes[phone] = code;

    public string? Peek(string phone) => _codes.GetValueOrDefault(phone);
}

public sealed partial class DevelopmentOtpSender(IDevelopmentOtpInbox inbox, ILogger<DevelopmentOtpSender> logger) : IOtpSender
{
    public Task SendAsync(string phone, string code, CancellationToken cancellationToken = default)
    {
        inbox.Store(phone, code);
        LogCodeSent(logger);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Login code sent")]
    private static partial void LogCodeSent(ILogger logger);
}
