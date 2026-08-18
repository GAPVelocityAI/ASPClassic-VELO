using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ASPClassic.Infrastructure.Caching;
using ASPClassic.Infrastructure.Data;
using ASPClassic.Infrastructure.Helpers;
using ASPClassic.Domain.Entities.Data;

namespace ASPClassic.Infrastructure.Engines;

/// <summary>
/// Encapsulates dynamic SQL construction and execution for DataView contents, server-side paging,
/// filtering, sorting and column ordering used by Ajax endpoints.
/// <para>Legacy source: New abstraction — ported from ajax_dataview.asp which built dynamic SQL
/// from DataView metadata (DataSource, fields, OrderBy) and returned JSON for DataTables.</para>
/// </summary>
public class DataViewQueryEngine
{
    private readonly IDbContextFactory<ASPClassicVBScriptDbContext> _dbFactory;
    private readonly DataViewCacheService _cacheService;
    private readonly JsonValueFormatter _jsonFormatter;
    private readonly ILogger<DataViewQueryEngine> _logger;

    public DataViewQueryEngine(
        IDbContextFactory<ASPClassicVBScriptDbContext> dbFactory,
        DataViewCacheService cacheService,
        JsonValueFormatter jsonFormatter,
        ILogger<DataViewQueryEngine> logger)
    {
        _dbFactory = dbFactory;
        _cacheService = cacheService;
        _jsonFormatter = jsonFormatter;
        _logger = logger;
    }

