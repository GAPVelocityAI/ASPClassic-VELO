using ASPClassic.Application.DTOs.Browse;
using ASPClassic.Application.DTOs.Data;
using ASPClassic.Domain.Entities.Data;

namespace ASPClassic.Application.Services.Browse;

/// <summary>Port of <c>browse.asp</c> — Browse page service.</summary>
public interface IBrowseService
{
    /// <summary>
    /// Retrieves a single DataView by its ID.
    /// Port of the GetDataView SELECT on DataView table.
    /// </summary>
    Task<DataViewDto?> GetDataViewAsync(CancellationToken ct = default);

    /// <summary>
    /// Loads the full browse page state: validates ViewID, loads the DataView record,
    /// decodes all bitwise flags, validates published/browse mode, and returns
    /// the list of resolved DataView DTOs.
    /// Port of the main script block in <c>browse.asp</c>.
    /// </summary>
    Task<List<DataViewDto>> LoadBrowseAsync(string dTItemId, string mode, string viewID, CancellationToken ct = default);

    /// <summary>
    /// Loads the browse result with fully decoded flags and redirect instructions.
    /// Extended version used by the Browse page component.
    /// </summary>
    Task<BrowseResultDto> LoadBrowseResultAsync(string dTItemId, string mode, string viewID, CancellationToken ct = default);
}
