using System.Data;
using System.Data.Common;
using System.Globalization;
using ASPClassic.Application.DTOs.Data;
using ASPClassic.Application.DTOs.Dataview;
using ASPClassic.Domain.Entities.Data;
using ASPClassic.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ASPClassic.Application.Services.Dataview;

/// <summary>
/// Port of <c>dataview_ngdt.asp</c>.
/// Loads DataView metadata, decodes bitwise flags, manages CRUD operations
/// against the view's underlying table, and returns the full page state.
/// </summary>
public class DataviewNgdtService : IDataviewNgdtService
{
    private readonly IDbContextFactory<ASPClassicVBScriptDbContext> _dbFactory;
    private readonly ILogger<DataviewNgdtService> _logger;

    public DataviewNgdtService(
        IDbContextFactory<ASPClassicVBScriptDbContext> dbFactory,
        ILogger<DataviewNgdtService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <summary>
    /// Port of <c>GetDataViewField()</c> — SELECT on DataViewField WHERE FieldID = @fieldId.
    /// Legacy: opens a recordset on DataViewField filtered by FieldID.
    /// </summary>
    public async Task<DataViewFieldDto?> GetDataViewFieldAsync(int fieldId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.DataViewFields
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.FieldID == fieldId, ct);

        if (entity is null)
            return null;

        return MapFieldToDto(entity);
    }

    /// <summary>
    /// Port of <c>GetDataView()</c> — SELECT on DataView WHERE ViewID = @viewId.
    /// Legacy: opens a recordset on DataView filtered by ViewID.
    /// </summary>
    public async Task<DataViewDto?> GetDataViewAsync(int viewId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.DataViews
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.ViewID == viewId, ct);

        if (entity is null)
            return null;

