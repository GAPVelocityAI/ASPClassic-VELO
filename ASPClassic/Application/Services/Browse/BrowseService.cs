using System.Globalization;
using ASPClassic.Application.DTOs.Browse;
using ASPClassic.Application.DTOs.Data;
using ASPClassic.Domain.Entities.Data;
using ASPClassic.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ASPClassic.Application.Services.Browse;

/// <summary>Port of <c>browse.asp</c> — Browse page service implementation.</summary>
public class BrowseService : IBrowseService
{
    private readonly IDbContextFactory<ASPClassicVBScriptDbContext> _dbFactory;
    private readonly ILogger<BrowseService> _logger;

    public BrowseService(
        IDbContextFactory<ASPClassicVBScriptDbContext> dbFactory,
        ILogger<BrowseService> logger)
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
            .OrderBy(dv => dv.ViewID)
            .FirstOrDefaultAsync(ct);

        if (entity is null)
            return null;

        return MapToDto(entity);
    }

    /// <summary>
    /// Port of legacy GetDataView that retrieves a DataView by its ID.
    /// </summary>
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
    public async Task<List<DataViewDto>> LoadBrowseAsync(
        string dTItemId, string mode, string viewID, CancellationToken ct = default)
    {
        var results = new List<DataViewDto>();

        // Validate and default mode
        if (string.IsNullOrEmpty(mode))
            mode = "browse";

        // Validate ViewID
        if (string.IsNullOrWhiteSpace(viewID) ||
            !int.TryParse(viewID, NumberStyles.Integer, CultureInfo.InvariantCulture, out int nViewID))
        {
            _logger.LogWarning("Browse: ViewID invalid or empty: {ViewID}", viewID);
            return results;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Load the DataView record
        var entity = await db.DataViews
            .AsNoTracking()
            .FirstOrDefaultAsync(dv => dv.ViewID == nViewID, ct);

        if (entity is null)
        {
            _logger.LogWarning("Browse: ViewID not found in database: {ViewID}", nViewID);
            return results;
        }

        // Check published
        if (!entity.Published)
        {
            _logger.LogWarning("Browse: View {ViewID} is not published", nViewID);
            return results;
        }

        // Check browse mode flag (bit 256)
        int nViewFlags = entity.Flags;
        bool browseMode = (nViewFlags & 256) > 0;
        if (!browseMode)
        {
            _logger.LogInformation("Browse: View {ViewID} is not in browse mode", nViewID);
            return results;
        }

        // Map and return the DataView as a single-item list
        var dto = MapToDto(entity);
        results.Add(dto);

        // If mode indicates a specific item and we have the view's data source,
        // also load related views that share the same data source for browse context
        if (!string.IsNullOrEmpty(dTItemId) && !string.IsNullOrEmpty(entity.DataSource))
        {
            var relatedViews = await db.DataViews
                .AsNoTracking()
                .Where(dv => dv.DataSource == entity.DataSource
                          && dv.ViewID != nViewID
                          && dv.Published
                          && (dv.Flags & 256) > 0)
                .OrderBy(dv => dv.Title)
                .ToListAsync(ct);

            foreach (var rv in relatedViews)
            {
                results.Add(MapToDto(rv));
            }
        }

        _logger.LogInformation(
            "Browse: Loaded {Count} view(s) for ViewID {ViewID} in mode '{Mode}'",
            results.Count, nViewID, mode);

        return results;
    }

    /// <inheritdoc />
    public async Task<BrowseResultDto> LoadBrowseResultAsync(
        string dTItemId, string mode, string viewID, CancellationToken ct = default)
    {
        var result = new BrowseResultDto();

        // --- Validate and default mode ---
        // Legacy: IF strMode = "" THEN strMode = "browse"
        if (string.IsNullOrEmpty(mode))
            mode = "browse";

        // --- Validate DT_ItemId ---
        // Legacy: IF NOT IsNumeric(nItemID) THEN nItemID = ""
        string validatedItemId = string.Empty;
        if (!string.IsNullOrWhiteSpace(dTItemId) && int.TryParse(dTItemId, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            validatedItemId = dTItemId;
        }

        // --- Build page title ---
        // Legacy: strPageTitle = UCase(Left(strMode,1)) & Mid(strMode, 2)
        //         IF nItemID <> "" THEN strPageTitle = strPageTitle & " " & nItemID
        string pageTitle = string.Empty;
        if (mode.Length > 0)
        {
            pageTitle = char.ToUpperInvariant(mode[0]) + mode[1..];
        }
        if (!string.IsNullOrEmpty(validatedItemId))
        {
            pageTitle = pageTitle + " " + validatedItemId;
        }
        result.PageTitle = pageTitle;

        // --- Validate ViewID ---
        // Legacy: IF strError = "" AND nViewID <> "" AND IsNumeric(nViewID) THEN ...
        if (string.IsNullOrWhiteSpace(viewID) || !int.TryParse(viewID, NumberStyles.Integer, CultureInfo.InvariantCulture, out int nViewID))
        {
            result.Error = "ViewID Invalid!";
            result.RedirectTo404 = true;
            result.RedirectUrl = "/aspclassic-vbscript/page404?msg=viewnotfound";
            _logger.LogWarning("Browse: ViewID invalid or empty: {ViewID}", viewID);
            return result;
        }

        result.ViewID = nViewID;

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // --- Load DataView record ---
        // Legacy: SELECT * FROM portal.DataView WHERE ViewID = ?
        var entity = await db.DataViews
            .AsNoTracking()
            .FirstOrDefaultAsync(dv => dv.ViewID == nViewID, ct);

        if (entity is null)
        {
            result.Error = "ViewID Not Found!";
            result.RedirectTo404 = true;
            result.RedirectUrl = "/aspclassic-vbscript/page404?msg=viewnotfound";
            _logger.LogWarning("Browse: ViewID not found in database: {ViewID}", nViewID);
            return result;
        }

        // --- Map core fields ---
        string dataSource = entity.DataSource ?? string.Empty;
        if (string.IsNullOrEmpty(dataSource))
            dataSource = "Default";

        result.DataSource = dataSource;
        result.Title = entity.Title ?? string.Empty;
        result.Published = entity.Published;
        result.ViewDescription = entity.ViewDescription ?? string.Empty;
        result.ViewProcedure = entity.ViewProcedure ?? string.Empty;
        result.ModificationProcedure = entity.ModificationProcedure ?? string.Empty;
        result.DeleteProcedure = entity.DeleteProcedure ?? string.Empty;
        result.MainTable = entity.MainTable ?? string.Empty;
        result.OrderBy = entity.OrderBy ?? string.Empty;
        result.RowReorderColumn = entity.RowReorderColumn ?? string.Empty;
        result.Primarykey = entity.Primarykey ?? string.Empty;
        result.DataTableModifierButtonStyle = entity.DataTableModifierButtonStyle;
        result.DataTableDefaultPageSize = entity.DataTableDefaultPageSize;
        result.DataTablePagingStyle = entity.DataTablePagingStyle ?? string.Empty;
        result.CSSTable = entity.CSSTable ?? string.Empty;
        result.IsSystemObject = entity.IsSystemObject;

        int nViewFlags = entity.Flags;
        int nDtFlags = entity.DataTableFlags;
        short nDtModBtnStyle = entity.DataTableModifierButtonStyle;

        // --- Decode Flags (nViewFlags) ---
        // Legacy: blnAllowUpdate = CBool((nViewFlags AND 1) > 0) etc.
        result.AllowUpdate = (nViewFlags & 1) > 0;
        result.AllowInsert = (nViewFlags & 2) > 0;
        result.AllowDelete = (nViewFlags & 4) > 0;
        result.AllowClone = (nViewFlags & 8) > 0;
        result.ShowForm = (nViewFlags & 16) > 0;
        result.ShowList = (nViewFlags & 32) > 0;
        result.ShowCharts = (nViewFlags & 64) > 0;
        result.ShowCustomActions = (nViewFlags & 128) > 0;
        result.BrowseMode = (nViewFlags & 256) > 0;

        // --- Decode DataTableFlags (nDtFlags) ---
        // Legacy: blnDtInfo = CBool((nDtFlags AND 1) > 0) etc.
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

        // --- Compute derived flags ---
        // Legacy: blnShowRowActions = CBool(blnAllowUpdate OR blnAllowDelete OR blnAllowClone OR strRowReorderCol <> "" OR blnAllowRowDetails)
        result.ShowRowActions = result.AllowUpdate
                             || result.AllowDelete
                             || result.AllowClone
                             || !string.IsNullOrEmpty(result.RowReorderColumn)
                             || result.AllowRowDetails;

        // Legacy: blnAllowExport = CBool(blnExportClipboard OR blnExportCSV OR blnExportExcel OR blnExportPDF OR blnExportPrint)
        result.AllowExport = result.ExportClipboard
                          || result.ExportCSV
                          || result.ExportExcel
                          || result.ExportPDF
                          || result.ExportPrint;

        // Legacy: blnAllowExportAll = CBool(blnExportClipboard AND blnExportCSV AND blnExportExcel AND blnExportPDF AND blnExportPrint)
        result.AllowExportAll = result.ExportClipboard
                             && result.ExportCSV
                             && result.ExportExcel
                             && result.ExportPDF
                             && result.ExportPrint;

        // --- Resolve modifier button style index ---
        // Legacy: FOR nIndex = 0 TO UBound(arrDataTableModifierButtonStyles, 2)
        //             IF nDtModBtnStyle = arrDataTableModifierButtonStyles(dtbsValue, nIndex) THEN
        //                 nDtModBtnStyleIndex = nIndex
        //             END IF
        //         NEXT
        var modifierButtonStyles = await db.DataViewModifierButtonStyles
            .AsNoTracking()
            .ToListAsync(ct);

        result.DtModBtnStyleIndex = 0;
        for (int i = 0; i < modifierButtonStyles.Count; i++)
        {
            if (int.TryParse(modifierButtonStyles[i].StyleValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int styleVal)
                && nDtModBtnStyle == styleVal)
            {
                result.DtModBtnStyleIndex = i;
                break;
            }
        }

        // --- Check published state ---
        // Legacy: IF NOT blnPublished OR strError <> "" THEN Response.Redirect "404.asp?msg=viewnotfound"
        if (!result.Published)
        {
            result.Error = "View is not published.";
            result.RedirectTo404 = true;
            result.RedirectUrl = "/aspclassic-vbscript/page404?msg=viewnotfound";
            _logger.LogWarning("Browse: View {ViewID} is not published, redirecting to 404", nViewID);
            return result;
        }

        // --- Build view query string ---
        // Legacy: strViewQueryString = "&ViewID=" & nViewID
        result.ViewQueryString = $"&ViewID={nViewID}";

        // --- Check browse mode ---
        // Legacy: IF NOT blnBrowseMode THEN Response.Redirect("dataview.asp?" & Request.QueryString)
        if (!result.BrowseMode)
        {
            result.RedirectToDataview = true;
            result.RedirectUrl = $"/aspclassic-vbscript/dataview?ViewID={nViewID}";
            _logger.LogInformation("Browse: View {ViewID} is not in browse mode, redirecting to dataview", nViewID);
            return result;
        }

        _logger.LogInformation(
            "Browse: Successfully loaded view {ViewID} ({Title}) in mode '{Mode}'",
            nViewID, result.Title, mode);

        return result;
    }

    /// <summary>Maps a <see cref="DataView"/> entity to a <see cref="DataViewDto"/>.</summary>
    private static DataViewDto MapToDto(ASPClassic.Domain.Entities.Data.DataView entity)
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
}
