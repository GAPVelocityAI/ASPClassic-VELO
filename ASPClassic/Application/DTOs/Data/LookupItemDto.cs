using ASPClassic.Application.Services.Data;
namespace ASPClassic.Application.DTOs.Data;

/// <summary>
/// Represents a single keyed lookup item in a DataViewLookupCollection.
/// Each item is itself a dictionary of named properties (key→value pairs).
/// Port of <c>DataViewLookupClassExtendable</c> instance stored inside <c>DataViewLookupCollectionClass</c>.
/// </summary>
public class LookupItemDto
{
    /// <summary>The unique key under which this item is stored in the collection.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The stored value this item represents — what a column holds when it is selected.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>The text shown for it.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional grouping, glyph and tooltip — the legacy lookup class carried all three.</summary>
    public string? Group { get; set; }
    public string? Glyph { get; set; }
    public string? Tooltip { get; set; }

    /// <summary>The named properties dictionary for this item (property-name → property-value).</summary>
    public Dictionary<string, string?> Properties { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Port of VBScript <c>UBound</c> property on <c>DataViewLookupClassExtendable</c>.
    /// Returns the upper bound index of the properties dictionary (Count - 1).
    /// </summary>
    public int UBound => Properties.Count - 1;

    /// <summary>
    /// Port of VBScript <c>Items</c> property on <c>DataViewLookupClassExtendable</c>.
    /// Returns all property values as a list.
    /// </summary>
    public IReadOnlyList<string?> Items => Properties.Values.ToList().AsReadOnly();

    /// <summary>
    /// Port of VBScript <c>GetProp(pKey)</c> on <c>DataViewLookupClassExtendable</c>.
    /// Retrieves a named property value by key, or null if not found.
    /// Legacy code:
    /// <code>
    /// public default function GetProp(pKey)
    ///     IF dictProperties.Exists(pKey) THEN
    ///         GetProp = dictProperties.Item(pKey)
    ///     Else
    ///         GetProp = ""
    ///     END IF
    /// end function
    /// </code>
    /// </summary>
    public string? GetProp(string pKey)
    {
        if (string.IsNullOrEmpty(pKey))
            return string.Empty;

        return Properties.TryGetValue(pKey, out var value) ? value : string.Empty;
    }

    /// <summary>
    /// Port of VBScript <c>SetProp(pKey, pValue)</c> on <c>DataViewLookupClassExtendable</c>.
    /// Sets a named property value by key.
    /// Legacy code:
    /// <code>
    /// public sub SetProp(pKey, pValue)
    ///     IF NOT dictProperties.Exists(pKey) THEN
    ///         dictProperties.Add pKey, pValue
    ///     Else
    ///         dictProperties.Item(pKey) = pValue
    ///     END IF
    /// end sub
    /// </code>
    /// </summary>
    public void SetProp(string pKey, string? pValue)
    {
        if (string.IsNullOrEmpty(pKey))
            return;

        Properties[pKey] = pValue;
    }
}
