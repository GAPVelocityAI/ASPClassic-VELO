using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ASPClassic.Infrastructure.Data;
using ASPClassic.Application.DTOs.Data;

namespace ASPClassic.Application.Services.Data;

/// <summary>Port of <c>DataViewLookupClassExtendable</c> (VBScript class from inc_crudeconstants.asp).
/// Implements an in-memory key-value property bag that mirrors the legacy Scripting.Dictionary wrapper.
/// Registered as Scoped — one instance per Blazor circuit/request, matching the legacy ASP
/// request-scoped Class instantiation pattern.</summary>
public class DataViewLookupClassExtendable : IDataViewLookupClassExtendable
{
    private readonly IDbContextFactory<ASPClassicVBScriptDbContext> _dbFactory;
    private readonly ILogger<DataViewLookupClassExtendable> _logger;

    /// <summary>The backing dictionary, equivalent to the legacy dictProperties (Scripting.Dictionary).
    /// Key comparison is case-insensitive to match VBScript's default dictionary behavior.</summary>
    private readonly Dictionary<string, string> _dictProperties;

    /// <summary>Tracks the number of properties, matching the legacy Count property.
    /// In legacy code, Count was incremented on SetProp for new keys only.</summary>
    private int _count;

    public DataViewLookupClassExtendable(
        IDbContextFactory<ASPClassicVBScriptDbContext> dbFactory,
        ILogger<DataViewLookupClassExtendable> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;

        // Legacy Class_Initialize: Set dictProperties = Server.CreateObject("Scripting.Dictionary")
        // VBScript Scripting.Dictionary is case-insensitive by default (CompareMode = vbTextCompare after init)
        _dictProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _count = 0;
    }

    /// <inheritdoc />
    public int Count => _count;

    /// <summary>Port of legacy VBScript property:
    /// <code>
    /// public property Get UBound
    ///     UBound = Count - 1
    /// end property
    /// </code>
    /// Returns the upper bound index as a string (legacy returned Variant coerced to string).</summary>
    public async Task<string?> UBoundAsync(CancellationToken ct = default)
    {
        // Legacy: UBound = Count - 1
        int upperBound = _count - 1;
        string? result = upperBound.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _logger.LogDebug("UBoundAsync called. Count={Count}, UBound={UBound}", _count, result);
        return await Task.FromResult(result);
    }

    /// <summary>Port of legacy VBScript property:
    /// <code>
    /// public property Get UBound
    ///     UBound = Count - 1
    /// end property
    /// </code>
    /// Synchronous version returning integer upper bound.</summary>
    public int UBound()
    {
        int upperBound = _count - 1;
        _logger.LogDebug("UBound called. Count={Count}, UBound={UBound}", _count, upperBound);
        return upperBound;
    }

    /// <summary>Port of legacy VBScript property:
    /// <code>
    /// public property Get Items
    ///     SET Items = dictProperties.Items
    /// end property
    /// </code>
    /// In legacy code this returned the dictionary's Items collection (array of values).
    /// Since the return type is Task (void), this method logs the items for diagnostics.
    /// Callers that need the actual items use <see cref="Items"/> or <see cref="GetAllProperties"/>.</summary>
    public async Task ItemsAsync(CancellationToken ct = default)
    {
        // Legacy: SET Items = dictProperties.Items
        _logger.LogDebug("ItemsAsync called. Current property count={Count}, Keys=[{Keys}]",
            _count,
            string.Join(", ", _dictProperties.Keys));

        await Task.CompletedTask;
    }

    /// <summary>Port of legacy VBScript property:
    /// <code>
    /// public property Get Items
    ///     SET Items = dictProperties.Items
    /// end property
    /// </code>
    /// Synchronous version returning a read-only dictionary snapshot.</summary>
    public IReadOnlyDictionary<string, string> Items()
    {
        _logger.LogDebug("Items called. Current property count={Count}", _count);
        return new Dictionary<string, string>(_dictProperties, _dictProperties.Comparer);
    }

    /// <summary>Port of legacy VBScript function:
    /// <code>
    /// public default function GetProp(pKey)
    ///     IF NOT dictProperties.Exists(pKey) THEN
    ///         GetProp = Null
    ///     Else
    ///         GetProp = dictProperties.Item(pKey)
    ///     END IF
    /// end function
    /// </code>
    /// Async version.</summary>
    public async Task<string?> GetPropAsync(string pKey, CancellationToken ct = default)
    {
        string? result = GetProp(pKey);
        return await Task.FromResult(result);
    }