    /// <summary>
    /// Executes a server-side paged, filtered, sorted query against the DataView's data source.
    /// Returns results in DataTables-compatible format.
    /// </summary>
    public async Task<DataViewResultDto> ExecuteDataTableQueryAsync(
        int viewId,
        int draw,
        int start,
        int length,
        string searchValue,
        bool searchRegEx,
        string columnsOptionsXml,
        string columnsOrderXml,
        bool filteringByPk,
        IReadOnlyDictionary<string, string>? columnFilters = null)
    {
        var result = new DataViewResultDto { Draw = draw };

        try
        {
            var viewInfo = await _cacheService.GetOrLoadDataViewInfoAsync(viewId);
            if (viewInfo.DataView is null)
            {
                result.Error = $"DataView with ID {viewId} not found.";
                return result;
            }

            var dv = viewInfo.DataView;

            // DataSource names a CONNECTION, not a table — the legacy looks it up in the
            // connectionStrings config to decide which database to open. The rows come from
            // MainTable, or from ViewProcedure when the view supplies one. Reading FROM the
            // DataSource asks the database for a table called "CrudeDefault", which does not exist.
            var dataSource = ResolveRowSource(dv);

            if (dataSource is null)
            {
                result.Error = "DataView names no table or view procedure to read from.";
                return result;
            }

            // Build visible field list from cached fields
            var visibleFields = viewInfo.Fields
                // Bit 8 is Show in Items List; bit 1 is Show in Form. The grid is the list.
                .Where(f => (f.FieldFlags & 8) > 0)
                .OrderBy(f => f.FieldOrder)
                .ToList();

            if (visibleFields.Count == 0)
            {
                result.Error = "DataView has no visible fields configured.";
                return result;
            }

            var fieldNames = visibleFields.Select(f =>
                !string.IsNullOrWhiteSpace(f.FieldSource) ? f.FieldSource : f.FieldLabel
            ).ToList();

            await using var db = await _dbFactory.CreateDbContextAsync();
            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync();

            // Count total records
            var countSql = $"SELECT COUNT(*) FROM ({dataSource})";
            using (var countCmd = connection.CreateCommand())
            {
                countCmd.CommandText = countSql;
                var totalObj = await countCmd.ExecuteScalarAsync();
                result.RecordsTotal = Convert.ToInt32(totalObj);
            }

            // Build filtered query
            var whereClauses = new List<string>();
            var parameters = new List<SqliteParameter>();

            if (!string.IsNullOrWhiteSpace(searchValue) && !filteringByPk)
            {
                var searchConditions = new List<string>();
                for (int i = 0; i < fieldNames.Count; i++)
                {
                    var paramName = $"@search{i}";
                    searchConditions.Add($"CAST([{fieldNames[i]}] AS TEXT) LIKE {paramName}");
                    parameters.Add(new SqliteParameter(paramName, $"%{searchValue}%"));
                }
                if (searchConditions.Count > 0)
                {
                    whereClauses.Add($"({string.Join(" OR ", searchConditions)})");
                }
            }
            else if (!string.IsNullOrWhiteSpace(searchValue) && filteringByPk)
            {
                var pk = dv.Primarykey;
                if (!string.IsNullOrWhiteSpace(pk))
                {
                    whereClauses.Add($"[{pk}] = @pkValue");
                    parameters.Add(new SqliteParameter("@pkValue", searchValue));
                }
            }

            // Filters aimed at ONE column, which is how the portal narrows a shared screen to a
            // single parent: the Fields button opens the field list with the parent view's id
            // targeted at the Data View column. A global text search cannot express that — it
            // matches the value anywhere in the row and returns everything that happens to contain
            // it.
            if (columnFilters is { Count: > 0 })
            {
                var index = 0;

                // Validated against EVERY field of the view, not only the displayed ones. A column
                // can be filterable and hidden — the field list's own Data View column is exactly
                // that, which is how the Fields button narrows the list without showing the id it
                // narrows by. Checking against the visible set rejects precisely the filter that
                // matters, and the screen then shows every row as if no filter had been asked for.
                var filterable = viewInfo.Fields
                    .Select(f => !string.IsNullOrWhiteSpace(f.FieldSource) ? f.FieldSource : f.FieldLabel)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();

                foreach (var (column, value) in columnFilters)
                {
                    // Still only a column this view declares, so the name can never be one a
                    // request invented.
                    if (!filterable.Contains(column, StringComparer.OrdinalIgnoreCase)) continue;

                    var name = $"@col{index++}";
                    whereClauses.Add($"CAST([{column}] AS TEXT) = {name}");
                    parameters.Add(new SqliteParameter(name, value));
                }
            }

            var whereClause = whereClauses.Count > 0
                ? "WHERE " + string.Join(" AND ", whereClauses)
                : string.Empty;

            // Count filtered
            var filteredCountSql = $"SELECT COUNT(*) FROM ({dataSource}) AS dvSource {whereClause}";
            using (var filteredCmd = connection.CreateCommand())
            {
                filteredCmd.CommandText = filteredCountSql;
                foreach (var p in parameters)
                {
                    var clonedParam = new SqliteParameter(p.ParameterName, p.Value);
                    filteredCmd.Parameters.Add(clonedParam);
                }
                var filteredObj = await filteredCmd.ExecuteScalarAsync();
                result.RecordsFiltered = Convert.ToInt32(filteredObj);
            }

            // Build ORDER BY
            var orderBy = !string.IsNullOrWhiteSpace(dv.OrderBy)
                ? dv.OrderBy
                : (fieldNames.Count > 0 ? $"[{fieldNames[0]}]" : "1");

            // Build paged data query. The primary key is selected even when it is not one of the
            // displayed fields: without it a row cannot say which record it is, and every edit and
            // delete has to guess.
            var selectNames = new List<string>(fieldNames);
            if (!string.IsNullOrWhiteSpace(dv.Primarykey)
                && !selectNames.Contains(dv.Primarykey, StringComparer.OrdinalIgnoreCase))
            {
                selectNames.Insert(0, dv.Primarykey);
            }

            var selectFields = string.Join(", ", selectNames.Select(f => $"[{f}]"));
            var dataSql = $"SELECT {selectFields} FROM ({dataSource}) AS dvSource {whereClause} ORDER BY {orderBy} LIMIT @pageSize OFFSET @pageStart";

            using var dataCmd = connection.CreateCommand();
            dataCmd.CommandText = dataSql;
            dataCmd.Parameters.Add(new SqliteParameter("@pageSize", length > 0 ? length : 25));
            dataCmd.Parameters.Add(new SqliteParameter("@pageStart", start));
            foreach (var p in parameters)
            {
                var clonedParam = new SqliteParameter(p.ParameterName, p.Value);
                dataCmd.Parameters.Add(clonedParam);
            }

            using var reader = await dataCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var colName = reader.GetName(i);
                    var value = reader.IsDBNull(i) ? string.Empty : reader.GetValue(i)?.ToString() ?? string.Empty;
                    row[colName] = value;
                }
                result.Data.Add(row);
            }

