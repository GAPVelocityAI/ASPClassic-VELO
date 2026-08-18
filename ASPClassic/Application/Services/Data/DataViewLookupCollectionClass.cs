using System.Text.Json;
using ASPClassic.Application.DTOs.Data;
using Microsoft.Extensions.Logging;
using ASPClassic.Application.Services.Data;

namespace ASPClassic.Application.Services.Data;

/// <summary>
/// Port of <c>DataViewLookupCollectionClass</c> from inc_crudeconstants.asp.
/// A scoped, keyed collection of <see cref="LookupItemDto"/> instances.
///
/// Legacy VBScript class held a Scripting.Dictionary of DataViewLookupClassExtendable objects.
/// This modernized version maintains an equivalent Dictionary&lt;string, LookupItemDto&gt;
/// within the scoped request lifetime. Other services (IncCrudeconstantsService) call
/// <see cref="AddItem"/> to populate it, and rendering pages call <see cref="GetItemByKey"/>
/// or <see cref="GetItemAsync"/> to retrieve items.
///
/// This does NOT use DbContextFactory because the legacy class was purely in-memory
/// request-scoped state — it never touched the database directly. The data it holds
/// is loaded FROM the database by other services and placed here for convenient
/// keyed access during a single rendering cycle.
/// </summary>
public class DataViewLookupCollectionClass : IDataViewLookupCollectionClass
{
    private readonly ILogger<DataViewLookupCollectionClass> _logger;

    /// <summary>
    /// Internal dictionary holding all items keyed by their string key.
    /// Mirrors the legacy <c>Scripting.Dictionary</c> used in VBScript.
    /// Case-insensitive keys to match VBScript dictionary behavior.
    /// </summary>
    private readonly Dictionary<string, LookupItemDto> _items = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Ordered list of keys preserving insertion order, mirroring legacy behavior
    /// where items were iterated in the order they were added.
    /// </summary>
    private readonly List<string> _orderedKeys = new();

    /// <summary>
    /// The last retrieved item, set by <see cref="GetItemAsync"/> for callers
    /// that read the result from a shared state pattern (legacy VBScript pattern
    /// where GetItem set a module-level variable).
    /// </summary>
    private LookupItemDto? _lastRetrievedItem;

