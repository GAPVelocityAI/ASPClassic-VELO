using ASPClassic.Application.DTOs.Data;
using ASPClassic.Application.Services.Data;
using ASPClassic.Domain.Entities.Data;

namespace ASPClassic.Application.Services.Data;

/// <summary>
/// Port of <c>DataViewLookupClassExtendable</c> from inc_crudeconstants.asp.
/// A scoped, extensible property bag used during DataView field/action initialization.
/// Each instance is a dictionary of named properties (key→value) representing a single
/// DataView field or action configuration item.
///
/// In the legacy code this was a VBScript class with a Scripting.Dictionary.
/// In the modernized version, the actual data is held in <see cref="LookupItemDto"/>.
/// This interface provides async wrappers matching the plan's service method signatures,
/// plus synchronous accessors (GetProp/SetProp) matching the legacy class methods.
/// </summary>
public interface IDataViewLookupClassExtendable
{
    /// <summary>
    /// Returns the upper bound index of the properties (Count - 1), as a string.
    /// Port of VBScript <c>UBound</c> property.
    /// </summary>
    Task<string?> UBoundAsync(CancellationToken ct = default);

    /// <summary>
    /// Triggers population/refresh of the items listing.
    /// Port of VBScript <c>Items</c> property.
    /// </summary>
    Task ItemsAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves a named property value by key.
    /// Port of VBScript <c>GetProp(pKey)</c>.
    /// </summary>
    Task<string?> GetPropAsync(string pKey, CancellationToken ct = default);

    /// <summary>
    /// Sets a named property value by key.
    /// Port of VBScript <c>SetProp(pKey, pValue)</c>.
    /// </summary>
    void SetProp(string pKey, string? pValue);

    /// <summary>
    /// Gets the underlying <see cref="LookupItemDto"/> holding all properties.
    /// </summary>
    LookupItemDto GetItem();

    /// <summary>
    /// Synchronous property getter for direct access.
    /// Port of VBScript <c>GetProp(pKey)</c>.
    /// </summary>
    string? GetProp(string pKey);

    /// <summary>
    /// Returns the upper bound index (Count - 1).
    /// Synchronous version of <see cref="UBoundAsync"/>.
    /// </summary>
    int UBound { get; }

    /// <summary>
    /// Returns all property values.
    /// Synchronous version of <see cref="ItemsAsync"/>.
    /// </summary>
    IReadOnlyList<string?> Items { get; }
}
