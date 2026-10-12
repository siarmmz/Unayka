namespace Unayka.Core.Counter;

public static class CounterWallet
{
    public const string Provider = "counter-mock";
    public const string CardProvider = "counter-terminal";
    public const string CashProvider = "counter-cash";
    public const int ApprovalDelayMilliseconds = 3000;
    private const char FailingLastDigit = '0';

    public static bool Approves(string? normalizedPhone) =>
        !string.IsNullOrEmpty(normalizedPhone) && normalizedPhone[^1] != FailingLastDigit;
}
