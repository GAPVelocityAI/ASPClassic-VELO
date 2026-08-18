using System.Globalization;
using System.Text;
using ASPClassic.Application.DTOs.Admin;
using ASPClassic.Application.DTOs.Data;
using ASPClassic.Domain.Entities.Data;
using ASPClassic.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ASPClassic.Application.Services.Admin;

/// <summary>Port of <c>admin_dataviews.asp</c> — manage Data Views CRUD.</summary>
public class AdminDataviewsService : IAdminDataviewsService
{
    private readonly IDbContextFactory<ASPClassicVBScriptDbContext> _dbFactory;
    private readonly ILogger<AdminDataviewsService> _logger;

    public AdminDataviewsService(
        IDbContextFactory<ASPClassicVBScriptDbContext> dbFactory,
        ILogger<AdminDataviewsService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DataViewDto?> GetDataViewAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.DataViews
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (entity is null)
            return null;

        return MapToDto(entity);
    }

    /// <summary>Retrieves a single DataView by ID (internal helper used by pages).</summary>
    public async Task<DataViewDto?> GetDataViewByIdAsync(int viewId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.DataViews
            .AsNoTracking()
            .FirstOrDefaultAsync(dv => dv.ViewID == viewId, ct);

        if (entity is null)
            return null;

        return MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task<List<DataViewDto>> GetAllDataViewsAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entities = await db.DataViews
            .AsNoTracking()
            .OrderBy(dv => dv.Title)
            .ToListAsync(ct);

        return entities.Select(MapToDto).ToList();
    }

