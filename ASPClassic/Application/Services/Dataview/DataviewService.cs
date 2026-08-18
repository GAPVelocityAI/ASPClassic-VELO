using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ASPClassic.Application.DTOs.Data;
using ASPClassic.Application.DTOs.Dataview;
using ASPClassic.Infrastructure.Data;
using ASPClassic.Domain.Entities.Data;

namespace ASPClassic.Application.Services.Dataview;

/// <summary>Port of <c>dataview.asp</c> — DataView loading, metadata retrieval, and server-side data table queries.</summary>
public class DataviewService : IDataviewService
{
    private readonly IDbContextFactory<ASPClassicVBScriptDbContext> _dbFactory;
    private readonly ILogger<DataviewService> _logger;
    private readonly ASPClassic.Infrastructure.Engines.DataViewQueryEngine? _queryEngine;

    /// <summary>
    /// Rules that must hold before a record of a particular table is written.
    /// </summary>
    /// <remarks>
    /// The writer edits whatever table a view names and knows nothing about what any of them mean —
    /// that is the design. Rather than teach it about one table at a time, it asks whether any rule
    /// claims the table it is about to write. With no rules registered it behaves exactly as before.
    /// </remarks>
    private readonly IReadOnlyList<ASPClassic.Application.Validation.IRecordWriteRule> _writeRules;

    public DataviewService(
        IDbContextFactory<ASPClassicVBScriptDbContext> dbFactory,
        ILogger<DataviewService> logger,
        ASPClassic.Infrastructure.Engines.DataViewQueryEngine? queryEngine = null,
        IEnumerable<ASPClassic.Application.Validation.IRecordWriteRule>? writeRules = null)
    {
        _dbFactory = dbFactory;
        _logger = logger;
        _queryEngine = queryEngine;
        _writeRules = writeRules?.ToList()
            ?? (IReadOnlyList<ASPClassic.Application.Validation.IRecordWriteRule>)Array.Empty<ASPClassic.Application.Validation.IRecordWriteRule>();
    }

    /// <summary>Port of <c>GetDataView()</c> — SELECT first DataView row.</summary>
    public async Task<DataViewDto?> GetDataViewAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.DataViews
            .AsNoTracking()
            .OrderBy(dv => dv.ViewID)
            .FirstOrDefaultAsync(ct);

        if (entity is null)
            return null;

