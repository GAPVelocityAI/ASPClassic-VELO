using System.Globalization;
using ASPClassic.Application.DTOs.Core;
using ASPClassic.Domain.Entities.Core;
using ASPClassic.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ASPClassic.Application.Services.View;

/// <summary>Port of <c>view.asp</c> — navigation lookup and view routing service.</summary>
public class ViewService : IViewService
{
    private readonly IDbContextFactory<ASPClassicVBScriptDbContext> _dbFactory;
    private readonly ILogger<ViewService> _logger;

    public ViewService(
        IDbContextFactory<ASPClassicVBScriptDbContext> dbFactory,
        ILogger<ViewService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <summary>
    /// Port of legacy <c>GetNavigation()</c> — retrieves the first Navigation record ordered by NavOrder.
    /// Returns the first navigation record, or null if the table is empty.
    /// Legacy code: SELECT on Navigation table.
    /// </summary>
    public async Task<NavigationDto?> GetNavigationAsync(CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.Navigations
            .AsNoTracking()
            .OrderBy(n => n.NavOrder)
            .FirstOrDefaultAsync(ct);

        if (entity is null)
        {
            _logger.LogWarning("No navigation records found in the Navigation table.");
            return null;
        }

        return MapToDto(entity);
    }

    /// <summary>
    /// Port of legacy <c>GetNavigation(navId)</c> — retrieves a specific Navigation record by its integer NavId.
    /// </summary>
    public async Task<NavigationDto?> GetNavigationAsync(int navId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entity = await db.Navigations
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.NavId == navId, ct);

        if (entity is null)
        {
            _logger.LogWarning("Navigation record not found for NavId={NavId}", navId);
            return null;
        }

        return MapToDto(entity);
    }

    /// <summary>
    /// Port of legacy <c>LoadView(navID)</c>.
    /// Legacy logic:
    ///   1. Validate navID is numeric; if not, return null (legacy defaulted to 404.asp).
    ///   2. Query portal.Navigation WHERE NavId = nNavID.
    ///   3. If record found:
    ///      - If ViewID is numeric, the page URL is "dataview.asp?ViewID={ViewID}"
    ///        (caller navigates to the dataview page).
    ///      - Otherwise, the page URL is NavUri (external/custom URI).
    ///      - strPageTitle = NavLabel.
    ///   4. Return the NavigationDto so the calling page can decide routing.
    /// </summary>
    public async Task<NavigationDto?> LoadViewAsync(string navID, CancellationToken ct = default)
    {
        // Legacy: IF NOT IsNumeric(nNavID) THEN nNavID = ""
        // Then: IF nNavID <> "" THEN ... query
        if (string.IsNullOrWhiteSpace(navID))
        {
            _logger.LogWarning("LoadViewAsync called with empty navID — no navigation record to load.");
            return null;
        }

        if (!int.TryParse(navID, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedNavId))
        {
            _logger.LogWarning("LoadViewAsync called with non-numeric navID: {NavID}", navID);
            return null;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        // Legacy: SELECT * FROM portal.Navigation WHERE NavId = nNavID
        var entity = await db.Navigations
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.NavId == parsedNavId, ct);

        if (entity is null)
        {
            _logger.LogWarning("Navigation record not found for NavId={NavId}", parsedNavId);
            return null;
        }

        // Legacy logic determined the page URL based on whether ViewID was numeric:
        //   IF IsNumeric(rsItems("ViewID")) THEN
        //       strPageURL = "dataview.asp?ViewID=" & rsItems("ViewID")
        //   ELSE
        //       strPageURL = rsItems("NavUri")
        //   END IF
        // The DTO carries both ViewID and NavUri so the caller (the page component)
        // can make the same routing decision. ViewID being non-null and > 0 means
        // the caller should route to the dataview page; otherwise use NavUri.

        _logger.LogInformation(
            "Loaded navigation record NavId={NavId}, Label={Label}, ViewID={ViewID}, NavUri={NavUri}",
            entity.NavId,
            entity.NavLabel,
            entity.ViewID,
            entity.NavUri);

        return MapToDto(entity);
    }

    /// <summary>
    /// Maps a Navigation entity to a NavigationDto, handling nullable-to-non-nullable coalescing.
    /// </summary>
    private static NavigationDto MapToDto(Navigation entity)
    {
        return new NavigationDto
        {
            NavId = entity.NavId,
            NavLabel = entity.NavLabel ?? string.Empty,
            NavParentId = entity.NavParentId ?? 0,
            NavOrder = entity.NavOrder,
            NavUri = entity.NavUri ?? string.Empty,
            NavGlyph = entity.NavGlyph ?? string.Empty,
            NavTooltip = entity.NavTooltip ?? string.Empty,
            ViewID = entity.ViewID ?? 0,
            OpenUriInIFRAME = entity.OpenUriInIFRAME
        };
    }
}