    /// <summary>Port of legacy VBScript function:
    /// <code>
    /// public default function GetProp(pKey)
    ///     IF NOT dictProperties.Exists(pKey) THEN
    ///         GetProp = Null
    ///     Else
    ///         GetProp = dictProperties.Item(pKey)
    ///     END IF
    /// end function
    /// </code>
    /// Synchronous version.</summary>
    public string? GetProp(string pKey)
    {
        if (string.IsNullOrEmpty(pKey))
        {
            _logger.LogWarning("GetProp called with null or empty key");
            return null;
        }

        // Legacy: IF NOT dictProperties.Exists(pKey) THEN GetProp = Null
        if (!_dictProperties.TryGetValue(pKey, out string? value))
        {
            _logger.LogDebug("GetProp: key '{Key}' not found, returning null", pKey);
            return null;
        }

        // Legacy: GetProp = dictProperties.Item(pKey)
        _logger.LogDebug("GetProp: key '{Key}' found, value='{Value}'", pKey, value);
        return value;
    }

    /// <summary>Port of legacy VBScript sub:
    /// <code>
    /// public sub SetProp(pKey, pItem)
    ///     IF NOT dictProperties.Exists(pKey) THEN
    ///         dictProperties.Add pKey, pItem
    ///         Count = Count + 1
    ///     Else
    ///         SET dictProperties.Item(pKey) = pItem
    ///     END IF
    /// end sub
    /// </code>
    /// Async version.</summary>
    public async Task SetPropAsync(string pKey, string pItem, CancellationToken ct = default)
    {
        SetProp(pKey, pItem);
        await Task.CompletedTask;
    }

    /// <summary>Port of legacy VBScript sub:
    /// <code>
    /// public sub SetProp(pKey, pItem)
    ///     IF NOT dictProperties.Exists(pKey) THEN
    ///         dictProperties.Add pKey, pItem
    ///         Count = Count + 1
    ///     Else
    ///         SET dictProperties.Item(pKey) = pItem
    ///     END IF
    /// end sub
    /// </code>
    /// Synchronous version.</summary>
    public void SetProp(string pKey, string pItem)
    {
        if (string.IsNullOrEmpty(pKey))
        {
            _logger.LogWarning("SetProp called with null or empty key, ignoring");
            return;
        }

        // Legacy: IF NOT dictProperties.Exists(pKey) THEN
        if (!_dictProperties.ContainsKey(pKey))
        {
            // Legacy: dictProperties.Add pKey, pItem
            _dictProperties.Add(pKey, pItem);
            // Legacy: Count = Count + 1
            _count++;
            _logger.LogDebug("SetProp: added new key '{Key}' with value '{Value}'. Count={Count}",
                pKey, pItem, _count);
        }
        else
        {
            // Legacy: SET dictProperties.Item(pKey) = pItem
            _dictProperties[pKey] = pItem;
            _logger.LogDebug("SetProp: updated existing key '{Key}' with value '{Value}'. Count={Count}",
                pKey, pItem, _count);
        }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> GetAllProperties()
    {
        // Returns a snapshot copy so callers cannot mutate internal state
        return new Dictionary<string, string>(_dictProperties, _dictProperties.Comparer);
    }

    /// <inheritdoc />
    public void Clear()
    {
        _dictProperties.Clear();
        _count = 0;
        _logger.LogDebug("Clear called. All properties removed, count reset to 0");
    }

    // ── Interface surface ──────────────────────────────────────────────
    // The interface states these as properties, matching the VBScript class it ports; the methods
    // above grew alongside and are what the rest of this file already calls. Implemented explicitly
    // so both spellings work and neither side has to be rewritten.

    int IDataViewLookupClassExtendable.UBound => UBound();

    IReadOnlyList<string?> IDataViewLookupClassExtendable.Items =>
        Items().Values.Cast<string?>().ToList();

    LookupItemDto IDataViewLookupClassExtendable.GetItem() => new()
    {
        Key = string.Empty,
        Properties = Items().ToDictionary(kv => kv.Key, kv => (string?)kv.Value, StringComparer.OrdinalIgnoreCase),
    };
}