    public DataViewLookupCollectionClass(ILogger<DataViewLookupCollectionClass> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public int Count => _items.Count;

    /// <summary>
    /// Port of VBScript: <c>UBound = Count - 1</c>.
    /// Returns the upper bound index as a string, or "-1" if the collection is empty.
    /// </summary>
    public async Task<string?> UBoundAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        int upperBound = _items.Count - 1;
        _logger.LogDebug("UBoundAsync called. Count={Count}, UBound={UBound}", _items.Count, upperBound);
        return await Task.FromResult<string?>(upperBound.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Port of VBScript: <c>SET Items = dictProperties.Items</c>.
    /// Returns a JSON-serialized array of all keys in the collection.
    /// The legacy code returned the dictionary's Items collection (an array of values);
    /// here we return a JSON representation of the keys so callers can enumerate them.
    /// </summary>
    public async Task<string?> ItemsAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (_items.Count == 0)
        {
            _logger.LogDebug("ItemsAsync called on empty collection, returning null.");
            return await Task.FromResult<string?>(null);
        }

        // Serialize all keys as a JSON array — callers used the legacy Items array
        // to iterate over the collection entries; the keys serve the same purpose
        // since GetItemByKey can then retrieve each one.
        var serialized = JsonSerializer.Serialize(_orderedKeys);
        _logger.LogDebug("ItemsAsync returning {Count} items.", _items.Count);
        return await Task.FromResult<string?>(serialized);
    }

    /// <summary>
    /// Port of VBScript: <c>Keys</c> property.
    /// In legacy code this returned the dictionary's Keys array.
    /// Here, keys are maintained automatically as items are added.
    /// This method ensures the ordered keys list is consistent.
    /// </summary>
    public async Task KeysAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // Reconcile _orderedKeys with _items in case of any replacement operations
        // that might have caused drift. In practice AddItem maintains both, but
        // this mirrors the legacy pattern where accessing .Keys was an explicit operation.
        var currentKeys = _items.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Remove any orphaned keys (keys in ordered list but not in dictionary)
        _orderedKeys.RemoveAll(k => !currentKeys.Contains(k));

        // Add any missing keys (keys in dictionary but not in ordered list)
        foreach (var key in _items.Keys)
        {
            if (!_orderedKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                _orderedKeys.Add(key);
            }
        }

        _logger.LogDebug("KeysAsync reconciled. Total keys: {Count}", _orderedKeys.Count);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Port of VBScript: <c>GetItem(pKey)</c>.
    /// Retrieves the item associated with the given key.
    /// Sets internal <c>_lastRetrievedItem</c> so callers using the legacy
    /// pattern of reading a module-level variable after calling GetItem can
    /// use <see cref="GetItemByKey"/> instead.
    ///
    /// Legacy code:
    /// <code>
    /// public default function GetItem(pKey)
    ///     IF NOT dictProperties.Exists(pKey) THEN
    ///         SET GetItem = Nothing
    ///     Else
    ///         SET GetItem = dictProperties.Item(pKey)
    ///     END IF
    /// end function
    /// </code>
    /// </summary>
    public async Task GetItemAsync(string pKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(pKey))
        {
            _logger.LogWarning("GetItemAsync called with null/empty key.");
            _lastRetrievedItem = null;
            await Task.CompletedTask;
            return;
        }

        if (_items.TryGetValue(pKey, out var item))
        {
            _lastRetrievedItem = item;
            _logger.LogDebug("GetItemAsync found item for key '{Key}' with {PropCount} properties.",
                pKey, item.Properties.Count);
        }
        else
        {
            _lastRetrievedItem = null;
            _logger.LogDebug("GetItemAsync: key '{Key}' not found in collection of {Count} items.",
                pKey, _items.Count);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Port of VBScript: <c>AddItem(pKey, pItem)</c>.
    /// Adds a lookup item to the collection. If the key already exists, replaces the item.
    ///
    /// Legacy code:
    /// <code>
    /// public sub AddItem(pKey, pItem)
    ///     IF NOT dictProperties.Exists(pKey) THEN
    ///         SET dictProperties.Item(pKey) = pItem
    ///         Count = Count + 1
    ///     Else
    ///         SET dictProperties.Item(pKey) = pItem
    ///     END IF
    /// end sub
    /// </code>
    /// </summary>
    public void AddItem(string key, LookupItemDto item)
    {
        if (string.IsNullOrEmpty(key))
        {
            _logger.LogWarning("AddItem called with null/empty key; ignoring.");
            return;
        }

        ArgumentNullException.ThrowIfNull(item);

        if (!_items.ContainsKey(key))
        {
            // New key — add to both dictionary and ordered list
            _items[key] = item;
            _orderedKeys.Add(key);
            _logger.LogDebug("AddItem: added new item with key '{Key}'. Count={Count}", key, _items.Count);
        }
        else
        {
            // Existing key — replace the item (legacy did SET dictProperties.Item(pKey) = pItem)
            _items[key] = item;
            _logger.LogDebug("AddItem: replaced existing item with key '{Key}'. Count={Count}", key, _items.Count);
        }

        // Ensure the item's own Key property is consistent
        item.Key = key;
    }

    /// <inheritdoc />
    public LookupItemDto? GetItemByKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return null;
        }

        _items.TryGetValue(key, out var item);
        return item;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetAllKeys()
    {
        return _orderedKeys.AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<LookupItemDto> GetAllItems()
    {
        var result = new List<LookupItemDto>(_orderedKeys.Count);
        foreach (var key in _orderedKeys)
        {
            if (_items.TryGetValue(key, out var item))
            {
                result.Add(item);
            }
        }
        return result.AsReadOnly();
    }
}
