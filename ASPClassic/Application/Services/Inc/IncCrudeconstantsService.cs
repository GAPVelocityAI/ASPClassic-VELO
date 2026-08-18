using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ASPClassic.Application.DTOs.Data;
using ASPClassic.Application.DTOs.Inc;
using ASPClassic.Domain.Entities.Data;
using ASPClassic.Infrastructure.Data;
using ASPClassic.Application.Services.Data;

namespace ASPClassic.Application.Services.Inc;

/// <summary>Port of <c>Inc_Crudeconstants</c> module from inc_crudeconstants.asp.
/// Provides initialization of DataView field/action metadata collections and flag lookups.
/// Also ports <c>DataViewLookupClassExtendable</c> (UBound, Items, GetProp, SetProp).</summary>
public class IncCrudeconstantsService : IIncCrudeconstantsService
{
    private readonly IDbContextFactory<ASPClassicVBScriptDbContext> _dbFactory;
    private readonly ILogger<IncCrudeconstantsService> _logger;

    // In-memory state mirroring the legacy global collections that were populated by
    // InitDataViewFields / InitDataViewActions and consumed by downstream rendering.
    private DataViewFieldCollectionDto _currentFields = new();
    private DataViewActionCollectionDto _currentActions = new();

    // Legacy DataViewLookupClassExtendable stored arbitrary key/value pairs.
    // We replicate that dictionary here for GetProp / SetProp.
    private readonly Dictionary<string, string> _propDictionary = new(StringComparer.OrdinalIgnoreCase);

