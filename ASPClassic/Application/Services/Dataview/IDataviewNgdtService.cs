using ASPClassic.Application.DTOs.Data;
using ASPClassic.Application.DTOs.Dataview;
using ASPClassic.Domain.Entities.Data;

namespace ASPClassic.Application.Services.Dataview;

/// <summary>
/// Service interface for the DataView NGDT page.
/// Port of <c>dataview_ngdt.asp</c>.
/// </summary>
public interface IDataviewNgdtService
{
    /// <summary>
    /// Retrieves a single DataViewField by its FieldID.
    /// Port of <c>GetDataViewField()</c> — SELECT on DataViewField.
    /// </summary>
    Task<DataViewFieldDto?> GetDataViewFieldAsync(int fieldId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a single DataView by its ViewID.
    /// Port of <c>GetDataView()</c> — SELECT on DataView.
    /// </summary>
    Task<DataViewDto?> GetDataViewAsync(int viewId, CancellationToken ct = default);

    /// <summary>
    /// Main page load + data manipulation handler for the DataView NGDT page.
    /// Port of <c>LoadDataviewNgdt</c>.
    /// </summary>
    Task<DataViewFieldDto?> LoadDataviewNgdtAsync(
        string itemID,
        string mode,
        string viewID,
        string postback,
        CancellationToken ct = default);

    /// <summary>
    /// Full page load returning the complete page state including fields, flags,
    /// edit-row data, lookup data, and CRUD results.
    /// </summary>
    Task<DataviewNgdtLoadResultDto> LoadPageAsync(
        string itemID,
        string mode,
        string viewID,
        string postback,
        Dictionary<string, string>? formValues = null,
        CancellationToken ct = default);
}
