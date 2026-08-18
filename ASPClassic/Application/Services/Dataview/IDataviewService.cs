using ASPClassic.Application.DTOs.Data;
using ASPClassic.Application.DTOs.Dataview;
using ASPClassic.Domain.Entities.Data;

namespace ASPClassic.Application.Services.Dataview;

/// <summary>Port of <c>dataview.asp</c> — DataView loading, metadata retrieval, and server-side data table queries.</summary>
public interface IDataviewService
{
    /// <summary>Retrieves a DataView by ViewID from the database.</summary>
    Task<DataViewDto?> GetDataViewAsync(CancellationToken ct = default);

    /// <summary>Retrieves a DataView by specific ViewID.</summary>
    Task<DataViewDto?> GetDataViewAsync(int viewId, CancellationToken ct = default);

    /// <summary>
    /// Main page load operation: fetches a DataView by ViewID, decodes all flag bitmasks,
    /// loads fields and actions, and returns a fully-resolved result DTO.
    /// Port of the top-level script in <c>dataview.asp</c>.
    /// </summary>
    Task<DataViewDto?> LoadDataviewAsync(string dTItemId, string mode, string viewID, CancellationToken ct = default);

    /// <summary>Retrieves the title for a ViewID.</summary>
    Task<string?> GetDataViewLabelAsync(int viewId);

    /// <summary>Returns DataView metadata for the given ViewID.</summary>
    Task<DataViewDto?> GetDataViewInfoAsync(int viewId);

    /// <summary>Retrieves the DataView contents result set.</summary>
    Task<DataViewDto?> GetDataViewContentsAsync(int viewId);

    /// <summary>Retrieves the DataView contents command.</summary>
    Task<DataViewDto?> GetDataViewContentsCommandAsync(int viewId);

    /// <summary>
    /// Server-side paging, filtering, sorting, and column ordering for DataView data tables.
    /// </summary>
    Task<DataViewDto?> GetDataViewDataTableCommandAsync(
        int viewId, int draw, int start, int length,
        string searchValue, bool searchRegEx,
        string columnsOptionsXml, string columnsOrderXml,
        bool filteringByPk);

    /// <summary>
    /// Returns the fully-resolved load result with decoded flags, fields, and actions.
    /// Used by the Dataview page to get all rendering information in one call.
    /// </summary>
    Task<DataViewLoadResultDto?> LoadDataviewFullAsync(string dTItemId, string mode, string viewID, CancellationToken ct = default);

    /// <summary>The fields this view shows, in their configured order.</summary>
    Task<List<DataViewFieldDto>> GetDataViewFieldsAsync(int viewId, CancellationToken ct = default);

    /// <summary>The view's actions — per-row when <paramref name="isPerRow"/>, toolbar otherwise.</summary>
    Task<List<DataViewActionDto>> GetDataViewActionsAsync(int viewId, bool isPerRow, CancellationToken ct = default);

    /// <summary>
    /// Writes one record of the view's own table — the legacy's <c>ajax_dataview.asp</c> post.
    /// </summary>
    /// <remarks>
    /// A data view names the table it edits, so the statement is built from the view's own field
    /// list rather than from a typed entity. Returns the error the caller should show, or null when
    /// the record was written.
    /// </remarks>
    Task<string?> SaveDataviewRecordAsync(
        int viewId, string mode, string? itemId,
        IReadOnlyDictionary<string, string> values, CancellationToken ct = default);

    /// <summary>
    /// The rows the grid shows — paged, filtered and sorted by the database.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="GetDataViewDataTableCommandAsync"/>, which returns the view's
    /// definition and runs no query at all.
    /// </remarks>
    Task<ASPClassic.Infrastructure.Engines.DataViewResultDto> GetDataViewRowsAsync(
        int viewId, int start, int length, string searchValue, bool filteringByPk = false,
        IReadOnlyDictionary<string, string>? columnFilters = null);

    /// <summary>
    /// One record of the view's own table, by key, as column-name to value.
    /// </summary>
    /// <remarks>
    /// What the edit form needs in order to show what is already there. Returns an empty dictionary
    /// when the record is absent.
    /// </remarks>
    Task<Dictionary<string, string>> GetDataviewRecordAsync(
        int viewId, string itemId, CancellationToken ct = default);

    /// <summary>
    /// The published views, for building a menu from — newest last, system views separated.
    /// </summary>
    /// <remarks>
    /// Derived from the DataView table rather than stored in Navigation, so a view added today
    /// appears without anyone having to add a menu entry for it as well.
    /// </remarks>
    Task<List<DataViewDto>> GetNavigableDataViewsAsync(CancellationToken ct = default);

    /// <summary>
    /// The choices for a field that draws them from another table — value and label, in label order.
    /// </summary>
    /// <remarks>
    /// A field of type "Selection Dropdown Box" names the table, the value column and the label
    /// column it draws from. Rendered as free text instead, nothing stops a foreign key being typed
    /// as a word, and the row it points at then does not exist.
    /// </remarks>
    Task<List<(string Value, string Label)>> GetLookupOptionsAsync(
        string linkedTable, string valueField, string titleField, CancellationToken ct = default);

    /// <summary>
    /// Deletes one record of the view's own table. Returns the error to show, or null on success.
    /// </summary>
    Task<string?> DeleteDataviewRecordAsync(int viewId, string itemId, CancellationToken ct = default);
}
