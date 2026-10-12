namespace Unayka.Core.Locations;

public enum ContactKind
{
    Phone,
    WhatsApp,
    Email,
}

/// <summary>Contact channel of a location.</summary>
public sealed class LocationContact
{
    public int Id { get; set; }
    public int LocationId { get; set; }
    public ContactKind Kind { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