        return MapViewToDto(entity);
    }

    /// <summary>
    /// Port of <c>LoadDataviewNgdt</c> — returns the first matching DataViewField for the view.
    /// This is the simplified form returning just the field DTO; see <see cref="LoadPageAsync"/>
    /// for the full page state.
    /// </summary>
    public async Task<DataViewFieldDto?> LoadDataviewNgdtAsync(
        string itemID,
        string mode,
        string viewID,
        string postback,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(viewID) || !int.TryParse(viewID, CultureInfo.InvariantCulture, out var nViewID))
            return null;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.DataViewFields
            .AsNoTracking()
            .Where(f => f.ViewID == nViewID)
            .OrderBy(f => f.FieldOrder)
            .FirstOrDefaultAsync(ct);

        if (entity is null)
            return null;

        return MapFieldToDto(entity);
    }

    /// <summary>
    /// Full page load + data manipulation handler for the DataView NGDT page.
    /// Port of <c>dataview_ngdt.asp</c> main body.
    /// Loads the DataView by ViewID, decodes bitwise flags, loads fields,
    /// loads linked table lookups, handles add/edit/delete operations,
    /// loads edit-row data for edit mode, and returns the fully-populated page state.
    /// </summary>
    public async Task<DataviewNgdtLoadResultDto> LoadPageAsync(
        string itemID,
        string mode,
        string viewID,
        string postback,
        Dictionary<string, string>? formValues = null,
        CancellationToken ct = default)
    {
        var result = new DataviewNgdtLoadResultDto();

        // -- Parse inputs (port of Request("ItemID"), Request("mode"), Request("ViewID")) --
        int? nItemID = null;
        if (!string.IsNullOrEmpty(itemID) && int.TryParse(itemID, CultureInfo.InvariantCulture, out var parsedItemId))
        {
            nItemID = parsedItemId;
        }

        if (string.IsNullOrEmpty(mode))
            mode = "none";

        result.Mode = mode;
        result.ItemID = nItemID;

        if (string.IsNullOrEmpty(viewID) || !int.TryParse(viewID, CultureInfo.InvariantCulture, out var nViewID))
        {
            result.Error = "ViewID Invalid!";
            return result;
        }

        result.ViewID = nViewID;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // -- Load DataView record (port of: SELECT * FROM portal.DataView WHERE ViewID = ...) --
        var dataView = await db.DataViews
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.ViewID == nViewID, ct);

        if (dataView is null)
        {
            result.Error = "ViewID Not Found!";
            return result;
        }

        // -- Populate view metadata --
        var dataSource = dataView.DataSource;
        if (string.IsNullOrEmpty(dataSource))
            dataSource = "Default";

        result.DataSource = dataSource;
        result.Title = dataView.Title;
        result.ViewDescription = dataView.ViewDescription ?? string.Empty;
        result.ViewProcedure = dataView.ViewProcedure ?? string.Empty;
        result.ModificationProcedure = dataView.ModificationProcedure ?? string.Empty;
        result.DeleteProcedure = dataView.DeleteProcedure ?? string.Empty;
        result.MainTable = dataView.MainTable ?? string.Empty;
        result.OrderBy = dataView.OrderBy ?? string.Empty;
        result.Primarykey = dataView.Primarykey ?? string.Empty;
        result.CSSTable = dataView.CSSTable;
        result.DataTableModifierButtonStyle = dataView.DataTableModifierButtonStyle;
        result.DataTableDefaultPageSize = dataView.DataTableDefaultPageSize;
        result.DataTablePagingStyle = dataView.DataTablePagingStyle;

        int nViewFlags = dataView.Flags;
        int nDtFlags = dataView.DataTableFlags;

        // -- Decode view flags (port of bitwise AND operations in legacy ASP) --
        result.AllowUpdate = (nViewFlags & 1) > 0;
        result.AllowInsert = (nViewFlags & 2) > 0;
        result.AllowDelete = (nViewFlags & 4) > 0;
        result.AllowClone = (nViewFlags & 8) > 0;
        result.ShowRowActions = result.AllowUpdate || result.AllowDelete || result.AllowClone;
        result.ShowForm = (nViewFlags & 16) > 0;
        result.ShowList = (nViewFlags & 32) > 0;
        result.AllowSearch = (nViewFlags & 64) > 0;
        result.RTEEnabled = (nViewFlags & 128) > 0;
        result.ShowCharts = (nViewFlags & 256) > 0;

        // -- Decode DataTable flags --
        result.DtInfo = (nDtFlags & 1) > 0;
        result.DtColumnFooter = (nDtFlags & 2) > 0;
        result.DtQuickSearch = (nDtFlags & 4) > 0;
        result.DtSort = (nDtFlags & 8) > 0;
        result.DtPagination = (nDtFlags & 16) > 0;
        result.DtPageSizeSelection = (nDtFlags & 32) > 0;
        result.DtStateSave = (nDtFlags & 64) > 0;

        // -- Resolve modifier button style (port of FOR loop over arrDataTableModifierButtonStyles) --
        short nDtModBtnStyle = dataView.DataTableModifierButtonStyle;
        var modBtnStyle = await db.DataViewModifierButtonStyles
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StyleValue == nDtModBtnStyle.ToString(CultureInfo.InvariantCulture), ct);

        if (modBtnStyle is not null)
        {
            result.ModifierButtonStyleClass = modBtnStyle.StyleClass ?? string.Empty;
            result.ModifierShowText = !string.IsNullOrEmpty(modBtnStyle.ShowText)
                && modBtnStyle.ShowText.Equals("true", StringComparison.OrdinalIgnoreCase);
            result.ModifierShowGlyph = !string.IsNullOrEmpty(modBtnStyle.ShowGlyph)
                && modBtnStyle.ShowGlyph.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        // -- Load fields (port of: SELECT * FROM portal.DataViewField WHERE ViewID = ... ORDER BY FieldOrder ASC) --
        var fieldEntities = await db.DataViewFields
            .AsNoTracking()
            .Where(f => f.ViewID == nViewID)
            .OrderBy(f => f.FieldOrder)
            .ToListAsync(ct);

        if (fieldEntities.Count == 0)
        {
            result.Error = "No Fields defined for this Data View";
            return result;
        }

        // -- Load URI styles for column rendering --
        var uriStyles = await db.DataViewUriStyles
            .AsNoTracking()
            .ToListAsync(ct);

        // -- Map fields to DTOs with extended info --
        var fields = new List<DataViewFieldInfoDto>(fieldEntities.Count);
        foreach (var fe in fieldEntities)
        {
            var fieldInfo = new DataViewFieldInfoDto
            {
                ViewID = fe.ViewID,
                FieldID = fe.FieldID,
                FieldLabel = fe.FieldLabel,
                FieldSource = fe.FieldSource ?? string.Empty,
                FieldType = fe.FieldType,
                FieldFlags = fe.FieldFlags,
                FieldOrder = fe.FieldOrder,
                DefaultValue = fe.DefaultValue ?? string.Empty,
                MaxLength = fe.MaxLength ?? 0,
                UriPath = fe.UriPath ?? string.Empty,
                UriStyle = fe.UriStyle ?? 0,
                LinkedTable = fe.LinkedTable ?? string.Empty,
                LinkedTableValueField = fe.LinkedTableValueField ?? string.Empty,
                LinkedTableTitleField = fe.LinkedTableTitleField ?? string.Empty,
                LinkedTableGroupField = fe.LinkedTableGroupField ?? string.Empty,
                LinkedTableGlyphField = fe.LinkedTableGlyphField ?? string.Empty,
                LinkedTableTooltipField = fe.LinkedTableTooltipField ?? string.Empty,
                LinkedTableAddition = fe.LinkedTableAddition ?? string.Empty,
                Width = fe.Width ?? 0,
                Height = fe.Height ?? 0,
                FieldDescription = fe.FieldDescription ?? string.Empty,
                FormatPattern = fe.FormatPattern ?? string.Empty,
                FieldTooltip = fe.FieldTooltip ?? string.Empty,
                FieldIdentifier = fe.FieldIdentifier ?? string.Empty,
                // Decode per-field flags (port of bitwise AND operations per field)
                ShowInForm = (fe.FieldFlags & 1) > 0,
                IsRequired = (fe.FieldFlags & 2) > 0,
                IsReadOnly = (fe.FieldFlags & 4) > 0,
                ShowInList = (fe.FieldFlags & 8) > 0,
            };

            // Resolve URI style class for this field
            if (!string.IsNullOrEmpty(fieldInfo.UriPath))
            {
                var matchingStyle = uriStyles.FirstOrDefault(
                    us => us.StyleValue == (fieldInfo.UriStyle?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));
                if (matchingStyle is not null)
                {
                    fieldInfo.UriStyleClass = matchingStyle.StyleClass ?? string.Empty;
                    fieldInfo.UriStyleGlyph = matchingStyle.StyleGlyph ?? string.Empty;
                }
            }

            fields.Add(fieldInfo);
        }

        result.Fields = fields;

        // -- Build ViewQueryString (port of strViewQueryString) --
        result.ViewQueryString = $"&ViewID={nViewID}";

        // -- Calculate list column count (port of nColSpan logic) --
        int colSpan = 1; // starts at 1 per legacy (row number column)
        foreach (var f in fields)
        {
            if (f.ShowInList)
                colSpan++;
        }
        if (result.ShowRowActions)
            colSpan++; // actions column
        result.ListColumnCount = colSpan;

        // -- Build field defaults map (port of default-value init loop) --
        for (int i = 0; i < fields.Count; i++)
        {
            if (!string.IsNullOrEmpty(fields[i].DefaultValue))
            {
                result.FieldDefaults[i] = fields[i].DefaultValue;
            }
        }

        // -- Load linked table lookup data for combo/multicombo fields --
        // Port of: for each field with a LinkedTable, query that table and populate dropdown options
        await LoadLookupDataAsync(db, fields, result, ct);

        // ************************************
        // Data Manipulation Section
        // ************************************

        bool isPostback = string.Equals(postback, "true", StringComparison.OrdinalIgnoreCase);
        formValues ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // -- ADD / EDIT (port of the add/edit branch) --
        if (string.IsNullOrEmpty(result.Error)
            && ((mode == "add" && result.AllowInsert) || (mode == "edit" && result.AllowUpdate && nItemID.HasValue))
            && isPostback)
        {
            _logger.LogInformation(
                "DataView NGDT: performing {Mode} on ViewID={ViewID}, ItemID={ItemID}",
                mode, nViewID, nItemID);

            if (!string.IsNullOrEmpty(result.ModificationProcedure))
            {
                _logger.LogWarning(
                    "DataView NGDT: ModificationProcedure '{Proc}' configured but stored procedures " +
                    "are not supported on SQLite. Falling back to direct table manipulation.",
                    result.ModificationProcedure);
            }

            // Validate required fields first (port of the FOR loop that checks required fields)
            var validationErrors = new List<string>();

            for (int nIndex = 0; nIndex < fields.Count; nIndex++)
            {
                var field = fields[nIndex];
                string fieldTypeNormalized = ResolveFieldTypeCode(field.FieldType);

                // Skip link fields (type 10) and read-only fields (flag & 4)
                if (fieldTypeNormalized == "10" || field.IsReadOnly)
                    continue;

                // Skip fields not shown in form
                if (!field.ShowInForm)
                    continue;

                string formKey = $"inputField_{nIndex}";
                formValues.TryGetValue(formKey, out var formValue);

                // Check required fields
                if (string.IsNullOrEmpty(formValue) && field.IsRequired)
                {
                    validationErrors.Add($"<b>{System.Net.WebUtility.HtmlEncode(field.FieldLabel)}</b> is required but has not been filled.");
                }
            }

            if (validationErrors.Count > 0)
            {
                result.Error = string.Join("<br/>", validationErrors);
                return result;
            }

            // Perform the actual data manipulation
            try
            {
                await using var dbWrite = await _dbFactory.CreateDbContextAsync(ct);
                await using var transaction = await dbWrite.Database.BeginTransactionAsync(ct);

                try
                {
                    if (mode == "edit" && nItemID.HasValue)
                    {
                        // Port of: SELECT * FROM MainTable WHERE PrimaryKey = nItemID, then rsItems(field) = value
                        var setClauses = new List<string>();
                        var parameters = new List<object>();

                        for (int nIndex = 0; nIndex < fields.Count; nIndex++)
                        {
                            var field = fields[nIndex];
                            string fieldTypeCode = ResolveFieldTypeCode(field.FieldType);

                            // Skip link (10), read-only (flag & 4), and fields not in form
                            if (fieldTypeCode == "10" || field.IsReadOnly || !field.ShowInForm)
                                continue;

                            if (string.IsNullOrEmpty(field.FieldSource))
                                continue;

                            string formKey = $"inputField_{nIndex}";
                            formValues.TryGetValue(formKey, out var formValue);

                            string? dbValue = ResolveFieldValueForDb(fieldTypeCode, formValue, field.IsRequired, field.FieldFlags);

                            if (dbValue is null)
                            {
                                setClauses.Add($"\"{SanitizeSqlIdentifier(field.FieldSource)}\" = NULL");
                            }
                            else
                            {
                                setClauses.Add($"\"{SanitizeSqlIdentifier(field.FieldSource)}\" = {{{parameters.Count}}}");
                                parameters.Add(dbValue);
                            }
                        }

                        if (setClauses.Count > 0)
                        {
                            string updateSql = $"UPDATE \"{SanitizeSqlIdentifier(result.MainTable)}\" SET {string.Join(", ", setClauses)} WHERE \"{SanitizeSqlIdentifier(result.Primarykey)}\" = {{{parameters.Count}}}";
                            parameters.Add(nItemID.Value);

                            await dbWrite.Database.ExecuteSqlRawAsync(
                                updateSql,
                                parameters.ToArray(),
                                ct);
                        }
                    }
                    else if (mode == "add")
                    {
                        // Port of: rsItems.AddNew, then rsItems(field) = value, then rsItems.Update
                        var columnNames = new List<string>();
                        var valuePlaceholders = new List<string>();
                        var parameters = new List<object>();

                        for (int nIndex = 0; nIndex < fields.Count; nIndex++)
                        {
                            var field = fields[nIndex];
                            string fieldTypeCode = ResolveFieldTypeCode(field.FieldType);

                            // Skip link (10), read-only (flag & 4), and fields not in form
                            if (fieldTypeCode == "10" || field.IsReadOnly || !field.ShowInForm)
                                continue;

                            if (string.IsNullOrEmpty(field.FieldSource))
                                continue;

                            string formKey = $"inputField_{nIndex}";
                            formValues.TryGetValue(formKey, out var formValue);

                            // For add mode, use default value if form value is empty
                            if (string.IsNullOrEmpty(formValue) && !string.IsNullOrEmpty(field.DefaultValue))
                                formValue = field.DefaultValue;

                            string? dbValue = ResolveFieldValueForDb(fieldTypeCode, formValue, field.IsRequired, field.FieldFlags);

                            columnNames.Add($"\"{SanitizeSqlIdentifier(field.FieldSource)}\"");
                            if (dbValue is null)
                            {
                                valuePlaceholders.Add("NULL");
                            }
                            else
                            {
                                valuePlaceholders.Add($"{{{parameters.Count}}}");
                                parameters.Add(dbValue);
                            }
                        }

                        if (columnNames.Count > 0)
                        {
                            string insertSql = $"INSERT INTO \"{SanitizeSqlIdentifier(result.MainTable)}\" ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", valuePlaceholders)})";

                            await dbWrite.Database.ExecuteSqlRawAsync(
                                insertSql,
                                parameters.ToArray(),
                                ct);
                        }
                    }

                    await transaction.CommitAsync(ct);

                    result.RedirectUrl = $"dataview?MSG={mode}{result.ViewQueryString}";
                    result.SuccessMessage = mode;

                    _logger.LogInformation(
                        "DataView NGDT: {Mode} completed successfully for ViewID={ViewID}",
                        mode, nViewID);
                }
                catch (DbUpdateConcurrencyException cex)
                {
                    await transaction.RollbackAsync(ct);
                    _logger.LogWarning(cex,
                        "DataView NGDT: concurrency error during {Mode} for ViewID={ViewID}, ItemID={ItemID}. Retrying once.",
                        mode, nViewID, nItemID);

                    // Retry once (port of VerificaErroLock retry pattern)
                    try
                    {
                        await using var dbRetry = await _dbFactory.CreateDbContextAsync(ct);
                        await using var retryTx = await dbRetry.Database.BeginTransactionAsync(ct);

                        // Re-execute the same operation
                        if (mode == "edit" && nItemID.HasValue)
                        {
                            var setClauses = BuildSetClauses(fields, formValues, out var parameters);
                            if (setClauses.Count > 0)
                            {
                                string updateSql = $"UPDATE \"{SanitizeSqlIdentifier(result.MainTable)}\" SET {string.Join(", ", setClauses)} WHERE \"{SanitizeSqlIdentifier(result.Primarykey)}\" = {{{parameters.Count}}}";
                                parameters.Add(nItemID.Value);
                                await dbRetry.Database.ExecuteSqlRawAsync(updateSql, parameters.ToArray(), ct);
                            }
                        }

                        await retryTx.CommitAsync(ct);
                        result.RedirectUrl = $"dataview?MSG={mode}{result.ViewQueryString}";
                        result.SuccessMessage = mode;
                    }
                    catch (Exception retryEx)
                    {
                        _logger.LogError(retryEx, "DataView NGDT: retry also failed for ViewID={ViewID}", nViewID);
                        result.Error = $"Error(s) while performing \"{mode}\" (after retry):<br/>{System.Net.WebUtility.HtmlEncode(retryEx.Message)}";
                    }
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync(ct);
                    _logger.LogError(ex,
                        "DataView NGDT: error during {Mode} for ViewID={ViewID}, ItemID={ItemID}",
                        mode, nViewID, nItemID);
                    result.Error = $"Error(s) while performing \"{mode}\":<br/>{System.Net.WebUtility.HtmlEncode(ex.Message)}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DataView NGDT: transaction setup failed for ViewID={ViewID}", nViewID);
                result.Error = $"Error opening data source:<br/>{System.Net.WebUtility.HtmlEncode(ex.Message)}";
            }
        }
        // -- DELETE (port of the delete branch) --
        else if (string.IsNullOrEmpty(result.Error)
                 && mode == "delete"
                 && nItemID.HasValue
                 && result.AllowDelete)
        {
            _logger.LogInformation(
                "DataView NGDT: performing delete on ViewID={ViewID}, ItemID={ItemID}",
                nViewID, nItemID);

            try
            {
                await using var dbWrite = await _dbFactory.CreateDbContextAsync(ct);

                if (!string.IsNullOrEmpty(result.DeleteProcedure))
                {
                    _logger.LogWarning(
                        "DataView NGDT: DeleteProcedure '{Proc}' configured but stored procedures " +
                        "are not supported on SQLite. Falling back to direct DELETE.",
                        result.DeleteProcedure);
                }

                // Port of: DELETE FROM MainTable WHERE PrimaryKey = nItemID
                string deleteSql = $"DELETE FROM \"{SanitizeSqlIdentifier(result.MainTable)}\" WHERE \"{SanitizeSqlIdentifier(result.Primarykey)}\" = {{0}}";
                await dbWrite.Database.ExecuteSqlRawAsync(deleteSql, new object[] { nItemID.Value }, ct);

                result.RedirectUrl = $"dataview?MSG=delete{result.ViewQueryString}";
                result.SuccessMessage = "delete";

                _logger.LogInformation(
                    "DataView NGDT: delete completed successfully for ViewID={ViewID}, ItemID={ItemID}",
                    nViewID, nItemID);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "DataView NGDT: error during delete for ViewID={ViewID}, ItemID={ItemID}",
                    nViewID, nItemID);
                result.Error = $"Error(s) while performing \"delete\":<br/>{System.Net.WebUtility.HtmlEncode(ex.Message)}";
            }
        }
        // -- EDIT mode (no postback) — load existing row data for form population --
        // Port of: SELECT * FROM MainTable WHERE PrimaryKey = nItemID to populate form fields
        else if (string.IsNullOrEmpty(result.Error)
                 && mode == "edit"
                 && nItemID.HasValue
                 && result.AllowUpdate
                 && !isPostback)
        {
            try
            {
                await using var dbRead = await _dbFactory.CreateDbContextAsync(ct);

                // Build column list from fields that have FieldSource
                var columnList = new List<string>();
                var fieldIndexMap = new List<int>(); // maps column position back to field index

                for (int i = 0; i < fields.Count; i++)
                {
                    if (!string.IsNullOrEmpty(fields[i].FieldSource))
                    {
                        columnList.Add($"\"{SanitizeSqlIdentifier(fields[i].FieldSource)}\"");
                        fieldIndexMap.Add(i);
                    }
                }

                if (columnList.Count > 0 && !string.IsNullOrEmpty(result.MainTable) && !string.IsNullOrEmpty(result.Primarykey))
                {
                    string selectSql = $"SELECT {string.Join(", ", columnList)} FROM \"{SanitizeSqlIdentifier(result.MainTable)}\" WHERE \"{SanitizeSqlIdentifier(result.Primarykey)}\" = {{0}}";

                    var connection = dbRead.Database.GetDbConnection();
                    if (connection.State != ConnectionState.Open)
                        await connection.OpenAsync(ct);

                    await using var command = connection.CreateCommand();
                    command.CommandText = selectSql.Replace("{0}", "@p0");
                    var param = command.CreateParameter();
                    param.ParameterName = "@p0";
                    param.Value = nItemID.Value;
                    command.Parameters.Add(param);

                    await using var reader = await command.ExecuteReaderAsync(ct);
                    if (await reader.ReadAsync(ct))
                    {
                        for (int col = 0; col < reader.FieldCount && col < fieldIndexMap.Count; col++)
                        {
                            int fieldIdx = fieldIndexMap[col];
                            if (!reader.IsDBNull(col))
                            {
                                var val = reader.GetValue(col);
                                string fieldTypeCode = ResolveFieldTypeCode(fields[fieldIdx].FieldType);

                                // Format dates for display (port of date formatting in edit mode)
                                if (fieldTypeCode == "7" && val is DateTime dtVal)
                                {
                                    result.EditRowData[fieldIdx] = dtVal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                                }
                                else if (fieldTypeCode == "8" && val is DateTime dtTimeVal)
                                {
                                    result.EditRowData[fieldIdx] = dtTimeVal.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
                                }
                                else if (fieldTypeCode == "9") // boolean
                                {
                                    result.EditRowData[fieldIdx] = Convert.ToBoolean(val) ? "1" : "0";
                                }
                                else
                                {
                                    result.EditRowData[fieldIdx] = Convert.ToString(val, CultureInfo.InvariantCulture) ?? string.Empty;
                                }
                            }
                            else
                            {
                                result.EditRowData[fieldIdx] = null;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "DataView NGDT: error loading edit data for ViewID={ViewID}, ItemID={ItemID}",
                    nViewID, nItemID);
                result.Error = $"Error loading record for editing:<br/>{System.Net.WebUtility.HtmlEncode(ex.Message)}";
            }
        }
        // -- CLONE mode — load existing row data but set mode to add --
        else if (string.IsNullOrEmpty(result.Error)
                 && mode == "clone"
                 && nItemID.HasValue
                 && result.AllowClone
                 && !isPostback)
        {
            try
            {
                await using var dbRead = await _dbFactory.CreateDbContextAsync(ct);

                var columnList = new List<string>();
                var fieldIndexMap = new List<int>();

                for (int i = 0; i < fields.Count; i++)
                {
                    if (!string.IsNullOrEmpty(fields[i].FieldSource))
                    {
                        columnList.Add($"\"{SanitizeSqlIdentifier(fields[i].FieldSource)}\"");
                        fieldIndexMap.Add(i);
                    }
                }

                if (columnList.Count > 0 && !string.IsNullOrEmpty(result.MainTable) && !string.IsNullOrEmpty(result.Primarykey))
                {
                    string selectSql = $"SELECT {string.Join(", ", columnList)} FROM \"{SanitizeSqlIdentifier(result.MainTable)}\" WHERE \"{SanitizeSqlIdentifier(result.Primarykey)}\" = {{0}}";

                    var connection = dbRead.Database.GetDbConnection();
                    if (connection.State != ConnectionState.Open)
                        await connection.OpenAsync(ct);

                    await using var command = connection.CreateCommand();
                    command.CommandText = selectSql.Replace("{0}", "@p0");
                    var param = command.CreateParameter();
                    param.ParameterName = "@p0";
                    param.Value = nItemID.Value;
                    command.Parameters.Add(param);

                    await using var reader = await command.ExecuteReaderAsync(ct);
                    if (await reader.ReadAsync(ct))
                    {
                        for (int col = 0; col < reader.FieldCount && col < fieldIndexMap.Count; col++)
                        {
                            int fieldIdx = fieldIndexMap[col];
                            if (!reader.IsDBNull(col))
                            {
                                result.EditRowData[fieldIdx] = Convert.ToString(reader.GetValue(col), CultureInfo.InvariantCulture) ?? string.Empty;
                            }
                            else
                            {
                                result.EditRowData[fieldIdx] = null;
                            }
                        }
                    }
                }

                // Switch mode to add for clone (the form will submit as add)
                result.Mode = "add";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "DataView NGDT: error loading clone data for ViewID={ViewID}, ItemID={ItemID}",
                    nViewID, nItemID);
                result.Error = $"Error loading record for cloning:<br/>{System.Net.WebUtility.HtmlEncode(ex.Message)}";
            }
        }

        return result;
    }

    // ======================================================================
    // Private helpers
    // ======================================================================

    /// <summary>
    /// Loads linked table lookup data for all combo/multicombo fields.
    /// Port of: FOR each field, if LinkedTable is set, SELECT value/title from that table.
    /// </summary>
    private async Task LoadLookupDataAsync(
        ASPClassicVBScriptDbContext db,
        List<DataViewFieldInfoDto> fields,
        DataviewNgdtLoadResultDto result,
        CancellationToken ct)
    {
        for (int i = 0; i < fields.Count; i++)
        {
            var field = fields[i];
            string fieldTypeCode = ResolveFieldTypeCode(field.FieldType);

            // Only load lookups for combo (5) and multicombo (6) types
            if (fieldTypeCode != "5" && fieldTypeCode != "6")
                continue;

            if (string.IsNullOrEmpty(field.LinkedTable) || string.IsNullOrEmpty(field.LinkedTableValueField) || string.IsNullOrEmpty(field.LinkedTableTitleField))
                continue;

            try
            {
                var lookupItems = new List<LookupItemDto>();

                // Build SELECT for the linked table
                var selectColumns = new List<string>
                {
                    $"\"{SanitizeSqlIdentifier(field.LinkedTableValueField)}\"",
                    $"\"{SanitizeSqlIdentifier(field.LinkedTableTitleField)}\""
                };

                bool hasGroup = !string.IsNullOrEmpty(field.LinkedTableGroupField);
                bool hasGlyph = !string.IsNullOrEmpty(field.LinkedTableGlyphField);
                bool hasTooltip = !string.IsNullOrEmpty(field.LinkedTableTooltipField);

                if (hasGroup)
                    selectColumns.Add($"\"{SanitizeSqlIdentifier(field.LinkedTableGroupField)}\"");
                if (hasGlyph)
                    selectColumns.Add($"\"{SanitizeSqlIdentifier(field.LinkedTableGlyphField)}\"");
                if (hasTooltip)
                    selectColumns.Add($"\"{SanitizeSqlIdentifier(field.LinkedTableTooltipField)}\"");

                string lookupSql = $"SELECT {string.Join(", ", selectColumns)} FROM \"{SanitizeSqlIdentifier(field.LinkedTable)}\"";

                // Append additional WHERE/ORDER BY clause if defined
                if (!string.IsNullOrEmpty(field.LinkedTableAddition))
                {
                    lookupSql += $" {field.LinkedTableAddition}";
                }

                var connection = db.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync(ct);

                await using var command = connection.CreateCommand();
                command.CommandText = lookupSql;

                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var item = new LookupItemDto
                    {
                        Value = reader.IsDBNull(0) ? string.Empty : Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture) ?? string.Empty,
                        Title = reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                    };

                    int colIdx = 2;
                    if (hasGroup)
                    {
                        item.Group = reader.IsDBNull(colIdx) ? string.Empty : Convert.ToString(reader.GetValue(colIdx), CultureInfo.InvariantCulture) ?? string.Empty;
                        colIdx++;
                    }
                    if (hasGlyph)
                    {
                        item.Glyph = reader.IsDBNull(colIdx) ? string.Empty : Convert.ToString(reader.GetValue(colIdx), CultureInfo.InvariantCulture) ?? string.Empty;
                        colIdx++;
                    }
                    if (hasTooltip)
                    {
                        item.Tooltip = reader.IsDBNull(colIdx) ? string.Empty : Convert.ToString(reader.GetValue(colIdx), CultureInfo.InvariantCulture) ?? string.Empty;
                    }

                    lookupItems.Add(item);
                }

                result.LookupData[i] = lookupItems;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "DataView NGDT: failed to load lookup data for field '{FieldLabel}' (LinkedTable='{LinkedTable}')",
                    field.FieldLabel, field.LinkedTable);
                result.LookupData[i] = new List<LookupItemDto>();
            }
        }
    }

    /// <summary>
    /// Builds SET clauses for an UPDATE statement from form values and field definitions.
    /// Used for retry logic.
    /// </summary>
    private List<string> BuildSetClauses(
        List<DataViewFieldInfoDto> fields,
        Dictionary<string, string> formValues,
        out List<object> parameters)
    {
        var setClauses = new List<string>();
        parameters = new List<object>();

        for (int nIndex = 0; nIndex < fields.Count; nIndex++)
        {
            var field = fields[nIndex];
            string fieldTypeCode = ResolveFieldTypeCode(field.FieldType);

            if (fieldTypeCode == "10" || field.IsReadOnly || !field.ShowInForm)
                continue;

            if (string.IsNullOrEmpty(field.FieldSource))
                continue;

            string formKey = $"inputField_{nIndex}";
            formValues.TryGetValue(formKey, out var formValue);

            string? dbValue = ResolveFieldValueForDb(fieldTypeCode, formValue, field.IsRequired, field.FieldFlags);

            if (dbValue is null)
            {
                setClauses.Add($"\"{SanitizeSqlIdentifier(field.FieldSource)}\" = NULL");
            }
            else
            {
                setClauses.Add($"\"{SanitizeSqlIdentifier(field.FieldSource)}\" = {{{parameters.Count}}}");
                parameters.Add(dbValue);
            }
        }

        return setClauses;
    }

    /// <summary>
    /// Resolves a field type string (e.g., "text", "textarea", "combo") to the legacy
    /// numeric code used in the Select Case blocks. The legacy code stored integer field
    /// types directly; the DB may store either the numeric string or the text name.
    /// </summary>
    private static string ResolveFieldTypeCode(string fieldType)
    {
        if (string.IsNullOrEmpty(fieldType))
            return "0";

        // If already numeric, return as-is
        if (int.TryParse(fieldType, CultureInfo.InvariantCulture, out _))
            return fieldType;

        // Map text field type names to their legacy numeric equivalents
        return fieldType.ToLowerInvariant() switch
        {
            "text" => "1",
            "textarea" => "2",
            "int" => "3",
            "double" => "4",
            "combo" => "5",
            "multicombo" => "6",
            "date" => "7",
            "datetime" => "8",
            "boolean" => "9",
            "link" => "10",
            "hidden" => "11",
            "password" => "12",
            "time" => "13",
            "rte" => "14",
            "email" => "15",
            _ => "0"
        };
    }

    /// <summary>
    /// Port of the Select Case block that determines the value to write to the database
    /// for each field type. Handles NULL for empty non-required fields, time truncation, etc.
    /// </summary>
    private static string? ResolveFieldValueForDb(
        string fieldTypeCode,
        string? formValue,
        bool isRequired,
        int fieldFlags)
    {
        bool isNonRequiredEmpty = string.IsNullOrEmpty(formValue) && !isRequired;

        switch (fieldTypeCode)
        {
            case "12": // password
            case "1":  // text
            case "2":  // textarea
            case "15": // email
                return formValue ?? string.Empty;

            case "6":  // multicombo — join multiple selected values
            case "14": // rte
                return formValue ?? string.Empty;

            case "13": // time
                if (isNonRequiredEmpty)
                    return null;
                // Truncate to first 8 chars (HH:mm:ss)
                if (!string.IsNullOrEmpty(formValue) && formValue.Length > 8)
                    return formValue[..8];
                return formValue;

            case "9": // boolean
                if (string.IsNullOrEmpty(formValue))
                    return isRequired ? "0" : null;
                return formValue == "1" || formValue.Equals("true", StringComparison.OrdinalIgnoreCase) ? "1" : "0";

            case "3": // int
                if (isNonRequiredEmpty)
                    return null;
                // Validate integer
                if (!string.IsNullOrEmpty(formValue) && int.TryParse(formValue, CultureInfo.InvariantCulture, out _))
                    return formValue;
                return isNonRequiredEmpty ? null : formValue;

            case "4": // double
                if (isNonRequiredEmpty)
                    return null;
                if (!string.IsNullOrEmpty(formValue) && double.TryParse(formValue, CultureInfo.InvariantCulture, out _))
                    return formValue;
                return isNonRequiredEmpty ? null : formValue;

            case "7": // date
                if (isNonRequiredEmpty)
                    return null;
                return formValue;

            case "8": // datetime
                if (isNonRequiredEmpty)
                    return null;
                return formValue;

            case "5": // combo
                if (isNonRequiredEmpty)
                    return null;
                return formValue;

            case "11": // hidden
                return formValue ?? string.Empty;

            default:
                if (isNonRequiredEmpty)
                    return null;
                return formValue;
        }
    }

    /// <summary>
    /// Sanitizes a SQL identifier by removing characters that are not alphanumeric,
    /// underscores, or dots. Prevents SQL injection in dynamic table/column names.
    /// </summary>
    private static string SanitizeSqlIdentifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return string.Empty;

        var sanitized = new char[identifier.Length];
        int pos = 0;
        foreach (char c in identifier)
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == ' ')
            {
                sanitized[pos++] = c;
            }
        }
        return new string(sanitized, 0, pos);
    }

    /// <summary>
    /// Maps a DataViewField entity to a DataViewFieldDto.
    /// Handles nullable → non-nullable coalescing per EF Core mapping rules.
    /// </summary>
    private static DataViewFieldDto MapFieldToDto(DataViewField entity)
    {
        return new DataViewFieldDto
        {
            ViewID = entity.ViewID,
            FieldID = entity.FieldID,
            FieldLabel = entity.FieldLabel,
            FieldSource = entity.FieldSource ?? string.Empty,
            FieldType = entity.FieldType,
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
            FieldIdentifier = entity.FieldIdentifier ?? string.Empty,
        };
    }

    /// <summary>
    /// Maps a DataView entity to a DataViewDto.
    /// Handles nullable → non-nullable coalescing per EF Core mapping rules.
    /// </summary>
    private static DataViewDto MapViewToDto(ASPClassic.Domain.Entities.Data.DataView entity)
    {
        return new DataViewDto
        {
            ViewID = entity.ViewID,
            Title = entity.Title,
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
            DataTablePagingStyle = entity.DataTablePagingStyle,
            Published = entity.Published,
            RowReorderColumn = entity.RowReorderColumn ?? string.Empty,
            IsSystemObject = entity.IsSystemObject,
            CSSTable = entity.CSSTable,
        };
    }
}