    public IncCrudeconstantsService(
        IDbContextFactory<ASPClassicVBScriptDbContext> dbFactory,
        ILogger<IncCrudeconstantsService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public DataViewFieldCollectionDto GetCurrentFieldCollection() => _currentFields;

    /// <inheritdoc/>
    public DataViewActionCollectionDto GetCurrentActionCollection() => _currentActions;

    /// <summary>Port of <c>InitDataViewFields</c> — queries DataViewField for a ViewID,
    /// builds an ordered collection of field metadata. Legacy opened an ADO recordset on
    /// "SELECT * FROM DataViewField WHERE ViewID=@pViewID ORDER BY FieldOrder" and iterated
    /// each row into a DataViewLookupClassExtendable dictionary keyed by FieldID.
    /// The result is stored in _currentFields for subsequent access by InitDataViewFieldsJS,
    /// UBound, Items, GetProp.</summary>
    public async Task InitDataViewFieldsAsync(
        string pViewID, string pDBConnection, CancellationToken ct = default)
    {
        var result = new DataViewFieldCollectionDto();

        if (!int.TryParse(pViewID, CultureInfo.InvariantCulture, out var viewId))
        {
            _logger.LogWarning("InitDataViewFieldsAsync: invalid pViewID '{PViewID}'", pViewID);
            _currentFields = result;
            return;
        }

        result.ViewID = viewId;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Legacy: "SELECT * FROM portal.DataViewField WHERE ViewID=" & pViewID & " ORDER BY FieldOrder"
        var entities = await db.DataViewFields
            .AsNoTracking()
            .Where(f => f.ViewID == viewId)
            .OrderBy(f => f.FieldOrder)
            .ToListAsync(ct);

        // Legacy: Do While Not rs.EOF — iterated each row and called dvFields.AddItem to populate
        // a DataViewLookupCollectionClass, then for each field set properties via SetProp on
        // a DataViewLookupClassExtendable instance keyed by FieldID.
        foreach (var e in entities)
        {
            result.Fields.Add(MapFieldToJsColumn(e));

            // Populate the prop dictionary with field properties keyed as "FieldID_PropName"
            // This mirrors legacy's per-field property storage
            var prefix = e.FieldID.ToString(CultureInfo.InvariantCulture);
            _propDictionary[$"{prefix}_FieldLabel"] = e.FieldLabel ?? string.Empty;
            _propDictionary[$"{prefix}_FieldSource"] = e.FieldSource ?? string.Empty;
            _propDictionary[$"{prefix}_FieldType"] = e.FieldType ?? string.Empty;
            _propDictionary[$"{prefix}_FieldFlags"] = e.FieldFlags.ToString(CultureInfo.InvariantCulture);
            _propDictionary[$"{prefix}_FieldOrder"] = e.FieldOrder.ToString(CultureInfo.InvariantCulture);
            _propDictionary[$"{prefix}_DefaultValue"] = e.DefaultValue ?? string.Empty;
            _propDictionary[$"{prefix}_MaxLength"] = (e.MaxLength ?? 0).ToString(CultureInfo.InvariantCulture);
            _propDictionary[$"{prefix}_UriPath"] = e.UriPath ?? string.Empty;
            _propDictionary[$"{prefix}_UriStyle"] = (e.UriStyle ?? 0).ToString(CultureInfo.InvariantCulture);
            _propDictionary[$"{prefix}_LinkedTable"] = e.LinkedTable ?? string.Empty;
            _propDictionary[$"{prefix}_LinkedTableValueField"] = e.LinkedTableValueField ?? string.Empty;
            _propDictionary[$"{prefix}_LinkedTableTitleField"] = e.LinkedTableTitleField ?? string.Empty;
            _propDictionary[$"{prefix}_LinkedTableGroupField"] = e.LinkedTableGroupField ?? string.Empty;
            _propDictionary[$"{prefix}_LinkedTableGlyphField"] = e.LinkedTableGlyphField ?? string.Empty;
            _propDictionary[$"{prefix}_LinkedTableTooltipField"] = e.LinkedTableTooltipField ?? string.Empty;
            _propDictionary[$"{prefix}_LinkedTableAddition"] = e.LinkedTableAddition ?? string.Empty;
            _propDictionary[$"{prefix}_Width"] = (e.Width ?? 0).ToString(CultureInfo.InvariantCulture);
            _propDictionary[$"{prefix}_Height"] = (e.Height ?? 0).ToString(CultureInfo.InvariantCulture);
            _propDictionary[$"{prefix}_FieldDescription"] = e.FieldDescription ?? string.Empty;
            _propDictionary[$"{prefix}_FormatPattern"] = e.FormatPattern ?? string.Empty;
            _propDictionary[$"{prefix}_FieldTooltip"] = e.FieldTooltip ?? string.Empty;
            _propDictionary[$"{prefix}_FieldIdentifier"] = e.FieldIdentifier ?? string.Empty;
        }

        _currentFields = result;

        _logger.LogDebug("InitDataViewFieldsAsync: loaded {Count} fields for ViewID {ViewID}",
            result.Fields.Count, viewId);
    }

    /// <summary>Port of <c>InitDataViewActions</c> — queries DataViewAction for a ViewID.
    /// Legacy SQL: "SELECT * FROM DataViewAction WHERE ViewID=@pViewID [AND IsPerRow=1] ORDER BY ActionOrder"
    /// When pIsInline = "1", only per-row (inline) actions are returned.
    /// The result is stored in _currentActions for subsequent access.</summary>
    public async Task InitDataViewActionsAsync(
        string pViewID, string pIsInline, string pDBConnection, CancellationToken ct = default)
    {
        var result = new DataViewActionCollectionDto();

        if (!int.TryParse(pViewID, CultureInfo.InvariantCulture, out var viewId))
        {
            _logger.LogWarning("InitDataViewActionsAsync: invalid pViewID '{PViewID}'", pViewID);
            _currentActions = result;
            return;
        }

        result.ViewID = viewId;
        bool filterInline = string.Equals(pIsInline, "1", StringComparison.OrdinalIgnoreCase);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Legacy: "SELECT * FROM portal.DataViewAction WHERE ViewID=" & pViewID
        //         & " AND IsPerRow=1" (when pIsInline = "1")
        //         & " ORDER BY ActionOrder"
        IQueryable<DataViewAction> query = db.DataViewActions
            .AsNoTracking()
            .Where(a => a.ViewID == viewId);

        if (filterInline)
        {
            query = query.Where(a => a.IsPerRow);
        }

        var entities = await query
            .OrderBy(a => a.ActionOrder)
            .ToListAsync(ct);

        // Legacy: Do While Not rs.EOF — iterated each row and called dvActions.AddItem
        // to populate a DataViewLookupCollectionClass, then for each action set properties.
        foreach (var e in entities)
        {
            result.Actions.Add(MapActionToButton(e));

            // Populate the prop dictionary with action properties keyed as "ActionID_PropName"
            var prefix = "action_" + e.ActionID.ToString(CultureInfo.InvariantCulture);
            _propDictionary[$"{prefix}_ActionLabel"] = e.ActionLabel ?? string.Empty;
            _propDictionary[$"{prefix}_ActionExpression"] = e.ActionExpression ?? string.Empty;
            _propDictionary[$"{prefix}_GlyphIcon"] = e.GlyphIcon ?? string.Empty;
            _propDictionary[$"{prefix}_ActionTooltip"] = e.ActionTooltip ?? string.Empty;
            _propDictionary[$"{prefix}_CSSButton"] = e.CSSButton ?? string.Empty;
            _propDictionary[$"{prefix}_ActionType"] = e.ActionType ?? string.Empty;
            _propDictionary[$"{prefix}_RequireConfirmation"] = e.RequireConfirmation ? "1" : "0";
            _propDictionary[$"{prefix}_IsPerRow"] = e.IsPerRow ? "1" : "0";
        }

        _currentActions = result;

        _logger.LogDebug("InitDataViewActionsAsync: loaded {Count} actions for ViewID {ViewID}, inline={Inline}",
            result.Actions.Count, viewId, filterInline);
    }

    /// <summary>Port of <c>InitDataViewFieldsJS</c> — legacy iterated the dvFields collection
    /// (a DataViewLookupCollectionClass) and emitted a JS array of column definition objects.
    /// In Blazor we populate _currentFields with the structured DTOs. The dvFields parameter
    /// is treated as a comma-separated list of FieldIDs or a single ViewID to look up.</summary>
    public async Task InitDataViewFieldsJSAsync(
        string dvFields, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dvFields))
        {
            return;
        }

