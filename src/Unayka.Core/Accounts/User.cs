namespace Unayka.Core.Accounts;

/// <summary>Customer or staff member. Phone is stored as the nine digit national number.</summary>
public sealed class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? PasswordHash { get; set; }
    public string? ExternalProvider { get; set; }
    public string? ExternalKey { get; set; }
    public UserRole Role { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}
