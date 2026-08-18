using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ASPClassic.Application.DTOs.Data;
using ASPClassic.Infrastructure.Data;
using ASPClassic.Domain.Entities.Data;

namespace ASPClassic.Infrastructure.Caching;

/// <summary>
/// In-memory caching layer for DataView metadata (fields, actions, charts) to reduce repeated
/// DB round-trips during rendering.
/// <para>Legacy source: New abstraction.</para>
/// </summary>
public class DataViewCacheService
{
    private readonly IDbContextFactory<ASPClassicVBScriptDbContext> _dbFactory;
    private readonly ILogger<DataViewCacheService> _logger;
    private readonly ConcurrentDictionary<int, DataViewInfoDto> _cache = new();

    public DataViewCacheService(
        IDbContextFactory<ASPClassicVBScriptDbContext> dbFactory,
        ILogger<DataViewCacheService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <summary>
    /// Returns the cached <see cref="DataViewInfoDto"/> for the given viewId, loading it from
    /// the database on the first request.
    /// </summary>
    public async Task<DataViewInfoDto> GetOrLoadDataViewInfoAsync(int viewId)
    {
        if (_cache.TryGetValue(viewId, out var cached))
        {
            return cached;
        }

        _logger.LogDebug("Cache miss for DataView {ViewId}. Loading from database.", viewId);

        await using var db = await _dbFactory.CreateDbContextAsync();

        var dataView = await db.DataViews
            .AsNoTracking()
            .Where(dv => dv.ViewID == viewId)
            .Select(dv => new DataViewDto
            {
                ViewID = dv.ViewID,
                Title = dv.Title,
                DataSource = dv.DataSource ?? string.Empty,
                MainTable = dv.MainTable ?? string.Empty,
                Primarykey = dv.Primarykey ?? string.Empty,
                ModificationProcedure = dv.ModificationProcedure ?? string.Empty,
                ViewProcedure = dv.ViewProcedure ?? string.Empty,
                DeleteProcedure = dv.DeleteProcedure ?? string.Empty,
                ViewDescription = dv.ViewDescription ?? string.Empty,
                OrderBy = dv.OrderBy ?? string.Empty,
                Flags = dv.Flags,
                DataTableModifierButtonStyle = dv.DataTableModifierButtonStyle,
                DataTableFlags = dv.DataTableFlags,
                DataTableDefaultPageSize = dv.DataTableDefaultPageSize,
                DataTablePagingStyle = dv.DataTablePagingStyle,
                Published = dv.Published,
                RowReorderColumn = dv.RowReorderColumn ?? string.Empty,
                IsSystemObject = dv.IsSystemObject,
                CSSTable = dv.CSSTable
            })
            .FirstOrDefaultAsync();

        var fields = await db.DataViewFields
            .AsNoTracking()
            .Where(f => f.ViewID == viewId)
            .OrderBy(f => f.FieldOrder)
            .Select(f => new DataViewFieldDto
            {
                ViewID = f.ViewID,
                FieldID = f.FieldID,
                FieldLabel = f.FieldLabel,
                FieldSource = f.FieldSource ?? string.Empty,
                FieldType = f.FieldType,
                FieldFlags = f.FieldFlags,
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
                FieldIdentifier = f.FieldIdentifier ?? string.Empty
            })
            .ToListAsync();

        var actions = await db.DataViewActions
            .AsNoTracking()
            .Where(a => a.ViewID == viewId)
            .OrderBy(a => a.ActionOrder)
            .Select(a => new DataViewActionDto
            {
                ActionID = a.ActionID,
                ViewID = a.ViewID,
                ActionLabel = a.ActionLabel,
                ParentActionID = a.ParentActionID ?? 0,
                ActionTooltip = a.ActionTooltip ?? string.Empty,
                ActionDescription = a.ActionDescription ?? string.Empty,
                ActionOrder = a.ActionOrder,
                RequireConfirmation = a.RequireConfirmation,
                OpenURLInNewWindow = a.OpenURLInNewWindow ?? false,
                ActionExpression = a.ActionExpression ?? string.Empty,
                GlyphIcon = a.GlyphIcon ?? string.Empty,
                IsPerRow = a.IsPerRow,
                CSSButton = a.CSSButton ?? string.Empty,
                ActionType = a.ActionType,
                DataViewTitle = a.DataViewTitle ?? string.Empty
            })
            .ToListAsync();

        var charts = await db.DataViewCharts
            .AsNoTracking()
            .Where(c => c.ViewID == viewId)
            .OrderBy(c => c.ChartOrder)
            .Select(c => new DataViewChartDto
            {
                ViewID = c.ViewID,
                ChartID = c.ChartID,
                ChartType = c.ChartType,
                ChartOrder = c.ChartOrder ?? 0,
                ChartGridWidth = c.ChartGridWidth,
                ChartProperties = c.ChartProperties ?? string.Empty,
                XField = c.XField ?? string.Empty,
                YField = c.YField ?? string.Empty,
                ZField = c.ZField ?? string.Empty
            })
            .ToListAsync();

        var info = new DataViewInfoDto
        {
            DataView = dataView,
            Fields = fields,
            Actions = actions,
            Charts = charts
        };

        _cache.TryAdd(viewId, info);
        return info;
    }

    /// <summary>
    /// Invalidates the cache entry for a specific DataView, forcing a reload on next access.
    /// </summary>
    public void Invalidate(int viewId)
    {
        _cache.TryRemove(viewId, out _);
        _logger.LogDebug("Cache invalidated for DataView {ViewId}.", viewId);
    }

    /// <summary>
    /// Clears all cached DataView metadata.
    /// </summary>
    public void InvalidateAll()
    {
        _cache.Clear();
        _logger.LogDebug("All DataView cache entries invalidated.");
    }
}