        return MapToDto(entity);
    }

    /// <summary>Port of <c>GetDataView(viewId)</c> — SELECT DataView by specific ViewID.</summary>
    public async Task<DataViewDto?> GetDataViewAsync(int viewId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.DataViews
            .AsNoTracking()
            .FirstOrDefaultAsync(dv => dv.ViewID == viewId, ct);

        if (entity is null)
        {
            _logger.LogWarning("GetDataViewAsync: ViewID {ViewID} not found", viewId);
            return null;
        }

        return MapToDto(entity);
    }

    /// <summary>
    /// Port of <c>LoadDataview</c> — simplified DTO return matching the interface contract.
    /// Fetches the DataView by parsed ViewID and returns its DTO if published, null otherwise.
    /// </summary>
    public async Task<DataViewDto?> LoadDataviewAsync(string dTItemId, string mode, string viewID, CancellationToken ct = default)
    {
        if (!int.TryParse(viewID, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedViewId))
        {
            _logger.LogWarning("LoadDataviewAsync: ViewID '{ViewID}' is not a valid integer", viewID);
            return null;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.DataViews
            .AsNoTracking()
            .FirstOrDefaultAsync(dv => dv.ViewID == parsedViewId, ct);

        if (entity is null)
        {
            _logger.LogWarning("LoadDataviewAsync: ViewID {ViewID} not found", parsedViewId);
            return null;
        }

        if (!entity.Published)
        {
            _logger.LogWarning("LoadDataviewAsync: ViewID {ViewID} is not published", parsedViewId);
            return null;
        }

        return MapToDto(entity);
    }

    /// <summary>
    /// Port of the full <c>dataview.asp</c> page load logic: fetches DataView, decodes all
    /// flag bitmasks, loads fields and actions, computes derived booleans, resolves
    /// RowReorderColumnMasked from field identifiers. Returns null with error if
    /// ViewID is invalid or view is not published.
    ///
    /// Legacy logic trace:
    /// 1. Parse nItemID from Request("DT_ItemId") — non-numeric → ""
    /// 2. Parse strMode from Request("mode") — empty → "none"
    /// 3. Parse nViewID from Request("ViewID") — non-numeric → error
    /// 4. SELECT * FROM portal.DataView WHERE ViewID = nViewID
    /// 5. Read all scalar columns into local variables
    /// 6. IF DataSource = "" THEN DataSource = "Default"
    /// 7. Decode Flags bitmask: blnAllowUpdate = CBool((nViewFlags AND 1) > 0), etc.
    /// 8. Decode DataTableFlags bitmask: blnDtInfo = CBool((nDtFlags AND 1) > 0), etc.
    /// 9. Compute compound booleans: blnShowRowActions, blnAllowExport, blnAllowExportAll
    /// 10. Loop over arrDataTableModifierButtonStyles to find matching style index
    /// 11. InitDataViewFields(nViewID, adoConnCrude) — builds dvFields collection
    /// 12. For each field: decode FieldFlags, determine search type, classify field
    /// 13. Loop fields to resolve strRowReorderColMasked from FieldSource match
    /// 14. InitDataViewActions(nViewID, True, adoConnCrude) — builds dvActionsInline
    /// 15. InitDataViewActions(nViewID, False, adoConnCrude) — builds dvActionsToolbar
    /// 16. Check Published — if not published, redirect to 404.asp
    /// 17. Build strViewQueryString for AJAX postback URLs
    /// </summary>
    public async Task<DataViewLoadResultDto?> LoadDataviewFullAsync(string dTItemId, string mode, string viewID, CancellationToken ct = default)
    {
        var result = new DataViewLoadResultDto();

        // Step 1: Parse nItemID — legacy: nItemID = Request("DT_ItemId"); IF NOT IsNumeric(nItemID) THEN nItemID = ""
        if (!string.IsNullOrEmpty(dTItemId) && !int.TryParse(dTItemId, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            dTItemId = string.Empty;
        }

        // Step 2: Parse mode — legacy: IF strMode = "" THEN strMode = "none"
        if (string.IsNullOrEmpty(mode))
            mode = "none";

        // Step 3: Validate ViewID
        if (string.IsNullOrEmpty(viewID) || !int.TryParse(viewID, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedViewId))
        {
            result.Error = "ViewID Invalid!";
            _logger.LogWarning("LoadDataviewFullAsync: ViewID '{ViewID}' is invalid", viewID);
            return result;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Step 4: SELECT * FROM portal.DataView WHERE ViewID = nViewID
        var entity = await db.DataViews
            .AsNoTracking()
            .FirstOrDefaultAsync(dv => dv.ViewID == parsedViewId, ct);

        if (entity is null)
        {
            result.Error = "ViewID Not Found!";
            result.ViewID = parsedViewId;
            _logger.LogWarning("LoadDataviewFullAsync: ViewID {ViewID} not found", parsedViewId);
            return result;
        }

        // Step 5: Map core properties from the entity to the result DTO
        result.ViewID = entity.ViewID;
        result.Title = entity.Title ?? string.Empty;
        result.DataSource = entity.DataSource ?? string.Empty;
        result.Published = entity.Published;
        result.ViewDescription = entity.ViewDescription ?? string.Empty;
        result.ViewProcedure = entity.ViewProcedure ?? string.Empty;
        result.ModificationProcedure = entity.ModificationProcedure ?? string.Empty;
        result.DeleteProcedure = entity.DeleteProcedure ?? string.Empty;
        result.MainTable = entity.MainTable ?? string.Empty;
        result.OrderBy = entity.OrderBy ?? string.Empty;
        result.RowReorderColumn = entity.RowReorderColumn ?? string.Empty;
        result.Primarykey = entity.Primarykey ?? string.Empty;
        result.CSSTable = entity.CSSTable ?? string.Empty;
        result.IsSystemObject = entity.IsSystemObject;
        result.DataTableModifierButtonStyle = entity.DataTableModifierButtonStyle;
        result.DataTableDefaultPageSize = entity.DataTableDefaultPageSize;
        result.DataTablePagingStyle = entity.DataTablePagingStyle ?? string.Empty;

        // Step 6: IF DataSource = "" THEN DataSource = "Default"
        if (string.IsNullOrEmpty(result.DataSource))
            result.DataSource = "Default";

        int nViewFlags = entity.Flags;
        int nDtFlags = entity.DataTableFlags;

        // Step 7: Decode View Flags bitmask
        // Legacy: blnAllowUpdate = CBool((nViewFlags AND 1) > 0)
        //         blnAllowInsert = CBool((nViewFlags AND 2) > 0)
        //         blnAllowDelete = CBool((nViewFlags AND 4) > 0)
        //         blnAllowClone  = CBool((nViewFlags AND 8) > 0)
        //         blnShowForm    = CBool((nViewFlags AND 16) > 0)
        //         blnShowList    = CBool((nViewFlags AND 32) > 0)
        //         blnShowCharts  = CBool((nViewFlags AND 64) > 0)
        //         blnShowCustomActions = CBool((nViewFlags AND 128) > 0)
        //         blnBrowseMode  = CBool((nViewFlags AND 256) > 0)
        result.AllowUpdate = (nViewFlags & 1) > 0;
        result.AllowInsert = (nViewFlags & 2) > 0;
        result.AllowDelete = (nViewFlags & 4) > 0;
        result.AllowClone = (nViewFlags & 8) > 0;
        result.ShowForm = (nViewFlags & 16) > 0;
        result.ShowList = (nViewFlags & 32) > 0;
        result.ShowCharts = (nViewFlags & 64) > 0;
        result.ShowCustomActions = (nViewFlags & 128) > 0;
        result.BrowseMode = (nViewFlags & 256) > 0;

        // Step 8: Decode DataTable Flags bitmask
        // Legacy: blnDtInfo = CBool((nDtFlags AND 1) > 0)
        //         blnDtColumnFooter = CBool((nDtFlags AND 2) > 0)
        //         blnDtQuickSearch  = CBool((nDtFlags AND 4) > 0)
        //         blnDtSort         = CBool((nDtFlags AND 8) > 0)
        //         blnDtPagination   = CBool((nDtFlags AND 16) > 0)
        //         blnDtPageSizeSelection = CBool((nDtFlags AND 32) > 0)
        //         blnDtStateSave    = CBool((nDtFlags AND 64) > 0)
        //         blnAllowSearch    = CBool((nDtFlags AND 128) > 0)
        //         blnAllowColumnsToggle = CBool((nDtFlags AND 256) > 0)
        //         blnAllowRowDetails    = CBool((nDtFlags AND 512) > 0)
        //         blnAllowRowSelection  = CBool((nDtFlags AND 1024) > 0)
        //         blnExportClipboard = CBool((nDtFlags AND 2048) > 0)
        //         blnExportCSV       = CBool((nDtFlags AND 4096) > 0)
        //         blnExportExcel     = CBool((nDtFlags AND 8192) > 0)
        //         blnExportPDF       = CBool((nDtFlags AND 16384) > 0)
        //         blnExportPrint     = CBool((nDtFlags AND 32768) > 0)
        //         blnFixedHeaders    = CBool((nDtFlags AND 65536) > 0)
        result.DtInfo = (nDtFlags & 1) > 0;
        result.DtColumnFooter = (nDtFlags & 2) > 0;
        result.DtQuickSearch = (nDtFlags & 4) > 0;
        result.DtSort = (nDtFlags & 8) > 0;
        result.DtPagination = (nDtFlags & 16) > 0;
        result.DtPageSizeSelection = (nDtFlags & 32) > 0;
        result.DtStateSave = (nDtFlags & 64) > 0;
        result.AllowSearch = (nDtFlags & 128) > 0;
        result.AllowColumnsToggle = (nDtFlags & 256) > 0;
        result.AllowRowDetails = (nDtFlags & 512) > 0;
        result.AllowRowSelection = (nDtFlags & 1024) > 0;
        result.ExportClipboard = (nDtFlags & 2048) > 0;
        result.ExportCSV = (nDtFlags & 4096) > 0;
        result.ExportExcel = (nDtFlags & 8192) > 0;
        result.ExportPDF = (nDtFlags & 16384) > 0;
        result.ExportPrint = (nDtFlags & 32768) > 0;
        result.FixedHeaders = (nDtFlags & 65536) > 0;

        // Step 9: Computed compound flags
        // Legacy: blnShowRowActions = blnShowCustomActions OR blnAllowUpdate OR blnAllowDelete
        //         OR blnAllowClone OR strRowReorderCol <> "" OR blnAllowRowDetails
        result.ShowRowActions = result.ShowCustomActions || result.AllowUpdate || result.AllowDelete
                                || result.AllowClone || !string.IsNullOrEmpty(result.RowReorderColumn)
                                || result.AllowRowDetails;

        // Legacy: blnAllowExport = blnExportClipboard OR blnExportCSV OR blnExportExcel
        //         OR blnExportPDF OR blnExportPrint
        result.AllowExport = result.ExportClipboard || result.ExportCSV || result.ExportExcel
                             || result.ExportPDF || result.ExportPrint;

        // Legacy: blnAllowExportAll = blnExportClipboard AND blnExportCSV AND blnExportExcel
        //         AND blnExportPDF AND blnExportPrint
        result.AllowExportAll = result.ExportClipboard && result.ExportCSV && result.ExportExcel
                                && result.ExportPDF && result.ExportPrint;

        // Step 10: Resolve modifier button style index
        // Legacy: loop over arrDataTableModifierButtonStyles comparing StyleValue to DataTableModifierButtonStyle
        var modBtnStyles = await db.DataViewModifierButtonStyles
            .AsNoTracking()
            .ToListAsync(ct);

        result.DtModBtnStyleIndex = 0;
        for (int i = 0; i < modBtnStyles.Count; i++)
        {
            if (int.TryParse(modBtnStyles[i].StyleValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var styleVal)
                && styleVal == entity.DataTableModifierButtonStyle)
            {
                result.DtModBtnStyleIndex = i;
                break;
            }
        }

        // Step 11: Load fields — legacy: SET dvFields = InitDataViewFields(nViewID, adoConnCrude)
        // SQL: SELECT * FROM portal.DataViewField WHERE ViewID = @ViewID ORDER BY FieldOrder
        var fieldEntities = await db.DataViewFields
            .AsNoTracking()
            .Where(f => f.ViewID == parsedViewId)
            .OrderBy(f => f.FieldOrder)
            .ToListAsync(ct);

        // Step 12: For each field — decode FieldFlags, determine search type, classify field
        foreach (var f in fieldEntities)
        {
            int fieldTypeNumeric = 0;
            int.TryParse(f.FieldType, NumberStyles.Integer, CultureInfo.InvariantCulture, out fieldTypeNumeric);

            int ff = f.FieldFlags;

            // The database declares what each bit means, in portal.DataViewFieldFlags:
            //   1 Show in Form   2 Required   4 Read Only   8 Show in Items List   16 Show in Search
            // There is no sixth or seventh flag. The legacy agrees throughout — it tests
            // `AND 4 = 0` for "not read-only", `AND 2 > 0` for required, and `AND 9` (form or
            // list) to decide whether to render a column at all.
            bool showInForm = (ff & 1) > 0;
            bool isRequired = (ff & 2) > 0;
            bool isReadOnly = (ff & 4) > 0;
            bool showInList = (ff & 8) > 0;
            bool isSearchEnabled = (ff & 16) > 0;

            // Legacy: determine search type by field type
            // Types 5 (dropdown/select), 6 (linked dropdown), 9 (checkbox/boolean),
            // 17-23 (various linked lookups) use dropdown search
            bool isDropdown = fieldTypeNumeric == 5 || fieldTypeNumeric == 6 || fieldTypeNumeric == 9
                              || (fieldTypeNumeric >= 17 && fieldTypeNumeric <= 23);

            var fieldInfo = new DataViewFieldInfoDto
            {
                FieldID = f.FieldID,
                ViewID = f.ViewID,
                FieldLabel = f.FieldLabel ?? string.Empty,
                FieldSource = f.FieldSource ?? string.Empty,
                FieldType = f.FieldType ?? string.Empty,
                FieldTypeNumeric = fieldTypeNumeric,
                FieldFlags = ff,
                FieldOrder = f.FieldOrder,
                DefaultValue = f.DefaultValue ?? string.Empty,
                MaxLength = f.MaxLength ?? 0,
                UriPath = f.UriPath ?? string.Empty,
                UriStyle = f.UriStyle ?? 0,
                LinkedTable = f.LinkedTable ?? string.Empty,
                LinkedTableValueField = f.LinkedTableValueField ?? string.Empty,
                LinkedTableTitleField = f.LinkedTableTitleField ?? string.Empty,
                LinkedTableGroupField = f.LinkedTableGroupField ?? string.Empty,
                LinkedTableGlyphField = f.LinkedTableGlyphField ?? string.Empty,
                LinkedTableTooltipField = f.LinkedTableTooltipField ?? string.Empty,
                LinkedTableAddition = f.LinkedTableAddition ?? string.Empty,
                Width = f.Width ?? 0,
                Height = f.Height ?? 0,
                FieldDescription = f.FieldDescription ?? string.Empty,
                FormatPattern = f.FormatPattern ?? string.Empty,
                FieldTooltip = f.FieldTooltip ?? string.Empty,
                FieldIdentifier = f.FieldIdentifier ?? string.Empty,
                ShowInForm = showInForm,
                IsRequired = isRequired,
                IsReadOnly = isReadOnly,
                ShowInList = showInList,
                // The legacy's `AND 9` test: a field earns a place if it appears in either surface.
                IsVisible = showInForm || showInList,
                IsSearchable = isSearchEnabled,
                IsSearchableDropdown = isSearchEnabled && isDropdown,
                IsSearchableText = isSearchEnabled && !isDropdown
            };

            result.Fields.Add(fieldInfo);
        }

        // Step 13: Resolve RowReorderColumnMasked
        // Legacy: loop through dvFields; if field.FieldSource = strRowReorderCol then
        //         strRowReorderColMasked = field.FieldIdentifier
        //         IF strRowReorderColMasked = "" THEN strRowReorderColMasked = strRowReorderCol
        if (!string.IsNullOrEmpty(result.RowReorderColumn))
        {
            foreach (var field in result.Fields)
            {
                if (string.Equals(result.RowReorderColumn, field.FieldSource, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrEmpty(field.FieldIdentifier))
                {
                    result.RowReorderColumnMasked = field.FieldIdentifier;
                    break;
                }
            }

            if (string.IsNullOrEmpty(result.RowReorderColumnMasked))
            {
                result.RowReorderColumnMasked = result.RowReorderColumn;
            }
        }

        // Steps 14-15: Load actions — split into inline (IsPerRow=true) and toolbar (IsPerRow=false)
        // Legacy: SET dvActionsInline = InitDataViewActions(nViewID, True, adoConnCrude)
        //         SET dvActionsToolbar = InitDataViewActions(nViewID, False, adoConnCrude)
        // SQL: SELECT * FROM portal.DataViewAction WHERE ViewID = @ViewID ORDER BY ActionOrder
        var actionEntities = await db.DataViewActions
            .AsNoTracking()
            .Where(a => a.ViewID == parsedViewId)
            .OrderBy(a => a.ActionOrder)
            .ToListAsync(ct);

        foreach (var a in actionEntities)
        {
            var actionInfo = MapActionToInfo(a);
            if (a.IsPerRow)
            {
                result.InlineActions.Add(actionInfo);
            }
            else
            {
                result.ToolbarActions.Add(actionInfo);
            }
        }

        // Step 16: Validate published
        // Legacy: IF NOT blnPublished OR strError <> "" THEN Response.Redirect "404.asp?msg=viewnotfound"
        if (!result.Published)
        {
            result.Error = "viewnotfound";
            _logger.LogWarning("LoadDataviewFullAsync: ViewID {ViewID} is not published, would redirect to 404", parsedViewId);
            return result;
        }

        // Step 17: Build view query string
        // Legacy: strViewQueryString = "&ViewID=" & nViewID
        result.ViewQueryString = $"&ViewID={parsedViewId}";

        _logger.LogInformation(
            "LoadDataviewFullAsync: Loaded ViewID {ViewID} '{Title}' with {FieldCount} fields, " +
            "{InlineCount} inline actions, {ToolbarCount} toolbar actions",
            parsedViewId, result.Title, result.Fields.Count,
            result.InlineActions.Count, result.ToolbarActions.Count);

        return result;
    }

    /// <summary>
    /// Port of stored procedure <c>[dbo].[GetDataViewLabel](@ViewID int, @returns NVARCHAR(100) OUTPUT)</c>.
    /// Since SQLite doesn't support stored procedures, this executes the equivalent query directly.
    /// </summary>
    public async Task<string?> GetDataViewLabelAsync(int viewId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var title = await db.DataViews
            .AsNoTracking()
            .Where(dv => dv.ViewID == viewId)
            .Select(dv => dv.Title)
            .FirstOrDefaultAsync();

        return title;
    }

    /// <summary>
    /// Port of stored procedure <c>[portal].[GetDataViewInfo](@ViewID INT)</c>.
    /// Returns the DataView metadata for the given ViewID.
    /// </summary>
    public async Task<DataViewDto?> GetDataViewInfoAsync(int viewId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var entity = await db.DataViews
            .AsNoTracking()
            .FirstOrDefaultAsync(dv => dv.ViewID == viewId);

        if (entity is null)
        {
            _logger.LogWarning("GetDataViewInfoAsync: ViewID {ViewID} not found", viewId);
            return null;
        }

        return MapToDto(entity);
    }

    /// <summary>
    /// Port of stored procedure <c>[portal].[GetDataViewContents](@ViewID INT)</c>.
    /// Retrieves the DataView along with its field definitions for content rendering.
    /// </summary>
    public async Task<DataViewDto?> GetDataViewContentsAsync(int viewId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var entity = await db.DataViews
            .AsNoTracking()
            .FirstOrDefaultAsync(dv => dv.ViewID == viewId);

        if (entity is null)
        {
            _logger.LogWarning("GetDataViewContentsAsync: ViewID {ViewID} not found", viewId);
            return null;
        }

        return MapToDto(entity);
    }

    /// <summary>
    /// Port of stored procedure <c>[portal].[GetDataViewContentsCommand](@ViewID INT)</c>.
    /// Returns the DataView metadata so the caller can construct the appropriate query.
    /// </summary>
    public async Task<DataViewDto?> GetDataViewContentsCommandAsync(int viewId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var entity = await db.DataViews
            .AsNoTracking()
            .FirstOrDefaultAsync(dv => dv.ViewID == viewId);

        if (entity is null)
        {
            _logger.LogWarning("GetDataViewContentsCommandAsync: ViewID {ViewID} not found", viewId);
            return null;
        }

        return MapToDto(entity);
    }

    /// <summary>
    /// Port of stored procedure <c>[portal].[GetDataViewDataTableCommand]</c> with server-side
    /// paging, filtering, sorting and column ordering. Loads the DataView, its fields, validates
    /// parameters, and returns view metadata for query construction.
    ///
    /// Legacy SP builds dynamic SQL:
    ///   SELECT [columns from DataViewField]
    ///   FROM [MainTable or DataSource]
    ///   WHERE [search conditions based on searchValue and columnsOptionsXml]
    ///   ORDER BY [columnsOrderXml or default OrderBy]
    ///   OFFSET @Start ROWS FETCH NEXT @Length ROWS ONLY
    /// </summary>
    public async Task<DataViewDto?> GetDataViewDataTableCommandAsync(
        int viewId, int draw, int start, int length,
        string searchValue, bool searchRegEx,
        string columnsOptionsXml, string columnsOrderXml,
        bool filteringByPk)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var entity = await db.DataViews
            .AsNoTracking()
            .FirstOrDefaultAsync(dv => dv.ViewID == viewId);

        if (entity is null)
        {
            _logger.LogWarning("GetDataViewDataTableCommandAsync: ViewID {ViewID} not found", viewId);
            return null;
        }

        // Load fields to validate column references
        var fields = await db.DataViewFields
            .AsNoTracking()
            .Where(f => f.ViewID == viewId)
            .OrderBy(f => f.FieldOrder)
            .ToListAsync();

        // Validate and clamp paging parameters
        if (start < 0) start = 0;
        if (length <= 0) length = entity.DataTableDefaultPageSize > 0 ? entity.DataTableDefaultPageSize : 10;

        _logger.LogInformation(
            "GetDataViewDataTableCommandAsync: ViewID={ViewID}, Draw={Draw}, Start={Start}, Length={Length}, " +
            "Search='{Search}', RegEx={RegEx}, FilterByPK={FilterByPK}, FieldCount={FieldCount}",
            viewId, draw, start, length,
            searchValue ?? string.Empty, searchRegEx, filteringByPk, fields.Count);

        var dto = MapToDto(entity);
        return dto;
    }

    // ──────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────

    /// <summary>Maps a DataView entity to a DataViewDto, null-coalescing all nullable columns.</summary>
    private static DataViewDto MapToDto(Domain.Entities.Data.DataView entity)
    {
        return new DataViewDto
        {
            ViewID = entity.ViewID,
            Title = entity.Title ?? string.Empty,
            DataSource = entity.DataSource ?? string.Empty,
            MainTable = entity.MainTable ?? string.Empty,
            Primarykey = entity.Primarykey ?? string.Empty,
            ModificationProcedure = entity.ModificationProcedure ?? string.Empty,
            ViewProcedure = entity.ViewProcedure ?? string.Empty,
            DeleteProcedure = entity.DeleteProcedure ?? string.Empty,
            ViewDescription = entity.ViewDescription ?? string.Empty,
            OrderBy = entity.OrderBy ?? string.Empty,
            Flags = entity.Flags,
            DataTableModifierButtonStyle = entity.DataTableModifierButtonStyle,
            DataTableFlags = entity.DataTableFlags,
            DataTableDefaultPageSize = entity.DataTableDefaultPageSize,
            DataTablePagingStyle = entity.DataTablePagingStyle ?? string.Empty,
            Published = entity.Published,
            RowReorderColumn = entity.RowReorderColumn ?? string.Empty,
            IsSystemObject = entity.IsSystemObject,
            CSSTable = entity.CSSTable ?? string.Empty
        };
    }

    /// <summary>Maps a DataViewAction entity to a DataViewActionInfoDto.</summary>
    private static DataViewActionInfoDto MapActionToInfo(Domain.Entities.Data.DataViewAction entity)
    {
        return new DataViewActionInfoDto
        {
            ActionID = entity.ActionID,
            ViewID = entity.ViewID,
            ActionLabel = entity.ActionLabel ?? string.Empty,
            ParentActionID = entity.ParentActionID,
            ActionTooltip = entity.ActionTooltip ?? string.Empty,
            ActionDescription = entity.ActionDescription ?? string.Empty,
            ActionOrder = entity.ActionOrder,
            RequireConfirmation = entity.RequireConfirmation,
            OpenURLInNewWindow = entity.OpenURLInNewWindow ?? false,
            ActionExpression = entity.ActionExpression ?? string.Empty,
            GlyphIcon = entity.GlyphIcon ?? string.Empty,
            IsPerRow = entity.IsPerRow,
            CSSButton = entity.CSSButton ?? string.Empty,
            ActionType = entity.ActionType ?? string.Empty,
            DataViewTitle = entity.DataViewTitle ?? string.Empty
        };
    }

    /// <summary>The fields this view shows, in their configured order.</summary>
    public async Task<List<DataViewFieldDto>> GetDataViewFieldsAsync(int viewId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.DataViewFields
            .AsNoTracking()
            .Where(f => f.ViewID == viewId)
            .OrderBy(f => f.FieldOrder)
            .Select(f => new DataViewFieldDto
            {
                ViewID = f.ViewID,
                FieldID = f.FieldID,
                FieldLabel = f.FieldLabel,
                FieldSource = f.FieldSource,
                FieldType = f.FieldType,
                FieldFlags = f.FieldFlags,
                FieldOrder = f.FieldOrder,
                DefaultValue = f.DefaultValue,
                MaxLength = f.MaxLength,
                UriPath = f.UriPath,
                UriStyle = f.UriStyle,
                LinkedTable = f.LinkedTable,
                LinkedTableValueField = f.LinkedTableValueField,
                LinkedTableTitleField = f.LinkedTableTitleField,
                FieldDescription = f.FieldDescription,
                FieldTooltip = f.FieldTooltip,

                // The field's client-side name, and the one thing that lets a per-column filter in
                // the address be matched to a field. Omitted from the projection it is always
                // empty, every such filter silently matches nothing, and a screen meant to show one
                // parent's fields shows every field there is.
                FieldIdentifier = f.FieldIdentifier,
            })
            .ToListAsync(ct);
    }

    /// <summary>The view's actions — per-row when <paramref name="isPerRow"/>, toolbar otherwise.</summary>
    public async Task<List<DataViewActionDto>> GetDataViewActionsAsync(
        int viewId, bool isPerRow, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.DataViewActions
            .AsNoTracking()
            .Where(a => a.ViewID == viewId && a.IsPerRow == isPerRow)
            .OrderBy(a => a.ActionOrder)
            .Select(a => new DataViewActionDto
            {
                ActionID = a.ActionID,
                ViewID = a.ViewID,
                ActionLabel = a.ActionLabel,
                ParentActionID = a.ParentActionID ?? 0,
                ActionTooltip = a.ActionTooltip,
                ActionDescription = a.ActionDescription,
                ActionOrder = a.ActionOrder,
                IsPerRow = a.IsPerRow,

                // Everything that makes an action DO something. Left out of the projection, every
                // action arrives with an empty type and an empty expression: the page reports
                // "Action type '' is not recognized", and a button whose whole job is to open a URL
                // has no URL to open. The rows are correct in the database throughout — only the
                // columns nobody selected are missing.
                ActionType = a.ActionType,
                ActionExpression = a.ActionExpression,
                GlyphIcon = a.GlyphIcon,
                RequireConfirmation = a.RequireConfirmation,
                // Nullable in the schema; absent means the legacy's default, which is same-window.
                OpenURLInNewWindow = a.OpenURLInNewWindow ?? false,
                CSSButton = a.CSSButton,
                DataViewTitle = a.DataViewTitle,
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Writes one record of the view's own table — the legacy's <c>ajax_dataview.asp</c> post.
    /// </summary>
    /// <remarks>
    /// <para>The legacy opened an updatable recordset on the view's MainTable, assigned the posted
    /// fields and called Update. There is no typed entity to use here, because the table is
    /// whatever the view points at, so the statement is composed from the view's field list.</para>
    /// <para>Values are passed as parameters, never concatenated. Column and table names come from
    /// the view definition rather than from the request, and only names that appear in the view's
    /// own field list are used, so nothing a user types can reach the statement as SQL.</para>
    /// </remarks>
    public async Task<string?> SaveDataviewRecordAsync(
        int viewId, string mode, string? itemId,
        IReadOnlyDictionary<string, string> values, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var view = await db.DataViews.AsNoTracking().FirstOrDefaultAsync(v => v.ViewID == viewId, ct);
        if (view == null) return "Data View not found.";

        var isAdd = string.Equals(mode, "add", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(mode, "clone", StringComparison.OrdinalIgnoreCase);

        // portal.DataViewFlags: 1 Allow Edit, 2 Allow Add.
        if (isAdd && (view.Flags & 2) == 0) return "This view does not allow adding records.";
        if (!isAdd && (view.Flags & 1) == 0) return "This view does not allow editing records.";

        var table = BareName(view.MainTable);
        var key = BareName(view.Primarykey);

        if (table.Length == 0 || key.Length == 0)
            return "This view does not name a table and key to write to.";

        var fields = await db.DataViewFields
            .AsNoTracking()
            .Where(f => f.ViewID == viewId)
            .OrderBy(f => f.FieldOrder)
            .ToListAsync(ct);

        // portal.DataViewFieldFlags: 1 Show in Form, 2 Required, 4 Read Only.
        // Field type 10 is a link — rendered, never written, exactly as the legacy skipped it.
        var writable = fields
            .Where(f => (f.FieldFlags & 1) > 0
                     && (f.FieldFlags & 4) == 0
                     && f.FieldType != "10"
                     && !string.IsNullOrWhiteSpace(f.FieldSource)
                     && !string.Equals(f.FieldSource, key, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (writable.Count == 0) return "This view has no writable fields.";

        var missing = writable
            .Where(f => (f.FieldFlags & 2) > 0
                     && (!values.TryGetValue(f.FieldSource, out var v) || string.IsNullOrWhiteSpace(v)))
            .Select(f => f.FieldLabel)
            .ToList();

        if (missing.Count > 0) return $"Required: {string.Join(", ", missing)}";

        // Whatever this particular table requires beyond being structurally complete. The writer
        // does not know what any table means; it only asks whether something else does.
        foreach (var rule in _writeRules.Where(r =>
                     string.Equals(r.Table, table, StringComparison.OrdinalIgnoreCase)))
        {
            var ruleError = await rule.ValidateAsync(viewId, mode, itemId, values, ct);

            if (ruleError is not null)
            {
                _logger.LogInformation(
                    "A write to {Table} through view {ViewId} was refused: {Error}", table, viewId, ruleError);

                return ruleError;
            }
        }

        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        var assignments = new List<string>();
        var columns = new List<string>();
        var placeholders = new List<string>();

        for (var i = 0; i < writable.Count; i++)
        {
            var f = writable[i];
            var name = $"@p{i}";

            values.TryGetValue(f.FieldSource, out var raw);
            var blank = string.IsNullOrWhiteSpace(raw);

            // On insert, a field the user left blank is one the legacy never assigned: it opened
            // the row with AddNew and set only what was posted, so the column's DDL default
            // applied. Writing NULL instead defeats that default, and on a NOT NULL column with one
            // it fails outright — which loses the whole record for a field nobody filled in.
            if (blank && isAdd) continue;

            var parameter = cmd.CreateParameter();
            parameter.ParameterName = name;

            // On update the row already exists, so a cleared field means cleared. Empty is absent
            // rather than an empty string — the difference between "no date" and a date of zero.
            parameter.Value = blank ? DBNull.Value : raw!;

            cmd.Parameters.Add(parameter);

            columns.Add($"[{f.FieldSource}]");
            placeholders.Add(name);
            assignments.Add($"[{f.FieldSource}] = {name}");
        }

        if (columns.Count == 0) return "Nothing was filled in.";

        if (isAdd)
        {
            cmd.CommandText =
                $"INSERT INTO [{table}] ({string.Join(", ", columns)}) " +
                $"VALUES ({string.Join(", ", placeholders)})";
        }
        else
        {
            if (!int.TryParse(itemId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                return "This record has no identifier to update.";

            var keyParam = cmd.CreateParameter();
            keyParam.ParameterName = "@key";
            keyParam.Value = id;
            cmd.Parameters.Add(keyParam);

            cmd.CommandText =
                $"UPDATE [{table}] SET {string.Join(", ", assignments)} WHERE [{key}] = @key";
        }

        try
        {
            var affected = await cmd.ExecuteNonQueryAsync(ct);

            // Reporting success for a statement that changed nothing is how a save appears to work
            // and silently does not.
            if (affected == 0) return "Nothing was written — the record was not found.";

            _logger.LogInformation(
                "{Mode} on view {ViewId} wrote {Rows} row(s) to {Table}.", mode, viewId, affected, table);

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Saving a record of view {ViewId} to {Table} failed.", viewId, table);
            return ex.Message;
        }
    }

    /// <summary>A schema-qualified name reduced to the bare one the context maps by.</summary>
    private static string BareName(string? qualified)
    {
        var v = (qualified ?? string.Empty).Trim().Trim('[', ']');
        var dot = v.LastIndexOf('.');
        return (dot >= 0 ? v[(dot + 1)..] : v).Trim('[', ']');
    }

    /// <summary>
    /// The rows the grid shows — paged, filtered and sorted by the database.
    /// </summary>
    /// <remarks>
    /// The query engine that does this work already existed and nothing called it; it was not even
    /// registered, so it could not have been resolved. Meanwhile the page asked for the view's
    /// definition, threw the answer away and assigned an empty list, which is why every grid in the
    /// application reported "No records found" however much data there was.
    /// </remarks>
    public async Task<ASPClassic.Infrastructure.Engines.DataViewResultDto> GetDataViewRowsAsync(
        int viewId, int start, int length, string searchValue, bool filteringByPk = false,
        IReadOnlyDictionary<string, string>? columnFilters = null)
    {
        if (_queryEngine is null)
        {
            _logger.LogError(
                "No query engine is available, so view {ViewId} can return no rows. " +
                "DataViewQueryEngine has to be registered for the grid to show anything.", viewId);

            return new ASPClassic.Infrastructure.Engines.DataViewResultDto { Error = "The query engine is not available." };
        }

        return await _queryEngine.ExecuteDataTableQueryAsync(
            viewId,
            draw: 1,
            start: start,
            length: length,
            searchValue: searchValue ?? string.Empty,
            searchRegEx: false,
            columnsOptionsXml: string.Empty,
            columnsOrderXml: string.Empty,
            filteringByPk: filteringByPk,
            columnFilters: columnFilters);
    }

    /// <summary>
    /// One record of the view's own table, by key, as column-name to value.
    /// </summary>
    /// <remarks>
    /// The legacy opened the row before rendering the form — <c>SELECT * FROM MainTable WHERE
    /// pk = id</c> — so the inputs showed what was stored. Without it an edit form opens on the
    /// field defaults, which looks like a record with no data in it and saves over the real one.
    /// </remarks>
    public async Task<Dictionary<string, string>> GetDataviewRecordAsync(
        int viewId, string itemId, CancellationToken ct = default)
    {
        var record = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(itemId)) return record;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var view = await db.DataViews.AsNoTracking().FirstOrDefaultAsync(v => v.ViewID == viewId, ct);
        if (view == null) return record;

        var table = BareName(view.MainTable);
        var key = BareName(view.Primarykey);
        if (table.Length == 0 || key.Length == 0) return record;

        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();

        // The key is a parameter; the table and column names come from the view definition.
        cmd.CommandText = $"SELECT * FROM [{table}] WHERE [{key}] = @key";

        var p = cmd.CreateParameter();
        p.ParameterName = "@key";
        p.Value = int.TryParse(itemId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? id
            : itemId;
        cmd.Parameters.Add(p);

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            if (await reader.ReadAsync(ct))
            {
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    record[reader.GetName(i)] =
                        reader.IsDBNull(i) ? string.Empty : reader.GetValue(i)?.ToString() ?? string.Empty;
                }
            }
            else
            {
                _logger.LogWarning(
                    "No record with {Key}={Id} in {Table} for view {ViewId}.", key, itemId, table, viewId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reading record {Id} of view {ViewId} from {Table} failed.", itemId, viewId, table);
        }

        return record;
    }

    /// <summary>
    /// The published views, for building a menu from — newest last, system views separated.
    /// </summary>
    /// <remarks>
    /// <para>Derived from the DataView table rather than read from Navigation. The legacy keeps the
    /// two apart: Navigation is a menu somebody curates, and adding a view does not add a link to
    /// it. That is faithful but means a new view is invisible until it is also added to the menu by
    /// hand, so this list is computed instead and a new view appears the moment it is published.</para>
    /// <para>Unpublished views are left out for the same reason the pages refuse to render them.</para>
    /// <para>SYSTEM views are left out too, and that is not cosmetic. Data View Fields and Data
    /// View Actions are child screens: they list the fields and actions of EVERY view, and every
    /// link to them in the legacy — without exception — carries a filter naming the parent. Offered
    /// as a plain menu entry they open unfiltered, showing all 75 field rows of the whole portal as
    /// though that were a screen. The two system views the legacy does put on its menu, Manage Data
    /// Views and Manage Navigation, are in the curated Navigation table and arrive from there.</para>
    /// </remarks>
    public async Task<List<DataViewDto>> GetNavigableDataViewsAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entities = await db.DataViews
            .AsNoTracking()
            .Where(v => v.Published && !v.IsSystemObject)
            .OrderBy(v => v.Title)
            .ToListAsync(ct);

        return entities.Select(MapToDto).ToList();
    }

    /// <summary>
    /// The choices for a field that draws them from another table — value and label, in label order.
    /// </summary>
    public async Task<List<(string Value, string Label)>> GetLookupOptionsAsync(
        string linkedTable, string valueField, string titleField, CancellationToken ct = default)
    {
        var options = new List<(string Value, string Label)>();

        var table = BareName(linkedTable);
        var value = BareName(valueField);
        var label = BareName(titleField);

        if (table.Length == 0 || value.Length == 0) return options;
        if (label.Length == 0) label = value;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();

        // Names come from the view's own field definition, never from a request.
        cmd.CommandText = $"SELECT [{value}], [{label}] FROM [{table}] ORDER BY [{label}]";

        try
        {
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                var v = reader.IsDBNull(0) ? string.Empty : reader.GetValue(0)?.ToString() ?? string.Empty;
                var l = reader.IsDBNull(1) ? string.Empty : reader.GetValue(1)?.ToString() ?? string.Empty;

                if (v.Length > 0) options.Add((v, l.Length > 0 ? l : v));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not read the choices for a field linked to {Table}; it will fall back to a text box.",
                table);
        }

        return options;
    }

    /// <summary>
    /// Deletes one record of the view's own table. Returns the error to show, or null on success.
    /// </summary>
    /// <remarks>
    /// The delete was previously "performed" by calling the method that LOADS a view, passing
    /// "delete" as its mode — which that method ignores entirely. It returned the view definition,
    /// the page reported "Record deleted", and the row stayed exactly where it was.
    /// </remarks>
    public async Task<string?> DeleteDataviewRecordAsync(
        int viewId, string itemId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return "No record was identified to delete.";

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var view = await db.DataViews.AsNoTracking().FirstOrDefaultAsync(v => v.ViewID == viewId, ct);
        if (view == null) return "Data View not found.";

        // portal.DataViewFlags: 4 is Allow Delete. The legacy checked it before deleting and so
        // does this — a screen that forbids deletion must forbid it here, not only in the toolbar.
        if ((view.Flags & 4) == 0) return "This view does not allow deleting records.";

        var table = BareName(view.MainTable);
        var key = BareName(view.Primarykey);

        if (table.Length == 0 || key.Length == 0)
            return "This view does not name a table and key to delete from.";

        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"DELETE FROM [{table}] WHERE [{key}] = @key";

        var p = cmd.CreateParameter();
        p.ParameterName = "@key";
        p.Value = int.TryParse(itemId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? id
            : itemId;
        cmd.Parameters.Add(p);

        try
        {
            var affected = await cmd.ExecuteNonQueryAsync(ct);

            // Nothing removed is not a successful delete. Saying it was is how a record appears to
            // vanish and then comes back on the next refresh.
            if (affected == 0) return "Nothing was deleted — the record was not found.";

            _logger.LogInformation(
                "Deleted {Rows} row(s) from {Table} where {Key}={Id}, for view {ViewId}.",
                affected, table, key, itemId, viewId);

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deleting record {Id} of view {ViewId} from {Table} failed.", itemId, viewId, table);
            return ex.Message;
        }
    }
}
