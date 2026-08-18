using ASPClassic.Application.DTOs.Core;
using ASPClassic.Domain.Entities.Core;

namespace ASPClassic.Application.Services.View;

/// <summary>Port of <c>view.asp</c> — navigation lookup and view routing service.</summary>
public interface IViewService
{
    /// <summary>
    /// Retrieves a single Navigation record by its NavId.
    /// Port of the legacy SELECT on Navigation table.
    /// </summary>
    Task<NavigationDto?> GetNavigationAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves a Navigation record by its NavId (integer).
    /// Port of legacy GetNavigation subroutine.
    /// </summary>
    Task<NavigationDto?> GetNavigationAsync(int navId, CancellationToken ct = default);

    /// <summary>
    /// Loads a Navigation record by navID string, validates numeric input,
    /// and returns the navigation info (ViewID, NavUri, NavLabel) for routing.
    /// Port of <c>view.asp</c> main page logic.
    /// </summary>
    Task<NavigationDto?> LoadViewAsync(string navID, CancellationToken ct = default);
}
