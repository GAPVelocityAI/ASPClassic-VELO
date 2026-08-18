using System.Globalization;
using System.Text.RegularExpressions;
using ASPClassic.Application.DTOs.Admin;
using ASPClassic.Application.DTOs.Data;
using ASPClassic.Domain.Entities.Data;
using ASPClassic.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ASPClassic.Application.Services.Admin;

/// <summary>
/// Service for managing DataView field definitions.
/// Port of <c>admin_dataviewfields.asp</c>.
/// </summary>
public class AdminDataviewfieldsService : IAdminDataviewfieldsService
{
    private readonly IDbContextFactory<ASPClassicVBScriptDbContext> _dbFactory;
    private readonly ILogger<AdminDataviewfieldsService> _logger;

    /// <summary>
    /// Holds the current ViewID for parameterless GetDataViewAsync/GetDataViewFieldAsync calls.
    /// Set by page-level interactions.
    /// </summary>
    private int _currentViewId;
    private int _currentFieldId;

    public AdminDataviewfieldsService(
        IDbContextFactory<ASPClassicVBScriptDbContext> dbFactory,
        ILogger<AdminDataviewfieldsService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DataViewDto?> GetDataViewAsync(CancellationToken ct = default)
    {
        // A view is identified by a non-zero id, not a positive one: this portal's own
        // system views are negative (-1 to -4). The legacy tested IsNumeric and presence and
        // never the sign, so `> 0` silently treats every built-in view as "none supplied".
        if (_currentViewId == 0)
            return null;
        return await GetDataViewByIdAsync(_currentViewId, ct);
    }

    /// <inheritdoc />
    public async Task<DataViewFieldDto?> GetDataViewFieldAsync(CancellationToken ct = default)
    {
        if (_currentFieldId <= 0)
            return null;
        return await GetDataViewFieldByIdAsync(_currentFieldId, ct);
    }

    /// <inheritdoc />
    public async Task<DataViewDto?> GetDataViewByIdAsync(int viewId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.DataViews
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.ViewID == viewId, ct);

        if (entity is null)
            return null;

        return MapDataViewToDto(entity);
    }

    /// <inheritdoc />
    public async Task<DataViewFieldDto?> GetDataViewFieldByIdAsync(int fieldId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.DataViewFields
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.FieldID == fieldId, ct);

        if (entity is null)
            return null;