        // If _currentFields already has data (populated by InitDataViewFieldsAsync), use it directly.
        // Legacy: InitDataViewFieldsJS iterated the already-populated dvFields collection.
        if (_currentFields.Fields.Count > 0)
        {
            _logger.LogDebug("InitDataViewFieldsJSAsync: using {Count} already-loaded fields", _currentFields.Fields.Count);
            return;
        }

        // Fallback: load from DB if not yet initialized
        var fieldIds = new List<int>();
        foreach (var token in dvFields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(token, CultureInfo.InvariantCulture, out var fid))
            {
                fieldIds.Add(fid);
            }
        }

        if (fieldIds.Count == 0)
        {
            // If dvFields is a single ViewID, load all fields for that view
            if (int.TryParse(dvFields.Trim(), CultureInfo.InvariantCulture, out var viewId))
            {
                await using var db = await _dbFactory.CreateDbContextAsync(ct);
                var entities = await db.DataViewFields
                    .AsNoTracking()
                    .Where(f => f.ViewID == viewId)
                    .OrderBy(f => f.FieldOrder)
                    .ToListAsync(ct);

                var collection = new DataViewFieldCollectionDto { ViewID = viewId };
                foreach (var e in entities)
                {
                    collection.Fields.Add(MapFieldToJsColumn(e));
                }
                _currentFields = collection;
            }
            return;
        }

        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var entities = await db.DataViewFields
                .AsNoTracking()
                .Where(f => fieldIds.Contains(f.FieldID))
                .OrderBy(f => f.FieldOrder)
                .ToListAsync(ct);

            var collection = new DataViewFieldCollectionDto();
            foreach (var e in entities)
            {
                collection.Fields.Add(MapFieldToJsColumn(e));
            }
            _currentFields = collection;
        }

        _logger.LogDebug("InitDataViewFieldsJSAsync: loaded {Count} column definitions", _currentFields.Fields.Count);
    }

    /// <summary>Port of <c>InitDataViewInlineActionButtonsJS</c> — legacy iterated the
    /// dvActionsInline collection and emitted a JS array of inline action button objects.
    /// In Blazor we populate _currentActions. The dvActionsInline parameter is treated as a
    /// comma-separated list of ActionIDs or a single ViewID.</summary>
    public async Task InitDataViewInlineActionButtonsJSAsync(
        string dvActionsInline, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dvActionsInline))
        {
            return;
        }

        // If _currentActions already has data, use it directly.
        if (_currentActions.Actions.Count > 0)
        {
            _logger.LogDebug("InitDataViewInlineActionButtonsJSAsync: using {Count} already-loaded actions", _currentActions.Actions.Count);
            return;
        }

        var actionIds = new List<int>();
        foreach (var token in dvActionsInline.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (int.TryParse(token, CultureInfo.InvariantCulture, out var aid))
            {
                actionIds.Add(aid);
            }
        }

        if (actionIds.Count == 0)
        {
            // Fallback: treat as single ViewID and load inline actions for that view
            if (int.TryParse(dvActionsInline.Trim(), CultureInfo.InvariantCulture, out var viewId))
            {
                await using var db = await _dbFactory.CreateDbContextAsync(ct);
                var entities = await db.DataViewActions
                    .AsNoTracking()
                    .Where(a => a.ViewID == viewId && a.IsPerRow)
                    .OrderBy(a => a.ActionOrder)
                    .ToListAsync(ct);

                var collection = new DataViewActionCollectionDto { ViewID = viewId };
                foreach (var e in entities)
                {
                    collection.Actions.Add(MapActionToButton(e));
                }
                _currentActions = collection;
            }
            return;
        }

        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);

            var entities = await db.DataViewActions
                .AsNoTracking()
                .Where(a => actionIds.Contains(a.ActionID))
                .OrderBy(a => a.ActionOrder)
                .ToListAsync(ct);

            var collection = new DataViewActionCollectionDto();
            foreach (var e in entities)
            {
                collection.Actions.Add(MapActionToButton(e));
            }
            _currentActions = collection;
        }

        _logger.LogDebug("InitDataViewInlineActionButtonsJSAsync: loaded {Count} button definitions", _currentActions.Actions.Count);
    }

    /// <summary>Port of <c>DataViewLookupClassExtendable.UBound</c> — returns the upper bound
    /// (count - 1) of the currently loaded fields collection. Legacy: Property Get UBound
    /// returned UBound(m_items) which was the last index of the internal array.</summary>
    public Task<string?> UBoundAsync(CancellationToken ct = default)
    {
        int count = _currentFields.Fields.Count;
        string result = count > 0
            ? (count - 1).ToString(CultureInfo.InvariantCulture)
            : "-1";
        return Task.FromResult<string?>(result);
    }

    /// <summary>Port of <c>DataViewLookupClassExtendable.Items</c> — legacy returned the
    /// internal items array. In Blazor this is a no-op since callers access the collection
    /// directly via GetCurrentFieldCollection(). Provided for interface compliance.</summary>
    public Task ItemsAsync(CancellationToken ct = default)
    {
        // Legacy: Property Get Items returned m_items array.
        // In Blazor, callers use GetCurrentFieldCollection() or GetCurrentActionCollection().
        _logger.LogDebug("ItemsAsync: current fields count={FieldCount}, actions count={ActionCount}",
            _currentFields.Fields.Count, _currentActions.Actions.Count);
        return Task.CompletedTask;
    }

    /// <summary>Port of <c>DataViewLookupClassExtendable.GetProp</c> — retrieves a named
    /// property from the internal dictionary. Legacy: Public Function GetProp(pKey)
    /// returned m_dict(pKey) from a Scripting.Dictionary.</summary>
    public Task<string?> GetPropAsync(string pKey, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(pKey))
        {
            return Task.FromResult<string?>(null);
        }

        _propDictionary.TryGetValue(pKey, out var value);
        return Task.FromResult<string?>(value);
    }

    /// <summary>Port of <c>DataViewLookupClassExtendable.SetProp</c> — stores a named
    /// property in the internal dictionary. Legacy: Public Sub SetProp(pKey, pValue)
    /// set m_dict(pKey) = pValue in a Scripting.Dictionary.</summary>
    public Task SetPropAsync(string pKey, string pValue, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(pKey))
        {
            _propDictionary[pKey] = pValue ?? string.Empty;
        }
        return Task.CompletedTask;
    }

    /// <summary>Port of legacy getter — returns the first DataViewField found in the database.
    /// Legacy: used to retrieve a sample/default field record.</summary>
    public async Task<DataViewFieldDto?> GetDataViewFieldAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.DataViewFields
            .AsNoTracking()
            .OrderBy(f => f.FieldID)
            .FirstOrDefaultAsync(ct);

        if (entity is null)
            return null;

        return MapFieldToDto(entity);
    }

    /// <summary>Port of legacy getter — returns the first DataViewAction found in the database.
    /// Legacy: used to retrieve a sample/default action record.</summary>
    public async Task<DataViewActionDto?> GetDataViewActionAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.DataViewActions
            .AsNoTracking()
            .OrderBy(a => a.ActionID)
            .FirstOrDefaultAsync(ct);

        if (entity is null)
            return null;

        return MapActionToDto(entity);
    }

    /// <summary>Port of legacy getter — returns the first DataViewFlags record.
    /// Legacy: loaded the flags lookup table to provide flag definitions for the UI.</summary>
    public async Task<DataViewFlagsDto?> GetDataViewFlagsAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.DataViewFlags
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (entity is null)
            return null;

        return new DataViewFlagsDto
        {
            FlagValue = entity.FlagValue ?? string.Empty,
            FlagLabel = entity.FlagLabel ?? string.Empty,
            FlagGlyph = entity.FlagGlyph ?? string.Empty,
            FlagDefault = entity.FlagDefault ?? string.Empty
        };
    }

    /// <summary>Port of the top-level script block in inc_crudeconstants.asp.
    /// Legacy: this file was included at the top of many pages and ran initialization code
    /// that loaded DataView flag constants into global scope. The BreadCrumbCollection in
    /// state was also initialized here. In Blazor, we load flags from DB and return them.</summary>
    public async Task<DataViewFlagsDto?> LoadIncCrudeconstantsAsync(
        IncCrudeconstantsStateDto state, CancellationToken ct = default)
    {
        // Legacy top-level code in inc_crudeconstants.asp:
        // 1. Included constants definitions (DV_FLAG_*, DVFIELD_FLAG_*, etc.)
        // 2. Initialized breadcrumb collection: Dim dvBreadCrumbCollection
        // 3. Loaded flag lookups from database for use by downstream rendering

        if (state != null && string.IsNullOrEmpty(state.BreadCrumbCollection))
        {
            state.BreadCrumbCollection = string.Empty;
        }

        _logger.LogDebug("LoadIncCrudeconstantsAsync: initializing crudeconstants with breadcrumb state");

        // Load the flags — the primary output of this initialization
        var flags = await GetDataViewFlagsAsync(ct);

        return flags;
    }

    // ─── Private mapping helpers ───────────────────────────────────────────────

    /// <summary>Maps a DataViewField entity to a DataViewFieldJsColumnDto.
    /// Replicates the legacy loop body in InitDataViewFields that stored each field's
    /// properties into a DataViewLookupClassExtendable dictionary.</summary>
    private static DataViewFieldJsColumnDto MapFieldToJsColumn(DataViewField e)
    {
        return new DataViewFieldJsColumnDto
        {
            FieldID = e.FieldID,
            ViewID = e.ViewID,
            FieldLabel = e.FieldLabel ?? string.Empty,
            FieldSource = e.FieldSource ?? string.Empty,
            FieldType = e.FieldType ?? string.Empty,
            FieldFlags = e.FieldFlags,
            FieldOrder = e.FieldOrder,
            DefaultValue = e.DefaultValue ?? string.Empty,
            MaxLength = e.MaxLength ?? 0,
            UriPath = e.UriPath ?? string.Empty,
            UriStyle = e.UriStyle ?? 0,
            LinkedTable = e.LinkedTable ?? string.Empty,
            LinkedTableValueField = e.LinkedTableValueField ?? string.Empty,
            LinkedTableTitleField = e.LinkedTableTitleField ?? string.Empty,
            LinkedTableGroupField = e.LinkedTableGroupField ?? string.Empty,
            LinkedTableGlyphField = e.LinkedTableGlyphField ?? string.Empty,
            LinkedTableTooltipField = e.LinkedTableTooltipField ?? string.Empty,
            LinkedTableAddition = e.LinkedTableAddition ?? string.Empty,
            Width = e.Width ?? 0,
            Height = e.Height ?? 0,
            FieldDescription = e.FieldDescription ?? string.Empty,
            FormatPattern = e.FormatPattern ?? string.Empty,
            FieldTooltip = e.FieldTooltip ?? string.Empty,
            FieldIdentifier = e.FieldIdentifier ?? string.Empty
        };
    }

    /// <summary>Maps a DataViewField entity to the plan DTO (DataViewFieldDto).
    /// Used by GetDataViewFieldAsync.</summary>
    private static DataViewFieldDto MapFieldToDto(DataViewField e)
    {
        return new DataViewFieldDto
        {
            ViewID = e.ViewID,
            FieldID = e.FieldID,
            FieldLabel = e.FieldLabel ?? string.Empty,
            FieldSource = e.FieldSource ?? string.Empty,
            FieldType = e.FieldType ?? string.Empty,
            FieldFlags = e.FieldFlags,
            FieldOrder = e.FieldOrder,
            DefaultValue = e.DefaultValue ?? string.Empty,
            MaxLength = e.MaxLength ?? 0,
            UriPath = e.UriPath ?? string.Empty,
            UriStyle = e.UriStyle ?? 0,
            LinkedTable = e.LinkedTable ?? string.Empty,
            LinkedTableValueField = e.LinkedTableValueField ?? string.Empty,
            LinkedTableTitleField = e.LinkedTableTitleField ?? string.Empty,
            LinkedTableGroupField = e.LinkedTableGroupField ?? string.Empty,
            LinkedTableGlyphField = e.LinkedTableGlyphField ?? string.Empty,
            LinkedTableTooltipField = e.LinkedTableTooltipField ?? string.Empty,
            LinkedTableAddition = e.LinkedTableAddition ?? string.Empty,
            Width = e.Width ?? 0,
            Height = e.Height ?? 0,
            FieldDescription = e.FieldDescription ?? string.Empty,
            FormatPattern = e.FormatPattern ?? string.Empty,
            FieldTooltip = e.FieldTooltip ?? string.Empty,
            FieldIdentifier = e.FieldIdentifier ?? string.Empty
        };
    }

    /// <summary>Maps a DataViewAction entity to a DataViewInlineActionButtonDto.
    /// Replicates the legacy loop body in InitDataViewActions that stored each action's
    /// properties into a DataViewLookupClassExtendable dictionary.</summary>
    private static DataViewInlineActionButtonDto MapActionToButton(DataViewAction e)
    {
        return new DataViewInlineActionButtonDto
        {
            ActionID = e.ActionID,
            ViewID = e.ViewID,
            ActionLabel = e.ActionLabel ?? string.Empty,
            ParentActionID = e.ParentActionID,
            ActionTooltip = e.ActionTooltip ?? string.Empty,
            ActionDescription = e.ActionDescription ?? string.Empty,
            ActionOrder = e.ActionOrder,
            RequireConfirmation = e.RequireConfirmation,
            OpenURLInNewWindow = e.OpenURLInNewWindow ?? false,
            ActionExpression = e.ActionExpression ?? string.Empty,
            GlyphIcon = e.GlyphIcon ?? string.Empty,
            IsPerRow = e.IsPerRow,
            CSSButton = e.CSSButton ?? string.Empty,
            ActionType = e.ActionType ?? string.Empty,
            DataViewTitle = e.DataViewTitle ?? string.Empty
        };
    }

    /// <summary>Maps a DataViewAction entity to the plan DTO (DataViewActionDto).
    /// Used by GetDataViewActionAsync.</summary>
    private static DataViewActionDto MapActionToDto(DataViewAction e)
    {
        return new DataViewActionDto
        {
            ActionID = e.ActionID,
            ViewID = e.ViewID,
            ActionLabel = e.ActionLabel ?? string.Empty,
            ParentActionID = e.ParentActionID ?? 0,
            ActionTooltip = e.ActionTooltip ?? string.Empty,
            ActionDescription = e.ActionDescription ?? string.Empty,
            ActionOrder = e.ActionOrder,
            RequireConfirmation = e.RequireConfirmation,
            OpenURLInNewWindow = e.OpenURLInNewWindow ?? false,
            ActionExpression = e.ActionExpression ?? string.Empty,
            GlyphIcon = e.GlyphIcon ?? string.Empty,
            IsPerRow = e.IsPerRow,
            CSSButton = e.CSSButton ?? string.Empty,
            ActionType = e.ActionType ?? string.Empty,
            DataViewTitle = e.DataViewTitle ?? string.Empty
        };
    }
}