            _logger.LogDebug(
                "DataView {ViewId} query: {Total} total, {Filtered} filtered, returning {Count} rows (start={Start}, length={Length}).",
                viewId, result.RecordsTotal, result.RecordsFiltered, result.Data.Count, start, length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing DataView query for ViewId {ViewId}.", viewId);
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Executes the DataView's data source query and returns all rows (no paging).
    /// Used for non-DataTable rendering (e.g., browse mode, view mode).
    /// </summary>
    public async Task<DataViewResultDto> ExecuteViewContentsAsync(int viewId)
    {
        var result = new DataViewResultDto { Draw = 1 };

        try
        {
            var viewInfo = await _cacheService.GetOrLoadDataViewInfoAsync(viewId);
            if (viewInfo.DataView is null)
            {
                result.Error = $"DataView with ID {viewId} not found.";
                return result;
            }

            // See ResolveRowSource: DataSource is a connection name, not a table.
            var dataSource = ResolveRowSource(viewInfo.DataView);
            if (dataSource is null)
            {
                result.Error = "DataView names no table or view procedure to read from.";
                return result;
            }

            var orderBy = !string.IsNullOrWhiteSpace(viewInfo.DataView.OrderBy)
                ? $" ORDER BY {viewInfo.DataView.OrderBy}"
                : string.Empty;

            await using var db = await _dbFactory.CreateDbContextAsync();
            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT * FROM ({dataSource}) AS dvSource{orderBy}";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var colName = reader.GetName(i);
                    var value = reader.IsDBNull(i) ? string.Empty : reader.GetValue(i)?.ToString() ?? string.Empty;
                    row[colName] = value;
                }
                result.Data.Add(row);
            }

            result.RecordsTotal = result.Data.Count;
            result.RecordsFiltered = result.Data.Count;

            _logger.LogDebug("DataView {ViewId} view contents: {Count} rows.", viewId, result.Data.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing view contents for ViewId {ViewId}.", viewId);
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Executes the DataView's ViewProcedure (if configured) as a command.
    /// Used for executing stored procedures or action commands associated with the view.
    /// </summary>
    public async Task ExecuteViewContentsCommandAsync(int viewId)
    {
        var viewInfo = await _cacheService.GetOrLoadDataViewInfoAsync(viewId);
        if (viewInfo.DataView is null)
        {
            _logger.LogWarning("Cannot execute command: DataView {ViewId} not found.", viewId);
            return;
        }

        var viewProcedure = viewInfo.DataView.ViewProcedure;
        if (string.IsNullOrWhiteSpace(viewProcedure))
        {
            _logger.LogDebug("DataView {ViewId} has no ViewProcedure configured. Skipping command execution.", viewId);
            return;
        }

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync();
            var connection = db.Database.GetDbConnection();
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = viewProcedure;
            await cmd.ExecuteNonQueryAsync();

            _logger.LogInformation("Successfully executed ViewProcedure for DataView {ViewId}.", viewId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing ViewProcedure for DataView {ViewId}.", viewId);
            throw;
        }
    }

    /// <summary>
    /// The SQL a view's rows come from, ready to sit inside <c>FROM (...)</c>, or null when the
    /// view names nothing to read.
    /// </summary>
    /// <remarks>
    /// <c>ViewProcedure</c> wins when present, exactly as the legacy preferred it; otherwise the
    /// rows are the whole of <c>MainTable</c>. The schema prefix is dropped because the generated
    /// database maps a table by its bare name.
    /// </remarks>
    private static string? ResolveRowSource(Application.DTOs.Data.DataViewDto dv)
    {
        static string Bare(string? qualified)
        {
            var v = (qualified ?? string.Empty).Trim().Trim('[', ']');
            var dot = v.LastIndexOf('.');
            return (dot >= 0 ? v[(dot + 1)..] : v).Trim('[', ']');
        }

        if (!string.IsNullOrWhiteSpace(dv.ViewProcedure))
            return $"SELECT * FROM [{Bare(dv.ViewProcedure)}]";

        var table = Bare(dv.MainTable);
        return table.Length == 0 ? null : $"SELECT * FROM [{table}]";
    }
}
