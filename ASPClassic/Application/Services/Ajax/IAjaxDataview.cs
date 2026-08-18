using ASPClassic.Application.DTOs.Data;
using ASPClassic.Domain.Entities.Data;

namespace ASPClassic.Application.Services.Ajax;

/// <summary>Port of <c>ajax_dataview.asp</c> — AJAX endpoint for DataView CRUD, datatable, dataviewcontents, autoinit, and navigation operations.</summary>
public interface IAjaxDataview
{
    /// <summary>Retrieves a single DataViewField by its FieldID.</summary>
    Task<DataViewFieldDto?> GetDataViewFieldAsync(CancellationToken ct = default);

    /// <summary>Retrieves a single DataView by its ViewID.</summary>
    Task<DataViewDto?> GetDataViewAsync(CancellationToken ct = default);

    /// <summary>Retrieves a DataViewField by a specific FieldID.</summary>
    Task<DataViewFieldDto?> GetDataViewFieldAsync(int fieldId, CancellationToken ct = default);

    /// <summary>Retrieves a DataView by a specific ViewID.</summary>
    Task<DataViewDto?> GetDataViewAsync(int viewId, CancellationToken ct = default);

    /// <summary>
    /// Main dispatch method porting the full ajax_dataview.asp logic.
    /// Handles modes: add, edit, delete, delete_multiple, reorder, autoinit, dataviewcontents, datatable, getSiteNav.
    /// </summary>
    Task<DataViewFieldDto?> LoadAjaxDataviewAsync(
        string mode, string viewID, string postback, string dTRowID,
        string draw, string length, string start, string browse,
        CancellationToken ct = default);
}
