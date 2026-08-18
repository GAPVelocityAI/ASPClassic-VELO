using ASPClassic.Application.DTOs.Data;
using ASPClassic.Domain.Entities.Data;
using ASPClassic.Application.Services.Data;

namespace ASPClassic.Application.Services.Data;

/// <summary>
/// Port of <c>DataViewLookupCollectionClass</c> from inc_crudeconstants.asp.
/// A scoped, keyed collection of lookup items used during DataView rendering.
/// Each item is a dictionary of named properties (mirroring DataViewLookupClassExtendable instances).
/// This is a request-scoped in-memory structure populated by IncCrudeconstantsService
/// and consumed by DataView rendering pages within the same request/circuit call.
/// </summary>
public interface IDataViewLookupCollectionClass
{
    /// <summary>
    /// Returns the upper bound index of the collection (Count - 1), as a string.
    /// Port of VBScript <c>UBound</c> property.
    /// </summary>
    Task<string?> UBoundAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns a serialized representation of all items in the collection (JSON array of keys).
    /// Port of VBScript <c>Items</c> property which returned the dictionary's Items collection.
    /// </summary>
    Task<string?> ItemsAsync(CancellationToken ct = default);

    /// <summary>
    /// Populates the internal keys listing. In the legacy code this returned the Keys array.
    /// In the modernized version, this is a no-op trigger that ensures keys are available;
    /// actual key data is maintained automatically as items are added.
    /// Port of VBScript <c>Keys</c> property.
    /// </summary>
    Task KeysAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves a lookup item by its key.
    /// Port of VBScript <c>GetItem</c> method.
    /// </summary>
    Task GetItemAsync(string pKey, CancellationToken ct = default);

    /// <summary>
    /// Adds a lookup item to the collection under the specified key.
    /// If the key already exists, the item is replaced.
    /// Port of VBScript <c>AddItem</c> method.
    /// </summary>
    void AddItem(string key, LookupItemDto item);

    /// <summary>
    /// Gets the current count of items in the collection.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Retrieves a lookup item by key, returning the full DTO or null if not found.
    /// Convenience accessor for callers that need the typed result.
    /// </summary>
    LookupItemDto? GetItemByKey(string key);

    /// <summary>
    /// Returns all keys currently in the collection.
    /// </summary>
    IReadOnlyList<string> GetAllKeys();

    /// <summary>
    /// Returns all items currently in the collection.
    /// </summary>
    IReadOnlyList<LookupItemDto> GetAllItems();
}
