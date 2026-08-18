namespace ASPClassic.Application.DTOs.Inc;

/// <summary>Represents a single value/title pair from a dynamic lookup table query.</summary>
public class LookupSelectionItemDto
{
    public string Value { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
}