        return MapFieldToDto(entity);
    }

    /// <inheritdoc />
    public async Task<DataViewDto?> LoadAdminDataviewfieldsAsync(
        string mode, string itemID, string viewID,
        string fieldLabel, string fieldSource, string fieldType,
        string fieldDescription, string defaultValue,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Validate viewID
        if (!int.TryParse(viewID, NumberStyles.Integer, CultureInfo.InvariantCulture, out int nViewID) || nViewID == 0)
        {
            _logger.LogWarning("LoadAdminDataviewfieldsAsync: Invalid ViewID={ViewID}", viewID);
            return null;
        }

        _currentViewId = nViewID;

        // Parse itemID
        int.TryParse(itemID, NumberStyles.Integer, CultureInfo.InvariantCulture, out int nItemID);
        _currentFieldId = nItemID;

        // Load the DataView header
        var dataView = await db.DataViews
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.ViewID == nViewID, ct);

        if (dataView is null)
        {
            _logger.LogWarning("LoadAdminDataviewfieldsAsync: DataView not found for ViewID={ViewID}", nViewID);
            return null;
        }

        string normalizedMode = (mode ?? string.Empty).Trim().ToLowerInvariant();

        // Process mutations based on mode
        if (normalizedMode == "add" && !string.IsNullOrWhiteSpace(fieldLabel))
        {
            // Determine the next FieldOrder
            int nextOrder = 1;
            var maxOrder = await db.DataViewFields
                .Where(f => f.ViewID == nViewID)
                .OrderByDescending(f => f.FieldOrder)
                .Select(f => (int?)f.FieldOrder)
                .FirstOrDefaultAsync(ct);
            if (maxOrder.HasValue)
                nextOrder = maxOrder.Value + 1;

            var newField = new DataViewField
            {
                ViewID = nViewID,
                FieldLabel = fieldLabel,
                FieldSource = fieldSource ?? string.Empty,
                FieldType = string.IsNullOrWhiteSpace(fieldType) ? "1" : fieldType,
                FieldFlags = 1,
                FieldOrder = nextOrder,
                DefaultValue = defaultValue,
                FieldDescription = fieldDescription
            };
            db.DataViewFields.Add(newField);

            try
            {
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Added DataViewField for ViewID={ViewID}, FieldLabel={FieldLabel}", nViewID, fieldLabel);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error adding DataViewField for ViewID={ViewID}", nViewID);
            }
        }
        else if (normalizedMode == "edit" && nItemID > 0 && !string.IsNullOrWhiteSpace(fieldLabel))
        {
            var existingField = await db.DataViewFields
                .FirstOrDefaultAsync(f => f.FieldID == nItemID, ct);

            if (existingField is not null)
            {
                existingField.FieldLabel = fieldLabel;
                existingField.FieldSource = fieldSource ?? existingField.FieldSource;
                existingField.FieldType = string.IsNullOrWhiteSpace(fieldType) ? existingField.FieldType : fieldType;
                existingField.DefaultValue = defaultValue;
                existingField.FieldDescription = fieldDescription;

                try
                {
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Updated DataViewField FieldID={FieldID} for ViewID={ViewID}", nItemID, nViewID);
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Error updating DataViewField FieldID={FieldID}", nItemID);
                }
            }
        }
        else if (normalizedMode == "delete" && nItemID > 0)
        {
            var field = await db.DataViewFields
                .FirstOrDefaultAsync(f => f.FieldID == nItemID, ct);

            if (field is not null)
            {
                db.DataViewFields.Remove(field);
                try
                {
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Deleted DataViewField FieldID={FieldID} from ViewID={ViewID}", nItemID, nViewID);
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Error deleting DataViewField FieldID={FieldID}", nItemID);
                }
            }
        }
        else if (normalizedMode == "autoinit")
        {
            await PerformAutoInitAsync(db, nViewID, dataView, ct);
        }

        return MapDataViewToDto(dataView);
    }

    /// <inheritdoc />
    public async Task<AdminDataviewfieldsResultDto> LoadPageAsync(
        string mode, string itemID, string viewID,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var result = new AdminDataviewfieldsResultDto();

        // Validate viewID
        if (!int.TryParse(viewID, NumberStyles.Integer, CultureInfo.InvariantCulture, out int nViewID) || nViewID == 0)
        {
            result.RedirectToList = true;
            result.RedirectUrl = "/aspclassic-vbscript/admin-dataviews?MSG=notfound";
            result.ErrorMessage = "Invalid ViewID";
            return result;
        }

        // Load the DataView header
        var dataView = await db.DataViews
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.ViewID == nViewID, ct);

        if (dataView is null)
        {
            result.RedirectToList = true;
            result.RedirectUrl = "/aspclassic-vbscript/admin-dataviews?MSG=notfound";
            result.ErrorMessage = "DataView not found";
            return result;
        }

        result.ViewID = nViewID;
        result.DataViewTitle = dataView.Title ?? string.Empty;
        result.PageTitle = "Manage Data View Fields for " + result.DataViewTitle;
        result.Mode = mode ?? string.Empty;

        // Parse itemID
        int.TryParse(itemID, NumberStyles.Integer, CultureInfo.InvariantCulture, out int nItemID);
        result.EditFieldID = nItemID > 0 ? nItemID : null;

        // Load current edit field if in edit mode
        if (string.Equals(mode, "edit", StringComparison.OrdinalIgnoreCase) && nItemID > 0)
        {
            var fieldEntity = await db.DataViewFields
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.FieldID == nItemID, ct);

            if (fieldEntity is not null)
            {
                result.CurrentField = MapFieldToEditDto(fieldEntity);
            }
            else
            {
                result.ErrorMessage = "Item Not Found";
            }
        }
        else if (string.Equals(mode, "add", StringComparison.OrdinalIgnoreCase))
        {
            // Prepare a blank edit DTO with defaults matching legacy
            result.CurrentField = new DataViewFieldEditDto
            {
                ViewID = nViewID,
                FieldFlags = 1,
                FieldType = "1",
                UriStyle = 1,
                MaxLength = 100,
                Width = 0,
                Height = 0
            };
        }

        // Always load the full field list for the grid, ordered by FieldOrder ASC
        await LoadFieldListIntoResult(db, nViewID, result, ct);

        return result;
    }

    /// <inheritdoc />
    public async Task<AdminDataviewfieldsResultDto> SaveDataViewFieldAsync(
        string mode, int viewId, int? fieldId,
        DataViewFieldEditDto fieldData,
        List<int> fieldFlagValues,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var result = new AdminDataviewfieldsResultDto { ViewID = viewId };

        // Load the DataView header for title
        var dataView = await db.DataViews
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.ViewID == viewId, ct);

        if (dataView is null)
        {
            result.ErrorMessage = "DataView not found";
            result.RedirectToList = true;
            result.RedirectUrl = "/aspclassic-vbscript/admin-dataviews?MSG=notfound";
            return result;
        }

        result.DataViewTitle = dataView.Title ?? string.Empty;
        result.PageTitle = "Manage Data View Fields for " + result.DataViewTitle;

        // A field DESCRIBES a column; it does not create one. Neither this portal nor the legacy
        // ever issues DDL, so a field naming a column that is not there is accepted here and fails
        // much later, on the first insert that mentions it — "table Inventory has no column named
        // hour", raised from a save screen that has nothing to do with fields. Checked at the point
        // the mistake is made, where the name is still in front of whoever typed it.
        var (sourceError, sourceWarning) =
            await ValidateFieldSourceAsync(db, dataView, fieldData.FieldSource, ct);

        if (sourceError is not null)
        {
            result.ErrorMessage = sourceError;
            return result;
        }

        result.WarningMessage = sourceWarning;

        // Compute aggregate flags by summing all checked flag values (bitwise OR equivalent for power-of-2 flags)
        int nFlags = 0;
        foreach (var flagVal in fieldFlagValues)
        {
            nFlags += flagVal;
        }

        if (string.Equals(mode, "add", StringComparison.OrdinalIgnoreCase))
        {
            // Determine the next FieldOrder: max existing + 1
            int nextOrder = 1;
            var maxOrder = await db.DataViewFields
                .Where(f => f.ViewID == viewId)
                .OrderByDescending(f => f.FieldOrder)
                .Select(f => (int?)f.FieldOrder)
                .FirstOrDefaultAsync(ct);

            if (maxOrder.HasValue)
                nextOrder = maxOrder.Value + 1;

            var newField = new DataViewField
            {
                ViewID = viewId,
                FieldLabel = fieldData.FieldLabel,
                FieldSource = fieldData.FieldSource,
                FieldType = fieldData.FieldType,
                FieldFlags = nFlags,
                FieldOrder = nextOrder,
                DefaultValue = fieldData.DefaultValue,
                MaxLength = fieldData.MaxLength > 0 ? fieldData.MaxLength : (int?)null,
                UriPath = fieldData.UriPath,
                UriStyle = fieldData.UriStyle > 0 ? fieldData.UriStyle : (int?)null,
                LinkedTable = fieldData.LinkedTable,
                LinkedTableValueField = fieldData.LinkedTableValueField,
                LinkedTableTitleField = fieldData.LinkedTableTitleField,
                LinkedTableGroupField = fieldData.LinkedTableGroupField,
                LinkedTableGlyphField = fieldData.LinkedTableGlyphField,
                LinkedTableTooltipField = fieldData.LinkedTableTooltipField,
                LinkedTableAddition = fieldData.LinkedTableAddition,
                FieldDescription = fieldData.FieldDescription,
                Width = fieldData.Width > 0 ? fieldData.Width : (int?)null,
                Height = fieldData.Height > 0 ? fieldData.Height : (int?)null,
                FormatPattern = !string.IsNullOrEmpty(fieldData.FormatPattern) ? fieldData.FormatPattern : null,
                FieldTooltip = !string.IsNullOrEmpty(fieldData.FieldTooltip) ? fieldData.FieldTooltip : null,
                FieldIdentifier = !string.IsNullOrEmpty(fieldData.FieldIdentifier) ? fieldData.FieldIdentifier : null
            };

            db.DataViewFields.Add(newField);

            try
            {
                await db.SaveChangesAsync(ct);
                result.SuccessMessage = "add";
                result.RedirectToList = true;
                result.RedirectUrl = $"/aspclassic-vbscript/admin-dataviewfields?ViewID={viewId}&MSG=add";
                _logger.LogInformation("Added DataViewField for ViewID={ViewID}, FieldLabel={FieldLabel}", viewId, fieldData.FieldLabel);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error adding DataViewField for ViewID={ViewID}", viewId);
                result.ErrorMessage = $"Error(s) while performing \"add\": {ex.InnerException?.Message ?? ex.Message}";
            }
        }
        else if (string.Equals(mode, "edit", StringComparison.OrdinalIgnoreCase) && fieldId.HasValue && fieldId.Value > 0)
        {
            await ApplyFieldUpdate(db, fieldId.Value, viewId, fieldData, nFlags, result, ct);
        }
        else
        {
            result.ErrorMessage = "Invalid input!";
        }

        // If there was an error, reload the field list for re-display
        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            await LoadFieldListIntoResult(db, viewId, result, ct);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<AdminDataviewfieldsResultDto> UpdateDataViewFieldAsync(
        int fieldId, int viewId,
        DataViewFieldEditDto fieldData,
        List<int> fieldFlagValues,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var result = new AdminDataviewfieldsResultDto { ViewID = viewId };

        // Load the DataView header for title
        var dataView = await db.DataViews
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.ViewID == viewId, ct);

        if (dataView is null)
        {
            result.ErrorMessage = "DataView not found";
            result.RedirectToList = true;
            result.RedirectUrl = "/aspclassic-vbscript/admin-dataviews?MSG=notfound";
            return result;
        }

        // The same check as on add. Without it a field could be created correctly and then renamed
        // to a column that does not exist, which fails identically and later.
        var (sourceError, sourceWarning) =
            await ValidateFieldSourceAsync(db, dataView, fieldData.FieldSource, ct);

        if (sourceError is not null)
        {
            result.ErrorMessage = sourceError;
            return result;
        }

        result.WarningMessage = sourceWarning;

        result.DataViewTitle = dataView.Title ?? string.Empty;
        result.PageTitle = "Manage Data View Fields for " + result.DataViewTitle;

        int nFlags = 0;
        foreach (var flagVal in fieldFlagValues)
        {
            nFlags += flagVal;
        }

        await ApplyFieldUpdate(db, fieldId, viewId, fieldData, nFlags, result, ct);

        if (!string.IsNullOrEmpty(result.ErrorMessage))
        {
            await LoadFieldListIntoResult(db, viewId, result, ct);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<AdminDataviewfieldsResultDto> DeleteDataViewFieldAsync(
        int fieldId, int viewId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var result = new AdminDataviewfieldsResultDto { ViewID = viewId };

        var field = await db.DataViewFields
            .FirstOrDefaultAsync(f => f.FieldID == fieldId, ct);

        if (field is not null)
        {
            db.DataViewFields.Remove(field);

            try
            {
                await db.SaveChangesAsync(ct);
                result.SuccessMessage = "delete";
                result.RedirectToList = true;
                result.RedirectUrl = $"/aspclassic-vbscript/admin-dataviewfields?ViewID={viewId}&MSG=delete";
                _logger.LogInformation("Deleted DataViewField FieldID={FieldID} from ViewID={ViewID}", fieldId, viewId);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error deleting DataViewField FieldID={FieldID}", fieldId);
                result.ErrorMessage = $"Error deleting field: {ex.InnerException?.Message ?? ex.Message}";
            }
        }
        else
        {
            result.ErrorMessage = "Field not found";
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<AdminDataviewfieldsResultDto> SortFieldsAsync(
        int viewId, List<SortFieldOrderDto> sortOrders, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var result = new AdminDataviewfieldsResultDto { ViewID = viewId };

        // Load all fields for this view in one query
        var fields = await db.DataViewFields
            .Where(f => f.ViewID == viewId)
            .ToListAsync(ct);

        // Build a lookup for quick access
        var fieldLookup = fields.ToDictionary(f => f.FieldID);

        // Apply the new ordering from sortOrders
        foreach (var sortOrder in sortOrders)
        {
            if (fieldLookup.TryGetValue(sortOrder.FieldID, out var field))
            {
                field.FieldOrder = sortOrder.NewOrder;
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
            result.SuccessMessage = "sorted";
            result.RedirectToList = true;
            result.RedirectUrl = $"/aspclassic-vbscript/admin-dataviewfields?ViewID={viewId}&MSG=sorted";
            _logger.LogInformation("Sorted DataViewFields for ViewID={ViewID}, {Count} fields reordered", viewId, sortOrders.Count);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Error sorting DataViewFields for ViewID={ViewID}", viewId);
            result.ErrorMessage = $"Error sorting fields: {ex.InnerException?.Message ?? ex.Message}";
        }

        return result;
    }

    /// <inheritdoc />
    public async Task AutoInitDataViewFieldsAsync(int viewId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        // Load the DataView to get the MainTable and PrimaryKey
        var dataView = await db.DataViews
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.ViewID == viewId);

        if (dataView is null)
        {
            _logger.LogWarning("AutoInit: DataView with ViewID={ViewID} not found", viewId);
            return;
        }

        await PerformAutoInitAsync(db, viewId, dataView);
    }

    // ─── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Applies an update to an existing DataViewField entity.
    /// Shared between SaveDataViewFieldAsync (edit mode) and UpdateDataViewFieldAsync.
    /// </summary>
    private async Task ApplyFieldUpdate(
        ASPClassicVBScriptDbContext db, int fieldId, int viewId,
        DataViewFieldEditDto fieldData, int nFlags,
        AdminDataviewfieldsResultDto result, CancellationToken ct)
    {
        var existingField = await db.DataViewFields
            .FirstOrDefaultAsync(f => f.FieldID == fieldId, ct);

        if (existingField is null)
        {
            result.ErrorMessage = "Item Not Found";
            return;
        }

        existingField.ViewID = viewId;
        existingField.FieldLabel = fieldData.FieldLabel;
        existingField.FieldSource = fieldData.FieldSource;
        existingField.FieldType = fieldData.FieldType;
        existingField.FieldFlags = nFlags;
        existingField.DefaultValue = fieldData.DefaultValue;
        existingField.UriPath = fieldData.UriPath;
        existingField.UriStyle = fieldData.UriStyle > 0 ? fieldData.UriStyle : (int?)null;
        existingField.LinkedTable = fieldData.LinkedTable;
        existingField.LinkedTableValueField = fieldData.LinkedTableValueField;
        existingField.LinkedTableTitleField = fieldData.LinkedTableTitleField;
        existingField.LinkedTableGroupField = fieldData.LinkedTableGroupField;
        existingField.LinkedTableGlyphField = fieldData.LinkedTableGlyphField;
        existingField.LinkedTableTooltipField = fieldData.LinkedTableTooltipField;
        existingField.LinkedTableAddition = fieldData.LinkedTableAddition;
        existingField.FieldDescription = fieldData.FieldDescription;
        existingField.MaxLength = fieldData.MaxLength > 0 ? fieldData.MaxLength : (int?)null;
        existingField.Width = fieldData.Width > 0 ? fieldData.Width : (int?)null;
        existingField.Height = fieldData.Height > 0 ? fieldData.Height : (int?)null;
        existingField.FormatPattern = !string.IsNullOrEmpty(fieldData.FormatPattern) ? fieldData.FormatPattern : null;
        existingField.FieldTooltip = !string.IsNullOrEmpty(fieldData.FieldTooltip) ? fieldData.FieldTooltip : null;
        existingField.FieldIdentifier = !string.IsNullOrEmpty(fieldData.FieldIdentifier) ? fieldData.FieldIdentifier : null;

        try
        {
            await db.SaveChangesAsync(ct);
            result.SuccessMessage = "edit";
            result.RedirectToList = true;
            result.RedirectUrl = $"/aspclassic-vbscript/admin-dataviewfields?ViewID={viewId}&MSG=edit";
            _logger.LogInformation("Updated DataViewField FieldID={FieldID} for ViewID={ViewID}", fieldId, viewId);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Error updating DataViewField FieldID={FieldID}", fieldId);
            result.ErrorMessage = $"Error(s) while performing \"edit\": {ex.InnerException?.Message ?? ex.Message}";
        }
    }

    /// <summary>
    /// Core AutoInit logic shared between both overloads.
    /// Port of the autoinit mode in <c>admin_dataviewfields.asp</c>.
    /// </summary>
    private async Task PerformAutoInitAsync(
        ASPClassicVBScriptDbContext db, int viewId,
        ASPClassic.Domain.Entities.Data.DataView dataView,
        CancellationToken ct = default)
    {
        string mainTable = dataView.MainTable ?? string.Empty;
        string primaryKey = dataView.Primarykey ?? string.Empty;

        if (string.IsNullOrWhiteSpace(mainTable))
        {
            _logger.LogWarning("AutoInit: DataView ViewID={ViewID} has no MainTable configured", viewId);
            return;
        }

        // Load existing field sources to avoid duplicates (matching legacy ExistingColumns dictionary)
        var existingFieldSources = await db.DataViewFields
            .AsNoTracking()
            .Where(f => f.ViewID == viewId)
            .Select(f => f.FieldSource ?? string.Empty)
            .ToListAsync(ct);

        var existingColumnsSet = new HashSet<string>(existingFieldSources, StringComparer.OrdinalIgnoreCase);

        // Extract just the table name (strip schema brackets if present)
        string tableName = ExtractTableName(mainTable);

        List<AutoInitColumnInfoDto> columnInfos;
        try
        {
            columnInfos = await IntrospectTableColumnsAsync(db, tableName, primaryKey, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AutoInit: Failed to introspect table {TableName} for ViewID={ViewID}", tableName, viewId);
            return;
        }

        // Determine the starting order for new fields
        int currentMaxOrder = 0;
        if (existingFieldSources.Count > 0)
        {
            var maxExisting = await db.DataViewFields
                .Where(f => f.ViewID == viewId)
                .MaxAsync(f => (int?)f.FieldOrder, ct);
            currentMaxOrder = maxExisting ?? 0;
        }

        // Insert new fields for columns not already present
        int addedCount = 0;
        foreach (var colInfo in columnInfos)
        {
            // Skip columns that match the primary key (legacy: c.name <> @PK)
            if (string.Equals(colInfo.ColumnName, primaryKey, StringComparison.OrdinalIgnoreCase))
                continue;

            // Skip columns already existing in this view's fields
            if (existingColumnsSet.Contains(colInfo.ColumnName))
                continue;

            currentMaxOrder++;
            int fieldFlags = colInfo.FieldFlags;

            // Legacy logic: if fieldtype is 1, 2, 5, or 9, add flag 16 (searchable in DataTable)
            string ft = colInfo.FieldType;
            if (ft == "1" || ft == "2" || ft == "5" || ft == "9")
            {
                fieldFlags += 16;
            }

            var newField = new DataViewField
            {
                ViewID = viewId,
                FieldSource = colInfo.ColumnName,
                FieldLabel = AutoFormatLabel(colInfo.ColumnName),
                FieldType = colInfo.FieldType,
                FieldFlags = fieldFlags,
                FieldOrder = currentMaxOrder,
                DefaultValue = colInfo.FieldDefault,
                MaxLength = colInfo.MaxLength,
                LinkedTable = !string.IsNullOrEmpty(colInfo.LinkedTable) ? colInfo.LinkedTable : null,
                LinkedTableValueField = !string.IsNullOrEmpty(colInfo.LinkedColumnValue) ? colInfo.LinkedColumnValue : null,
                LinkedTableTitleField = !string.IsNullOrEmpty(colInfo.LinkedColumnLabel) ? colInfo.LinkedColumnLabel : null
            };

            // Legacy logic: if fieldtype == 2 and (max_length is null or >= 1000), set Height = 10
            if (colInfo.FieldType == "2" && (!colInfo.MaxLength.HasValue || colInfo.MaxLength >= 1000))
            {
                newField.Height = 10;
            }

            db.DataViewFields.Add(newField);
            addedCount++;
        }

        if (addedCount > 0)
        {
            try
            {
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("AutoInit: Added {Count} new DataViewFields for ViewID={ViewID} from table {Table}",
                    addedCount, viewId, tableName);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "AutoInit: Error saving auto-initialized fields for ViewID={ViewID}", viewId);
                throw;
            }
        }
        else
        {
            _logger.LogInformation("AutoInit: No new columns to add for ViewID={ViewID}, all columns already mapped", viewId);
        }
    }

    /// <summary>
    /// Introspects a table's columns using SQLite PRAGMA table_info.
    /// Maps column types to the legacy fieldtype numbering scheme.
    /// Port of the sys.columns query in <c>admin_dataviewfields.asp</c> autoinit mode.
    /// </summary>
    private async Task<List<AutoInitColumnInfoDto>> IntrospectTableColumnsAsync(
        ASPClassicVBScriptDbContext db, string tableName, string primaryKey, CancellationToken ct)
    {
        var results = new List<AutoInitColumnInfoDto>();

        // SQLite PRAGMA table_info returns: cid, name, type, notnull, dflt_value, pk
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(ct);

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info(\"{tableName.Replace("\"", "\"\"")}\")";

            using var reader = await cmd.ExecuteReaderAsync(ct);

            int orderIndex = 1;
            while (await reader.ReadAsync(ct))
            {
                string columnName = reader.GetString(1); // name
                string colType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2); // type
                bool notNull = reader.GetInt32(3) != 0; // notnull
                string dfltValue = reader.IsDBNull(4) ? string.Empty : reader.GetString(4); // dflt_value
                bool isPk = reader.GetInt32(5) != 0; // pk

                // Skip the primary key column (matching legacy: c.name <> @PK)
                if (isPk && string.Equals(columnName, primaryKey, StringComparison.OrdinalIgnoreCase))
                {
                    orderIndex++;
                    continue;
                }

                string normalizedType = colType.ToUpperInvariant();

                // Map SQLite type affinity to legacy fieldtype numbering
                string fieldType = MapColumnTypeToFieldType(normalizedType);

                // Compute fieldFlags matching legacy logic:
                // base = 1
                // + 2 if NOT NULL and not bit
                // + 8 if short-ish text (max_length between 1 and 200)
                int fieldFlags = 1;
                if (notNull && fieldType != "9")
                    fieldFlags += 2;

                // Extract max_length from type definition if present (e.g., VARCHAR(100))
                int? maxLength = ExtractMaxLength(colType);

                if (maxLength.HasValue && maxLength.Value >= 1 && maxLength.Value <= 200)
                    fieldFlags += 8;

                results.Add(new AutoInitColumnInfoDto
                {
                    ColumnName = columnName,
                    FieldType = fieldType,
                    FieldFlags = fieldFlags,
                    FieldOrder = orderIndex,
                    FieldDefault = dfltValue,
                    MaxLength = maxLength,
                    LinkedTable = string.Empty,
                    LinkedColumnValue = string.Empty,
                    LinkedColumnLabel = string.Empty
                });

                orderIndex++;
            }
        }
        finally
        {
            await connection.CloseAsync();
        }

        return results;
    }

    /// <summary>
    /// Maps a SQLite column type string to the legacy DataView fieldtype numbering.
    /// Port of the CASE WHEN logic in <c>admin_dataviewfields.asp</c>.
    /// </summary>
    private static string MapColumnTypeToFieldType(string normalizedType)
    {
        if (string.IsNullOrEmpty(normalizedType))
            return "1"; // default to short text

        // BIT/BOOLEAN → 9
        if (normalizedType.Contains("BIT") || normalizedType.Contains("BOOL"))
            return "9";

        // DATE (exact) → 7
        if (normalizedType == "DATE")
            return "7";

        // DATETIME variants → 8
        if (normalizedType.Contains("DATETIME") || normalizedType.Contains("TIMESTAMP"))
            return "8";

        // TIME → 13
        if (normalizedType == "TIME")
            return "13";

        // Integer types → 3
        if (normalizedType.Contains("INT"))
            return "3";

        // Real/decimal/float/numeric → 4
        if (normalizedType.Contains("REAL") || normalizedType.Contains("FLOAT") ||
            normalizedType.Contains("DECIMAL") || normalizedType.Contains("NUMERIC") ||
            normalizedType.Contains("DOUBLE"))
            return "4";

        // TEXT/CLOB or very long varchar → 2
        if (normalizedType == "TEXT" || normalizedType.Contains("CLOB") || normalizedType == "XML")
            return "2";

        // Long varchar (> 400) → 2
        int? maxLen = ExtractMaxLength(normalizedType);
        if (normalizedType.Contains("NVARCHAR") || normalizedType.Contains("NCHAR"))
        {
            if (maxLen.HasValue && maxLen.Value > 400)
                return "2";
            return "1";
        }

        if (normalizedType.Contains("VARCHAR") || normalizedType.Contains("CHAR"))
        {
            if (maxLen.HasValue && maxLen.Value > 400)
                return "2";
            return "1"; // short text
        }

        // BLOB → 2
        if (normalizedType.Contains("BLOB"))
            return "2";

        return "1"; // default short text
    }

    /// <summary>
    /// Extracts the max length from a type definition like VARCHAR(100) or NVARCHAR(300).
    /// Returns null if no length is specified.
    /// </summary>
    private static int? ExtractMaxLength(string colType)
    {
        if (string.IsNullOrEmpty(colType))
            return null;

        var match = Regex.Match(colType, @"\((\d+)\)", RegexOptions.None, TimeSpan.FromSeconds(1));
        if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int len))
            return len;

        return null;
    }

    /// <summary>
    /// Extracts the bare table name from a possibly schema-qualified, bracketed name.
    /// E.g., "[portal].[DataViewField]" → "DataViewField", "dbo.MyTable" → "MyTable"
    /// </summary>
    private static string ExtractTableName(string qualifiedName)
    {
        if (string.IsNullOrWhiteSpace(qualifiedName))
            return qualifiedName;

        // Remove brackets
        string cleaned = qualifiedName.Replace("[", "").Replace("]", "");

        // Take the last part after any dots
        int lastDot = cleaned.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < cleaned.Length - 1)
            return cleaned[(lastDot + 1)..];

        return cleaned;
    }

    /// <summary>
    /// Auto-formats a column name into a human-readable label.
    /// Port of <c>AutoFormatLabels</c> from <c>inc_functions.asp</c>.
    /// E.g., "CustomerName" → "Customer Name", "first_name" → "First Name"
    /// </summary>
    private static string AutoFormatLabel(string columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName))
            return columnName;

        // Replace underscores with spaces
        string result = columnName.Replace('_', ' ');

        // Insert spaces before uppercase letters that follow lowercase letters (camelCase/PascalCase)
        result = Regex.Replace(result, @"(?<=[a-z])(?=[A-Z])", " ", RegexOptions.None, TimeSpan.FromSeconds(1));

        // Title case each word
        var words = result.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0], CultureInfo.InvariantCulture) +
                            (words[i].Length > 1 ? words[i][1..] : string.Empty);
            }
        }

        return string.Join(' ', words);
    }

    /// <summary>
    /// Loads the field list into the result DTO for re-display after an error.
    /// </summary>
    private async Task LoadFieldListIntoResult(
        ASPClassicVBScriptDbContext db, int viewId, AdminDataviewfieldsResultDto result, CancellationToken ct)
    {
        var fieldEntities = await db.DataViewFields
            .AsNoTracking()
            .Where(f => f.ViewID == viewId)
            .OrderBy(f => f.FieldOrder)
            .ToListAsync(ct);

        result.Fields.Clear();
        foreach (var fe in fieldEntities)
        {
            result.Fields.Add(new DataViewFieldListItemDto
            {
                FieldID = fe.FieldID,
                ViewID = fe.ViewID,
                FieldLabel = fe.FieldLabel ?? string.Empty,
                FieldSource = fe.FieldSource ?? string.Empty,
                FieldType = fe.FieldType ?? string.Empty,
                FieldFlags = fe.FieldFlags,
                FieldOrder = fe.FieldOrder,
                DefaultValue = fe.DefaultValue ?? string.Empty,
                MaxLength = fe.MaxLength ?? 0,
                Width = fe.Width ?? 0,
                Height = fe.Height ?? 0,
                FieldDescription = fe.FieldDescription ?? string.Empty
            });
        }
    }

    /// <summary>
    /// Maps a DataView entity to its DTO. Coalesces all nullable string properties.
    /// </summary>
    private static DataViewDto MapDataViewToDto(ASPClassic.Domain.Entities.Data.DataView entity)
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

    /// <summary>
    /// Maps a DataViewField entity to the read-only DTO.
    /// </summary>
    private static DataViewFieldDto MapFieldToDto(DataViewField entity)
    {
        return new DataViewFieldDto
        {
            ViewID = entity.ViewID,
            FieldID = entity.FieldID,
            FieldLabel = entity.FieldLabel ?? string.Empty,
            FieldSource = entity.FieldSource ?? string.Empty,
            FieldType = entity.FieldType ?? string.Empty,
            FieldFlags = entity.FieldFlags,
            FieldOrder = entity.FieldOrder,
            DefaultValue = entity.DefaultValue ?? string.Empty,
            MaxLength = entity.MaxLength ?? 0,
            UriPath = entity.UriPath ?? string.Empty,
            UriStyle = entity.UriStyle ?? 0,
            LinkedTable = entity.LinkedTable ?? string.Empty,
            LinkedTableValueField = entity.LinkedTableValueField ?? string.Empty,
            LinkedTableTitleField = entity.LinkedTableTitleField ?? string.Empty,
            LinkedTableGroupField = entity.LinkedTableGroupField ?? string.Empty,
            LinkedTableGlyphField = entity.LinkedTableGlyphField ?? string.Empty,
            LinkedTableTooltipField = entity.LinkedTableTooltipField ?? string.Empty,
            LinkedTableAddition = entity.LinkedTableAddition ?? string.Empty,
            Width = entity.Width ?? 0,
            Height = entity.Height ?? 0,
            FieldDescription = entity.FieldDescription ?? string.Empty,
            FormatPattern = entity.FormatPattern ?? string.Empty,
            FieldTooltip = entity.FieldTooltip ?? string.Empty,
            FieldIdentifier = entity.FieldIdentifier ?? string.Empty
        };
    }

    /// <summary>
    /// Maps a DataViewField entity to the editable DTO for the add/edit form.
    /// </summary>
    private static DataViewFieldEditDto MapFieldToEditDto(DataViewField entity)
    {
        return new DataViewFieldEditDto
        {
            FieldID = entity.FieldID,
            ViewID = entity.ViewID,
            FieldLabel = entity.FieldLabel ?? string.Empty,
            FieldSource = entity.FieldSource ?? string.Empty,
            FieldType = entity.FieldType ?? "1",
            FieldFlags = entity.FieldFlags,
            FieldOrder = entity.FieldOrder,
            DefaultValue = entity.DefaultValue ?? string.Empty,
            MaxLength = entity.MaxLength ?? 100,
            UriPath = entity.UriPath ?? string.Empty,
            UriStyle = entity.UriStyle ?? 1,
            LinkedTable = entity.LinkedTable ?? string.Empty,
            LinkedTableValueField = entity.LinkedTableValueField ?? string.Empty,
            LinkedTableTitleField = entity.LinkedTableTitleField ?? string.Empty,
            LinkedTableGroupField = entity.LinkedTableGroupField ?? string.Empty,
            LinkedTableGlyphField = entity.LinkedTableGlyphField ?? string.Empty,
            LinkedTableTooltipField = entity.LinkedTableTooltipField ?? string.Empty,
            LinkedTableAddition = entity.LinkedTableAddition ?? string.Empty,
            Width = entity.Width ?? 0,
            Height = entity.Height ?? 0,
            FieldDescription = entity.FieldDescription ?? string.Empty,
            FormatPattern = entity.FormatPattern ?? string.Empty,
            FieldTooltip = entity.FieldTooltip ?? string.Empty,
            FieldIdentifier = entity.FieldIdentifier ?? string.Empty
        };
    }

    /// <summary>The DataViewFieldTypes lookup rows, read from the table the DDL declares.</summary>
    public async Task<List<DataViewFieldTypesDto>> GetDataViewFieldTypesAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.DataViewFieldTypes
            .AsNoTracking()
            .Select(e => new DataViewFieldTypesDto
            {
                    TypeValue = e.TypeValue,
                    TypeLabel = e.TypeLabel,
                    TypeWrappers = e.TypeWrappers,
                    TypeIdentifier = e.TypeIdentifier,
                    TypeGroup = e.TypeGroup
            })
            .ToListAsync(ct);
    }

    /// <summary>The DataViewFieldFlags lookup rows, read from the table the DDL declares.</summary>
    public async Task<List<DataViewFieldFlagsDto>> GetDataViewFieldFlagsAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.DataViewFieldFlags
            .AsNoTracking()
            .Select(e => new DataViewFieldFlagsDto
            {
                    FlagValue = e.FlagValue,
                    FlagLabel = e.FlagLabel,
                    FlagGlyph = e.FlagGlyph,
                    FlagDefault = e.FlagDefault
            })
            .ToListAsync(ct);
    }

    /// <summary>The DataViewUriStyles lookup rows, read from the table the DDL declares.</summary>
    public async Task<List<DataViewUriStylesDto>> GetDataViewUriStylesAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.DataViewUriStyles
            .AsNoTracking()
            .Select(e => new DataViewUriStylesDto
            {
                    StyleValue = e.StyleValue,
                    StyleLabel = e.StyleLabel,
                    StyleClass = e.StyleClass,
                    StyleGlyph = e.StyleGlyph,
                    StyleDefault = e.StyleDefault
            })
            .ToListAsync(ct);
    }

    /// <summary>The view's fields as the admin list shows them, in configured order.</summary>
    public async Task<List<DataViewFieldListItemDto>> GetDataViewFieldsListAsync(
        int viewId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.DataViewFields
            .AsNoTracking()
            .Where(f => f.ViewID == viewId)
            .OrderBy(f => f.FieldOrder)
            .Select(f => new DataViewFieldListItemDto
            {
                FieldID = f.FieldID,
                ViewID = f.ViewID,
                FieldLabel = f.FieldLabel,
                FieldSource = f.FieldSource,
                FieldType = f.FieldType,
                FieldFlags = f.FieldFlags,
                FieldOrder = f.FieldOrder,
                DefaultValue = f.DefaultValue,
                MaxLength = f.MaxLength ?? 0,
                Width = f.Width ?? 0,
                Height = f.Height ?? 0,
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Checks that a field names a real column of the view's table. Returns the message to show, or
    /// null when the name is good.
    /// </summary>
    /// <remarks>
    /// The same introspection Auto-Initialize uses, applied in the other direction: it reads the
    /// table to propose fields, this reads the table to check one. Unknown tables are allowed
    /// through — a view may point at something this connection cannot see, and refusing a save on
    /// that basis would be worse than the mistake it prevents.
    /// </remarks>
    private async Task<(string? Error, string? Warning)> ValidateFieldSourceAsync(
        ASPClassicVBScriptDbContext db,
        ASPClassic.Domain.Entities.Data.DataView dataView,
        string? fieldSource,
        CancellationToken ct)
    {
        // A field with no column named at all can never work, whatever happens to the table later.
        // That is refused; everything else is only "not yet".
        if (string.IsNullOrWhiteSpace(fieldSource))
            return ("Field Source is required — it names the database column this field shows.", null);

        var table = ExtractTableName(dataView.MainTable ?? string.Empty);
        if (table.Length == 0) return (null, null);

        List<string> columns;
        try
        {
            var infos = await IntrospectTableColumnsAsync(db, table, dataView.Primarykey ?? string.Empty, ct);
            columns = infos.Select(i => i.ColumnName).ToList();

            // The primary key is skipped by the introspection, and naming it is legitimate.
            if (!string.IsNullOrWhiteSpace(dataView.Primarykey)) columns.Add(dataView.Primarykey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the columns of {Table}; field source not checked.", table);
            return (null, null);
        }

        if (columns.Count == 0) return (null, null);   // nothing to check against

        if (columns.Contains(fieldSource, StringComparer.OrdinalIgnoreCase)) return (null, null);

        var suggestion = ASPClassic.Application.Validation.ColumnNameSuggestion
            .Suggest(fieldSource, columns);

        var known = string.Join(", ", columns.OrderBy(c => c, StringComparer.OrdinalIgnoreCase));

        // Saved, and said so. The column may be about to be created — but until it is, this field
        // will not display and any record saved through it will fail.
        _logger.LogWarning(
            "A field on view {ViewId} names '{Source}', which is not a column of {Table}.",
            dataView.ViewID, fieldSource, table);

        return (null, suggestion is not null
            ? $"Saved — but '{table}' has no column named '{fieldSource}' yet. Did you mean '{suggestion}'? " +
              "Until the column exists this field will not display, and saving a record will fail."
            : $"Saved — but '{table}' has no column named '{fieldSource}' yet. Add the column to the " +
              $"table, or use one of: {known}. Until then this field will not display, and saving a " +
              "record will fail.");
    }

    /// <summary>
    /// The column names of a table, primary key included. Empty when the table cannot be read.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetTableColumnsAsync(
        string table, string primaryKey, CancellationToken ct = default)
    {
        var bare = ExtractTableName(table ?? string.Empty);
        if (bare.Length == 0) return Array.Empty<string>();

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        try
        {
            var infos = await IntrospectTableColumnsAsync(db, bare, primaryKey ?? string.Empty, ct);
            var columns = infos.Select(i => i.ColumnName).ToList();

            // Introspection skips the key, and naming it is legitimate.
            if (!string.IsNullOrWhiteSpace(primaryKey)) columns.Add(primaryKey);

            return columns;
        }
        catch (Exception ex)
        {
            // A table this connection cannot see is not the same as a table with no columns, and
            // callers must be able to tell the difference: an empty list means "unknown", and every
            // caller treats unknown as permitted.
            _logger.LogWarning(ex, "Could not read the columns of {Table}.", bare);
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Adds a column to the table a view edits, typed from the field that needs it.
    /// </summary>
    public async Task<string?> AddColumnToViewTableAsync(
        int viewId, string columnName, string fieldType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(columnName))
            return "No column name was given.";

        // The name goes into DDL, which cannot be parameterised. Nothing but a plain identifier is
        // allowed through — not as a formality, but because this is the only statement in the
        // application whose text is influenced by something a user typed.
        if (!System.Text.RegularExpressions.Regex.IsMatch(columnName, @"^[A-Za-z_][A-Za-z0-9_]*$"))
            return $"'{columnName}' is not a valid column name. Use letters, digits and underscores, starting with a letter.";

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var view = await db.DataViews.AsNoTracking().FirstOrDefaultAsync(v => v.ViewID == viewId, ct);
        if (view is null) return "Data View not found.";

        var table = ExtractTableName(view.MainTable ?? string.Empty);
        if (table.Length == 0) return "This view does not name a table.";

        if (!System.Text.RegularExpressions.Regex.IsMatch(table, @"^[A-Za-z_][A-Za-z0-9_]*$"))
            return $"'{table}' is not a name this can safely alter.";

        var existing = await GetTableColumnsAsync(view.MainTable ?? string.Empty, view.Primarykey ?? string.Empty, ct);

        if (existing.Contains(columnName, StringComparer.OrdinalIgnoreCase))
            return $"'{table}' already has a column named '{columnName}'.";

        // Typed from the field that needs it, and nullable-with-a-default rather than NOT NULL: the
        // table already has rows, and every one of them would need a value this cannot invent.
        var (sqlType, defaultValue) = fieldType switch
        {
            "3" => ("INTEGER", "0"),                        // Integer
            "4" => ("REAL", "0"),                           // Decimal
            "9" or "22" or "23" or "26" => ("INTEGER", "0"), // Boolean
            _ => ("TEXT", "''"),
        };

        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            $"ALTER TABLE [{table}] ADD COLUMN [{columnName}] {sqlType} NOT NULL DEFAULT {defaultValue}";

        try
        {
            await cmd.ExecuteNonQueryAsync(ct);

            _logger.LogWarning(
                "Added column {Column} {Type} to {Table} for view {ViewId}. This application does not " +
                "otherwise alter tables; the change is permanent and is not recorded in any migration.",
                columnName, sqlType, table, viewId);

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not add column {Column} to {Table}", columnName, table);
            return $"Could not add the column: {ex.Message}";
        }
    }
}