    /// <inheritdoc />
    public async Task<DataViewDto?> LoadAdminDataviewsAsync(
        string mode,
        string itemID,
        string title,
        string dataSource,
        string published,
        string mainTable,
        string primaryKey,
        string modificationProcedure,
        CancellationToken ct = default)
    {
        // Delegate to the extended overload with default values for extra parameters
        var result = await LoadAdminDataviewsExtendedAsync(
            mode, itemID, title, dataSource, published, mainTable, primaryKey,
            modificationProcedure,
            string.Empty, // viewProcedure
            string.Empty, // deleteProcedure
            string.Empty, // viewDescription
            string.Empty, // orderBy
            string.Empty, // rowReorderColumn
            "0",          // dataTableModifierButtonStyle
            "10",         // dataTableDefaultPageSize
            string.Empty, // dataTablePagingStyle
            new List<int>(),  // flagValues
            new List<int>(),  // dataTableFlagValues
            ct);

        if (result.Success && result.DataView is not null)
        {
            return new DataViewDto
            {
                ViewID = result.DataView.ViewID,
                Title = result.DataView.Title,
                DataSource = result.DataView.DataSource,
                MainTable = result.DataView.MainTable,
                Primarykey = result.DataView.Primarykey,
                ModificationProcedure = result.DataView.ModificationProcedure,
                ViewProcedure = result.DataView.ViewProcedure,
                DeleteProcedure = result.DataView.DeleteProcedure,
                ViewDescription = result.DataView.ViewDescription,
                OrderBy = result.DataView.OrderBy,
                Flags = result.DataView.Flags,
                DataTableModifierButtonStyle = result.DataView.DataTableModifierButtonStyle,
                DataTableFlags = result.DataView.DataTableFlags,
                DataTableDefaultPageSize = result.DataView.DataTableDefaultPageSize,
                DataTablePagingStyle = result.DataView.DataTablePagingStyle,
                Published = result.DataView.Published,
                RowReorderColumn = result.DataView.RowReorderColumn,
                IsSystemObject = false,
                CSSTable = string.Empty,
            };
        }

        if (result.NewViewID.HasValue)
        {
            return await GetDataViewByIdAsync(result.NewViewID.Value, ct);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<AdminDataviewsSaveResultDto> LoadAdminDataviewsExtendedAsync(
        string mode,
        string itemID,
        string title,
        string dataSource,
        string published,
        string mainTable,
        string primaryKey,
        string modificationProcedure,
        string viewProcedure,
        string deleteProcedure,
        string viewDescription,
        string orderBy,
        string rowReorderColumn,
        string dataTableModifierButtonStyle,
        string dataTableDefaultPageSize,
        string dataTablePagingStyle,
        List<int> flagValues,
        List<int> dataTableFlagValues,
        CancellationToken ct = default)
    {
        var result = new AdminDataviewsSaveResultDto { Success = true };

        // Parse itemID — legacy: IF NOT IsNumeric(nItemID) THEN nItemID = ""
        int? nItemID = null;
        if (int.TryParse(itemID, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId))
        {
            nItemID = parsedId;
        }

        // Default DataSource to "Default" if empty — legacy: IF strDataSource = "" THEN strDataSource = "Default"
        if (string.IsNullOrEmpty(dataSource))
            dataSource = "Default";

        // Parse Published — legacy: IF blnPublished = "" THEN blnPublished = False ELSE blnPublished = CBool(blnPublished)
        bool blnPublished = !string.IsNullOrEmpty(published) &&
                            (string.Equals(published, "true", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(published, "True", StringComparison.OrdinalIgnoreCase) ||
                             published == "1" ||
                             string.Equals(published, "on", StringComparison.OrdinalIgnoreCase));

        // Parse numeric fields
        short nDtModBtnStyle = 0;
        if (short.TryParse(dataTableModifierButtonStyle, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedBtnStyle))
            nDtModBtnStyle = parsedBtnStyle;

        int nDtDefaultPageSize = 10;
        if (int.TryParse(dataTableDefaultPageSize, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPageSize))
            nDtDefaultPageSize = parsedPageSize;

        // Accumulate flags — legacy: For nIndex = 1 TO Request.Form("Flags").Count; nFlags = nFlags + CLng(...)
        int nFlags = 0;
        foreach (var fv in flagValues)
        {
            nFlags += fv;
        }

        int nDtFlags = 0;
        foreach (var dtfv in dataTableFlagValues)
        {
            nDtFlags += dtfv;
        }

        // Handle RowReorderColumn — legacy: IF strRowReorderCol = "" THEN strRowReorderCol = Null
        string? effectiveRowReorderCol = string.IsNullOrEmpty(rowReorderColumn) ? null : rowReorderColumn;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // ── DELETE MODE ──
        // Legacy: ELSEIF strMode = "delete" AND nItemID <> "" AND IsNumeric(nItemID) THEN
        //   adoConnCrude.Execute "DELETE FROM portal.DataViewField WHERE ViewID = ...; DELETE FROM portal.DataView WHERE ViewID = ..."
        if (string.Equals(mode, "delete", StringComparison.OrdinalIgnoreCase) && nItemID.HasValue)
        {
            return await DeleteDataViewAsync(nItemID.Value, ct);
        }

        // ── ADD / EDIT MODE with form submission ──
        // Legacy: IF Request.Form("Title") <> "" THEN ...
        if (!string.IsNullOrEmpty(title))
        {
            // Validate primaryKey — legacy auto-discovery of PK from sys.indexes
            if (string.IsNullOrEmpty(primaryKey) && !string.IsNullOrEmpty(mainTable))
            {
                var discoveredPk = await TryDiscoverPrimaryKeyAsync(db, mainTable, ct);
                if (discoveredPk is not null)
                {
                    primaryKey = discoveredPk;
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = "Primary Key must be specified for this table!";
                    result.DataView = new DataViewEditDto
                    {
                        ViewID = nItemID ?? 0,
                        Title = title,
                        DataSource = dataSource,
                        MainTable = mainTable,
                        Primarykey = primaryKey ?? string.Empty,
                        ModificationProcedure = modificationProcedure ?? string.Empty,
                        ViewProcedure = viewProcedure ?? string.Empty,
                        DeleteProcedure = deleteProcedure ?? string.Empty,
                        ViewDescription = viewDescription ?? string.Empty,
                        OrderBy = orderBy ?? string.Empty,
                        RowReorderColumn = rowReorderColumn ?? string.Empty,
                        Published = blnPublished,
                        Flags = nFlags,
                        DataTableModifierButtonStyle = nDtModBtnStyle,
                        DataTableFlags = nDtFlags,
                        DataTableDefaultPageSize = nDtDefaultPageSize,
                        DataTablePagingStyle = dataTablePagingStyle ?? string.Empty,
                    };
                    return result;
                }
            }

            if (string.Equals(mode, "add", StringComparison.OrdinalIgnoreCase))
            {
                // Legacy: rsItems.AddNew ... rsItems.Update ... nItemID = rsItems("ViewID")
                var newEntity = new DataView
                {
                    Title = title,
                    Published = blnPublished,
                    DataSource = dataSource,
                    MainTable = mainTable,
                    Primarykey = primaryKey,
                    ModificationProcedure = modificationProcedure,
                    ViewProcedure = viewProcedure,
                    DeleteProcedure = deleteProcedure,
                    ViewDescription = viewDescription,
                    OrderBy = orderBy,
                    RowReorderColumn = effectiveRowReorderCol,
                    Flags = nFlags,
                    DataTableModifierButtonStyle = nDtModBtnStyle,
                    DataTableDefaultPageSize = nDtDefaultPageSize,
                    DataTableFlags = nDtFlags,
                    DataTablePagingStyle = dataTablePagingStyle ?? string.Empty,
                    IsSystemObject = false,
                    CSSTable = string.Empty,
                };

                try
                {
                    db.DataViews.Add(newEntity);
                    await db.SaveChangesAsync(ct);

                    _logger.LogInformation("DataView added with ViewID={ViewID}, Title={Title}", newEntity.ViewID, title);

                    // Legacy: Response.Redirect("admin_dataviewfields.asp?mode=autoinit&ViewID=" & nItemID)
                    result.NewViewID = newEntity.ViewID;
                    result.RedirectUrl = $"/aspclassic-vbscript/admin-dataviewfields?mode=autoinit&ViewID={newEntity.ViewID}";
                    return result;
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Error adding DataView Title={Title}", title);
                    result.Success = false;
                    result.ErrorMessage = $"Error(s) while performing \"add\": {ex.InnerException?.Message ?? ex.Message}";
                    result.DataView = BuildEditDto(0, title, dataSource, mainTable, primaryKey,
                        modificationProcedure, viewProcedure, deleteProcedure, viewDescription,
                        orderBy, effectiveRowReorderCol, blnPublished, nFlags, nDtModBtnStyle,
                        nDtFlags, nDtDefaultPageSize, dataTablePagingStyle);
                    return result;
                }
            }
            else if (string.Equals(mode, "edit", StringComparison.OrdinalIgnoreCase) && nItemID.HasValue)
            {
                // Legacy: strSQL = "SELECT * FROM portal.DataView WHERE ViewID = " & nItemID
                //   then rsItems("Title") = strTitle ... rsItems.Update
                var entity = await db.DataViews.FirstOrDefaultAsync(dv => dv.ViewID == nItemID.Value, ct);

                if (entity is null)
                {
                    result.Success = false;
                    result.ErrorMessage = "Item Not Found";
                    return result;
                }

                entity.Title = title;
                entity.Published = blnPublished;
                entity.DataSource = dataSource;
                entity.MainTable = mainTable;
                entity.Primarykey = primaryKey;
                entity.ModificationProcedure = modificationProcedure;
                entity.ViewProcedure = viewProcedure;
                entity.DeleteProcedure = deleteProcedure;
                entity.ViewDescription = viewDescription;
                entity.OrderBy = orderBy;
                entity.RowReorderColumn = effectiveRowReorderCol;
                entity.Flags = nFlags;
                entity.DataTableModifierButtonStyle = nDtModBtnStyle;
                entity.DataTableDefaultPageSize = nDtDefaultPageSize;
                entity.DataTableFlags = nDtFlags;
                entity.DataTablePagingStyle = dataTablePagingStyle ?? string.Empty;

                try
                {
                    await db.SaveChangesAsync(ct);

                    _logger.LogInformation("DataView updated ViewID={ViewID}, Title={Title}", nItemID.Value, title);

                    // Legacy: Response.Redirect(constPageScriptName & "?MSG=" & strMode)
                    result.RedirectUrl = "/aspclassic-vbscript/admin-dataviews?MSG=edit";
                    return result;
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Error editing DataView ViewID={ViewID}", nItemID.Value);
                    result.Success = false;
                    result.ErrorMessage = $"Error(s) while performing \"edit\": {ex.InnerException?.Message ?? ex.Message}";
                    result.DataView = BuildEditDto(nItemID.Value, title, dataSource, mainTable, primaryKey,
                        modificationProcedure, viewProcedure, deleteProcedure, viewDescription,
                        orderBy, effectiveRowReorderCol, blnPublished, nFlags, nDtModBtnStyle,
                        nDtFlags, nDtDefaultPageSize, dataTablePagingStyle);
                    return result;
                }
            }
            else
            {
                // Legacy: strError = "Invalid input!"
                result.Success = false;
                result.ErrorMessage = "Invalid input!";
                return result;
            }
        }

        // ── LOAD FOR EDIT (no form submission — initial page load) ──
        // Legacy: IF (strMode = "edit" AND nItemID <> "" AND IsNumeric(nItemID)) OR strMode = "add" Then
        if (string.Equals(mode, "edit", StringComparison.OrdinalIgnoreCase) && nItemID.HasValue)
        {
            var entity = await db.DataViews
                .AsNoTracking()
                .FirstOrDefaultAsync(dv => dv.ViewID == nItemID.Value, ct);

            if (entity is not null)
            {
                result.DataView = new DataViewEditDto
                {
                    ViewID = entity.ViewID,
                    Title = entity.Title,
                    DataSource = entity.DataSource ?? "Default",
                    MainTable = entity.MainTable ?? string.Empty,
                    Primarykey = entity.Primarykey ?? string.Empty,
                    ModificationProcedure = entity.ModificationProcedure ?? string.Empty,
                    ViewProcedure = entity.ViewProcedure ?? string.Empty,
                    DeleteProcedure = entity.DeleteProcedure ?? string.Empty,
                    ViewDescription = entity.ViewDescription ?? string.Empty,
                    OrderBy = entity.OrderBy ?? string.Empty,
                    RowReorderColumn = entity.RowReorderColumn ?? string.Empty,
                    Published = entity.Published,
                    Flags = entity.Flags,
                    DataTableModifierButtonStyle = entity.DataTableModifierButtonStyle,
                    DataTableFlags = entity.DataTableFlags,
                    DataTableDefaultPageSize = entity.DataTableDefaultPageSize,
                    DataTablePagingStyle = entity.DataTablePagingStyle,
                };
            }
            else
            {
                result.Success = false;
                result.ErrorMessage = "Item Not Found";
            }

            return result;
        }
        else if (string.Equals(mode, "add", StringComparison.OrdinalIgnoreCase))
        {
            // Legacy: compute default flags from luDataViewFlags and luDataTableFlags
            int defaultFlags = await ComputeDefaultFlagsAsync(db, ct);
            int defaultDtFlags = await ComputeDefaultDataTableFlagsAsync(db, ct);

            result.DataView = new DataViewEditDto
            {
                ViewID = 0,
                Title = string.Empty,
                DataSource = "Default",
                MainTable = string.Empty,
                Primarykey = string.Empty,
                ModificationProcedure = string.Empty,
                ViewProcedure = string.Empty,
                DeleteProcedure = string.Empty,
                ViewDescription = string.Empty,
                OrderBy = string.Empty,
                RowReorderColumn = string.Empty,
                Published = false,
                Flags = defaultFlags,
                DataTableModifierButtonStyle = 0,
                DataTableFlags = defaultDtFlags,
                DataTableDefaultPageSize = 10,
                DataTablePagingStyle = string.Empty,
            };

            return result;
        }

        // No valid mode — return empty result (the page will show the listing grid)
        return result;
    }

    /// <inheritdoc />
    public async Task GenerateMergeForTableAsync(
        string currTable,
        string currSchema,
        bool deleteUnmatchedRows,
        bool updateExistingRows,
        bool insertNewRows,
        bool debugMode,
        bool includeTimestamp,
        bool ommitComputedCols,
        string topClause)
    {
        var result = await GenerateMergeForTableExtendedAsync(
            currTable, currSchema, deleteUnmatchedRows, updateExistingRows,
            insertNewRows, debugMode, includeTimestamp, ommitComputedCols, topClause);

        if (!result.Success)
        {
            _logger.LogWarning("GenerateMergeForTable failed: {Error}", result.ErrorMessage);
        }
    }

    /// <inheritdoc />
    public async Task<GenerateMergeResultDto> GenerateMergeForTableExtendedAsync(
        string currTable,
        string currSchema,
        bool deleteUnmatchedRows,
        bool updateExistingRows,
        bool insertNewRows,
        bool debugMode,
        bool includeTimestamp,
        bool ommitComputedCols,
        string topClause,
        CancellationToken ct = default)
    {
        // Legacy: calls [dbo].[usp_Generate_Merge_For_Table] stored procedure.
        // In SQLite mode we generate an equivalent INSERT OR REPLACE statement by reading
        // column metadata from the target table and building the SQL text in-memory.

        var result = new GenerateMergeResultDto { Success = true };

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        try
        {
            // Discover columns from the known entities mapped in the DbContext
            var entityType = db.Model.GetEntityTypes()
                .FirstOrDefault(et =>
                    string.Equals(et.GetTableName(), currTable, StringComparison.OrdinalIgnoreCase));

            if (entityType is null)
            {
                result.Success = false;
                result.ErrorMessage = $"Table '{currTable}' not found in the database model.";
                return result;
            }

            var tableName = entityType.GetTableName() ?? currTable;
            var schemaName = !string.IsNullOrEmpty(currSchema) ? currSchema : (entityType.GetSchema() ?? "dbo");
            var properties = entityType.GetProperties().ToList();

            // Find primary key columns
            var pk = entityType.FindPrimaryKey();
            var pkColumnNames = pk?.Properties.Select(p => p.GetColumnName() ?? p.Name).ToList()
                                ?? new List<string>();

            // Filter out computed columns if requested
            var columnsForMerge = properties
                .Where(p =>
                {
                    if (ommitComputedCols && p.GetComputedColumnSql() is not null)
                        return false;
                    if (!includeTimestamp && p.IsConcurrencyToken)
                        return false;
                    return true;
                })
                .ToList();

            var allColumnNames = columnsForMerge
                .Select(p => p.GetColumnName() ?? p.Name)
                .ToList();

            var nonPkColumnNames = allColumnNames
                .Where(c => !pkColumnNames.Contains(c, StringComparer.OrdinalIgnoreCase))
                .ToList();

            var sb = new StringBuilder();

            if (debugMode)
            {
                sb.AppendLine($"-- MERGE script for [{schemaName}].[{tableName}]");
                sb.AppendLine($"-- Generated at {DateTime.UtcNow:O}");
                sb.AppendLine($"-- PK columns: {string.Join(", ", pkColumnNames)}");
                sb.AppendLine($"-- Options: deleteUnmatched={deleteUnmatchedRows}, updateExisting={updateExistingRows}, insertNew={insertNewRows}");
                sb.AppendLine();
            }

            // Build the MERGE statement structure
            sb.AppendLine("SET NOCOUNT ON;");
            sb.AppendLine();

            // Read all rows to build the VALUES source
            var rows = new List<Dictionary<string, object?>>();

            var entityClrType = entityType.ClrType;

            // Use reflection to get the DbSet
            var dbSetProperty = db.GetType().GetProperties()
                .FirstOrDefault(pi =>
                    pi.PropertyType.IsGenericType &&
                    pi.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>) &&
                    pi.PropertyType.GetGenericArguments()[0] == entityClrType);

            if (dbSetProperty is null)
            {
                result.Success = false;
                result.ErrorMessage = $"No DbSet found for entity type '{entityClrType.Name}'.";
                return result;
            }

            var dbSetObj = dbSetProperty.GetValue(db);
            if (dbSetObj is null)
            {
                result.Success = false;
                result.ErrorMessage = $"DbSet for '{entityClrType.Name}' returned null.";
                return result;
            }

            // Use IQueryable to enumerate all rows
            var queryable = (IQueryable<object>)dbSetObj;
            var allEntities = await queryable.AsNoTracking().ToListAsync(ct);

            foreach (var entityObj in allEntities)
            {
                var row = new Dictionary<string, object?>();
                foreach (var prop in columnsForMerge)
                {
                    var clrProp = entityClrType.GetProperty(prop.Name);
                    var value = clrProp?.GetValue(entityObj);
                    var colName = prop.GetColumnName() ?? prop.Name;
                    row[colName] = value;
                }
                rows.Add(row);
            }

            if (rows.Count == 0)
            {
                sb.AppendLine($"-- No data found in [{schemaName}].[{tableName}]");
                result.GeneratedSql = sb.ToString();
                return result;
            }

            // Generate MERGE statement
            sb.AppendLine($"MERGE [{schemaName}].[{tableName}] AS Target");
            sb.AppendLine("USING (VALUES");

            for (int rowIdx = 0; rowIdx < rows.Count; rowIdx++)
            {
                var row = rows[rowIdx];
                var values = allColumnNames.Select(col => FormatSqlValue(row.GetValueOrDefault(col)));
                sb.Append($"  ({string.Join(", ", values)})");
                sb.AppendLine(rowIdx < rows.Count - 1 ? "," : string.Empty);
            }

            sb.AppendLine($") AS Source ({string.Join(", ", allColumnNames.Select(c => $"[{c}]"))})");

            // ON clause — match on PK
            var onClauses = pkColumnNames.Select(pkCol => $"Target.[{pkCol}] = Source.[{pkCol}]");
            sb.AppendLine($"ON ({string.Join(" AND ", onClauses)})");

            // WHEN MATCHED — UPDATE
            if (updateExistingRows && nonPkColumnNames.Count > 0)
            {
                sb.AppendLine("WHEN MATCHED THEN UPDATE SET");
                var setClauses = nonPkColumnNames.Select(c => $"  Target.[{c}] = Source.[{c}]");
                sb.AppendLine(string.Join("," + Environment.NewLine, setClauses));
            }

            // WHEN NOT MATCHED BY TARGET — INSERT
            if (insertNewRows)
            {
                sb.AppendLine("WHEN NOT MATCHED BY TARGET THEN INSERT");
                sb.AppendLine($"  ({string.Join(", ", allColumnNames.Select(c => $"[{c}]"))})");
                sb.AppendLine($"  VALUES ({string.Join(", ", allColumnNames.Select(c => $"Source.[{c}]"))})");
            }

            // WHEN NOT MATCHED BY SOURCE — DELETE
            if (deleteUnmatchedRows)
            {
                sb.AppendLine("WHEN NOT MATCHED BY SOURCE THEN DELETE");
            }

            sb.AppendLine(";");
            sb.AppendLine();
            sb.AppendLine("SET NOCOUNT OFF;");

            result.GeneratedSql = sb.ToString();

            _logger.LogInformation(
                "Generated MERGE script for [{Schema}].[{Table}] with {RowCount} rows",
                schemaName, tableName, rows.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating MERGE for [{Schema}].[{Table}]", currSchema, currTable);
            result.Success = false;
            result.ErrorMessage = $"Error generating MERGE: {ex.Message}";
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<AdminDataviewsSaveResultDto> DeleteDataViewAsync(int viewId, CancellationToken ct = default)
    {
        var result = new AdminDataviewsSaveResultDto { Success = true };

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Legacy: DELETE FROM portal.DataViewField WHERE ViewID = ...; DELETE FROM portal.DataView WHERE ViewID = ...
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Delete child DataViewFields first
            var childFields = await db.DataViewFields
                .Where(f => f.ViewID == viewId)
                .ToListAsync(ct);

            if (childFields.Count > 0)
            {
                db.DataViewFields.RemoveRange(childFields);
            }

            // Delete child DataViewActions
            var childActions = await db.DataViewActions
                .Where(a => a.ViewID == viewId)
                .ToListAsync(ct);

            if (childActions.Count > 0)
            {
                // Delete action parameters for each action
                var actionIds = childActions.Select(a => a.ActionID).ToList();
                var childParams = await db.DataViewActionParameters
                    .Where(p => actionIds.Contains(p.ActionID))
                    .ToListAsync(ct);

                if (childParams.Count > 0)
                {
                    db.DataViewActionParameters.RemoveRange(childParams);
                }

                db.DataViewActions.RemoveRange(childActions);
            }

            // Delete child DataViewCharts
            var childCharts = await db.DataViewCharts
                .Where(c => c.ViewID == viewId)
                .ToListAsync(ct);

            if (childCharts.Count > 0)
            {
                db.DataViewCharts.RemoveRange(childCharts);
            }

            // Delete the DataView itself
            var entity = await db.DataViews.FirstOrDefaultAsync(dv => dv.ViewID == viewId, ct);
            if (entity is not null)
            {
                db.DataViews.Remove(entity);
            }

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation("DataView deleted ViewID={ViewID}", viewId);

            // Legacy: Response.Redirect(constPageScriptName & "?MSG=delete")
            result.RedirectUrl = "/aspclassic-vbscript/admin-dataviews?MSG=delete";
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Error deleting DataView ViewID={ViewID}", viewId);
            result.Success = false;
            result.ErrorMessage = $"Error deleting DataView: {ex.InnerException?.Message ?? ex.Message}";
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteDataViewFieldAsync(int fieldId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        try
        {
            var field = await db.DataViewFields.FirstOrDefaultAsync(f => f.FieldID == fieldId, ct);
            if (field is null)
                return false;

            db.DataViewFields.Remove(field);
            await db.SaveChangesAsync(ct);

            _logger.LogInformation("DataViewField deleted FieldID={FieldID}", fieldId);
            return true;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Error deleting DataViewField FieldID={FieldID}", fieldId);
            return false;
        }
    }

    // ── Private helpers ──

    /// <summary>
    /// Computes default Flags value from DataViewFlags lookup table.
    /// Legacy: For Each objFlag In luDataViewFlags.Items: IF CBool(objFlag.DefaultValue) THEN nFlags = nFlags + CLng(objFlag.Value)
    /// </summary>
    private async Task<int> ComputeDefaultFlagsAsync(ASPClassicVBScriptDbContext db, CancellationToken ct)
    {
        var flagItems = await db.DataViewFlags
            .AsNoTracking()
            .ToListAsync(ct);

        int flags = 0;
        foreach (var flag in flagItems)
        {
            if (IsTruthyDefault(flag.FlagDefault))
            {
                if (int.TryParse(flag.FlagValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var flagVal))
                {
                    flags += flagVal;
                }
            }
        }

        return flags;
    }

    /// <summary>
    /// Computes default DataTableFlags value from DataViewDataTableFlags lookup table.
    /// Legacy: For Each objFlag In luDataTableFlags.Items: IF CBool(objFlag.DefaultValue) THEN nDtFlags = nDtFlags + CLng(objFlag.Value)
    /// </summary>
    private async Task<int> ComputeDefaultDataTableFlagsAsync(ASPClassicVBScriptDbContext db, CancellationToken ct)
    {
        var flagItems = await db.DataViewDataTableFlags
            .AsNoTracking()
            .ToListAsync(ct);

        int flags = 0;
        foreach (var flag in flagItems)
        {
            if (IsTruthyDefault(flag.FlagDefault))
            {
                if (int.TryParse(flag.FlagValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var flagVal))
                {
                    flags += flagVal;
                }
            }
        }

        return flags;
    }

    /// <summary>
    /// Evaluates legacy CBool() on a FlagDefault string value.
    /// Legacy VBScript CBool: "True"/"true"/"-1"/"1" → true; "False"/"false"/"0"/empty → false
    /// </summary>
    private static bool IsTruthyDefault(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        if (string.Equals(value, "True", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "-1", StringComparison.Ordinal) ||
            string.Equals(value, "1", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to discover the primary key column for a given table name
    /// by checking known entities in the EF model.
    /// Legacy: queried sys.indexes on the source database.
    /// </summary>
    private Task<string?> TryDiscoverPrimaryKeyAsync(
        ASPClassicVBScriptDbContext db, string tableName, CancellationToken ct)
    {
        var entityType = db.Model.GetEntityTypes()
            .FirstOrDefault(et =>
                string.Equals(et.GetTableName(), tableName, StringComparison.OrdinalIgnoreCase));

        if (entityType is null)
            return Task.FromResult<string?>(null);

        var pkEntity = entityType.FindPrimaryKey();
        if (pkEntity is null || pkEntity.Properties.Count == 0)
            return Task.FromResult<string?>(null);

        // Legacy: composite keys (PKComposite > 1) are unsupported
        if (pkEntity.Properties.Count > 1)
            return Task.FromResult<string?>(null);

        var pkColumnName = pkEntity.Properties[0].GetColumnName() ?? pkEntity.Properties[0].Name;
        return Task.FromResult<string?>(pkColumnName);
    }

    /// <summary>
    /// Formats a CLR value as a SQL literal for MERGE script generation.
    /// Uses decimal for all floating-point types to avoid precision loss.
    /// </summary>
    private static string FormatSqlValue(object? value)
    {
        if (value is null || value == DBNull.Value)
            return "NULL";

        return value switch
        {
            string s => $"N'{s.Replace("'", "''")}'",
            bool b => b ? "1" : "0",
            DateTime dt => $"'{dt.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}'",
            DateTimeOffset dto => $"'{dto.ToString("yyyy-MM-dd HH:mm:ss.fffffff zzz", CultureInfo.InvariantCulture)}'",
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            int i => i.ToString(CultureInfo.InvariantCulture),
            long l => l.ToString(CultureInfo.InvariantCulture),
            short s => s.ToString(CultureInfo.InvariantCulture),
            byte by => by.ToString(CultureInfo.InvariantCulture),
            float f => ((decimal)f).ToString(CultureInfo.InvariantCulture),
            double d => ((decimal)d).ToString(CultureInfo.InvariantCulture),
            Guid g => $"'{g}'",
            byte[] bytes => $"0x{Convert.ToHexString(bytes)}",
            _ => $"N'{(value.ToString() ?? string.Empty).Replace("'", "''")}'",
        };
    }

    /// <summary>Maps a DataView entity to a DataViewDto with null-coalescing for nullable fields.</summary>
    private static DataViewDto MapToDto(ASPClassic.Domain.Entities.Data.DataView entity)
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

    /// <summary>Builds a DataViewEditDto from raw parameter values for re-display on error.</summary>
    private static DataViewEditDto BuildEditDto(
        int viewId, string title, string dataSource, string mainTable, string? primaryKey,
        string? modificationProcedure, string? viewProcedure, string? deleteProcedure,
        string? viewDescription, string? orderBy, string? rowReorderColumn,
        bool published, int flags, short dtModBtnStyle, int dtFlags, int dtDefaultPageSize,
        string? dtPagingStyle)
    {
        return new DataViewEditDto
        {
            ViewID = viewId,
            Title = title,
            DataSource = dataSource,
            MainTable = mainTable,
            Primarykey = primaryKey ?? string.Empty,
            ModificationProcedure = modificationProcedure ?? string.Empty,
            ViewProcedure = viewProcedure ?? string.Empty,
            DeleteProcedure = deleteProcedure ?? string.Empty,
            ViewDescription = viewDescription ?? string.Empty,
            OrderBy = orderBy ?? string.Empty,
            RowReorderColumn = rowReorderColumn ?? string.Empty,
            Published = published,
            Flags = flags,
            DataTableModifierButtonStyle = dtModBtnStyle,
            DataTableFlags = dtFlags,
            DataTableDefaultPageSize = dtDefaultPageSize,
            DataTablePagingStyle = dtPagingStyle ?? string.Empty,
        };
    }

    /// <summary>The DataViewFlags lookup rows, read from the table the DDL declares.</summary>
    public async Task<List<DataViewFlagsDto>> GetDataViewFlagsAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.DataViewFlags
            .AsNoTracking()
            .Select(e => new DataViewFlagsDto
            {
                    FlagValue = e.FlagValue,
                    FlagLabel = e.FlagLabel,
                    FlagGlyph = e.FlagGlyph,
                    FlagDefault = e.FlagDefault
            })
            .ToListAsync(ct);
    }

    /// <summary>The DataViewDataTableFlags lookup rows, read from the table the DDL declares.</summary>
    public async Task<List<DataViewDataTableFlagsDto>> GetDataViewDataTableFlagsAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.DataViewDataTableFlags
            .AsNoTracking()
            .Select(e => new DataViewDataTableFlagsDto
            {
                    FlagValue = e.FlagValue,
                    FlagLabel = e.FlagLabel,
                    FlagTooltip = e.FlagTooltip,
                    FlagGlyph = e.FlagGlyph,
                    FlagDefault = e.FlagDefault
            })
            .ToListAsync(ct);
    }

    /// <summary>The DataViewModifierButtonStyles lookup rows, read from the table the DDL declares.</summary>
    public async Task<List<DataViewModifierButtonStylesDto>> GetDataViewModifierButtonStylesAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.DataViewModifierButtonStyles
            .AsNoTracking()
            .Select(e => new DataViewModifierButtonStylesDto
            {
                    StyleValue = e.StyleValue,
                    StyleLabel = e.StyleLabel,
                    StyleClass = e.StyleClass,
                    ShowText = e.ShowText,
                    ShowGlyph = e.ShowGlyph,
                    StyleDefault = e.StyleDefault
            })
            .ToListAsync(ct);
    }

    /// <summary>The DataViewPagingTypes lookup rows, read from the table the DDL declares.</summary>
    public async Task<List<DataViewPagingTypesDto>> GetDataViewPagingTypesAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        return await db.DataViewPagingTypes
            .AsNoTracking()
            .Select(e => new DataViewPagingTypesDto
            {
                    StyleValue = e.StyleValue,
                    StyleLabel = e.StyleLabel,
                    StyleDefault = e.StyleDefault
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Duplicates a data view and the fields that describe it. Returns the new ViewID, or null.
    /// </summary>
    public async Task<int?> CloneDataViewAsync(int viewId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var source = await db.DataViews.AsNoTracking().FirstOrDefaultAsync(v => v.ViewID == viewId, ct);
        if (source is null)
        {
            _logger.LogWarning("CloneDataViewAsync: ViewID {ViewID} not found", viewId);
            return null;
        }

        var copy = new ASPClassic.Domain.Entities.Data.DataView
        {
            Title = $"{source.Title} (copy)",
            DataSource = source.DataSource,
            MainTable = source.MainTable,
            Primarykey = source.Primarykey,
            ModificationProcedure = source.ModificationProcedure,
            ViewProcedure = source.ViewProcedure,
            DeleteProcedure = source.DeleteProcedure,
            ViewDescription = source.ViewDescription,
            OrderBy = source.OrderBy,
            Flags = source.Flags,
            DataTableModifierButtonStyle = source.DataTableModifierButtonStyle,
            DataTableFlags = source.DataTableFlags,
            DataTableDefaultPageSize = source.DataTableDefaultPageSize,
            DataTablePagingStyle = source.DataTablePagingStyle,
            Published = source.Published,
            RowReorderColumn = source.RowReorderColumn,
            CSSTable = source.CSSTable,

            // A copy is never one of the portal's own screens, whatever it was copied from — the
            // system views are protected from deletion and describe the portal's own tables.
            IsSystemObject = false,
        };

        db.DataViews.Add(copy);
        await db.SaveChangesAsync(ct);

        var fields = await db.DataViewFields.AsNoTracking()
            .Where(f => f.ViewID == viewId)
            .OrderBy(f => f.FieldOrder)
            .ToListAsync(ct);

        foreach (var f in fields)
        {
            db.DataViewFields.Add(new ASPClassic.Domain.Entities.Data.DataViewField
            {
                ViewID = copy.ViewID,
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
                LinkedTableGroupField = f.LinkedTableGroupField,
                LinkedTableGlyphField = f.LinkedTableGlyphField,
                LinkedTableTooltipField = f.LinkedTableTooltipField,
                LinkedTableAddition = f.LinkedTableAddition,
                Width = f.Width,
                Height = f.Height,
                FieldDescription = f.FieldDescription,
                FormatPattern = f.FormatPattern,
                FieldTooltip = f.FieldTooltip,

                // The identifier is the field's client-side name and has to be unique to the field,
                // so it is minted for the copy rather than carried over.
                FieldIdentifier = string.Empty,
            });
        }

        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Cloned view {Source} to {Copy} with {Fields} field(s).", viewId, copy.ViewID, fields.Count);

        return copy.ViewID;
    }
}
