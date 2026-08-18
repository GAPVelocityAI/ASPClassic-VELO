using ASPClassic.Application.DTOs.Admin;
using ASPClassic.Application.DTOs.Data;
using ASPClassic.Domain.Entities.Data;

namespace ASPClassic.Application.Services.Admin;

/// <summary>
/// Service interface for managing DataView field definitions.
/// Port of <c>admin_dataviewfields.asp</c>.
/// </summary>
public interface IAdminDataviewfieldsService
{
    /// <summary>Retrieves a DataView by its ID (no extra parameters).</summary>
    Task<DataViewDto?> GetDataViewAsync(CancellationToken ct = default);

    /// <summary>Retrieves a single DataViewField (no extra parameters).</summary>
    Task<DataViewFieldDto?> GetDataViewFieldAsync(CancellationToken ct = default);

    /// <summary>Retrieves a DataView by its ID (parameterized overload).</summary>
    Task<DataViewDto?> GetDataViewByIdAsync(int viewId, CancellationToken ct = default);

    /// <summary>Retrieves a single DataViewField by its FieldID (parameterized overload).</summary>
    Task<DataViewFieldDto?> GetDataViewFieldByIdAsync(int fieldId, CancellationToken ct = default);

    /// <summary>
    /// Main page-load handler: processes add/edit/delete/sortFields/autoinit modes
    /// and returns the current DataView DTO (legacy signature).
    /// Port of <c>admin_dataviewfields.asp</c>.
    /// </summary>
    Task<DataViewDto?> LoadAdminDataviewfieldsAsync(
        string mode, string itemID, string viewID,
        string fieldLabel, string fieldSource, string fieldType,
        string fieldDescription, string defaultValue,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves the full page result including field list and current edit data.
    /// </summary>
    Task<AdminDataviewfieldsResultDto> LoadPageAsync(
        string mode, string itemID, string viewID,
        CancellationToken ct = default);

    /// <summary>
    /// Saves or updates a DataViewField with full field data from the edit form.
    /// Port of the add/edit branch of <c>admin_dataviewfields.asp</c>.
    /// </summary>
    Task<AdminDataviewfieldsResultDto> SaveDataViewFieldAsync(
        string mode, int viewId, int? fieldId,
        DataViewFieldEditDto fieldData,
        List<int> fieldFlagValues,
        CancellationToken ct = default);

    /// <summary>
    /// Updates an existing DataViewField.
    /// Port of the edit/update branch of <c>admin_dataviewfields.asp</c>.
    /// </summary>
    Task<AdminDataviewfieldsResultDto> UpdateDataViewFieldAsync(
        int fieldId, int viewId,
        DataViewFieldEditDto fieldData,
        List<int> fieldFlagValues,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a DataViewField by FieldID.
    /// Port of the delete branch of <c>admin_dataviewfields.asp</c>.
    /// </summary>
    Task<AdminDataviewfieldsResultDto> DeleteDataViewFieldAsync(
        int fieldId, int viewId, CancellationToken ct = default);

    /// <summary>
    /// Reorders fields by applying new FieldOrder values.
    /// Port of the sortFields branch of <c>admin_dataviewfields.asp</c>.
    /// </summary>
    Task<AdminDataviewfieldsResultDto> SortFieldsAsync(
        int viewId, List<SortFieldOrderDto> sortOrders, CancellationToken ct = default);

    /// <summary>
    /// Auto-initializes DataViewFields by introspecting the source table's columns.
    /// Port of the autoinit branch of <c>admin_dataviewfields.asp</c>.
    /// </summary>
    Task AutoInitDataViewFieldsAsync(int viewId);

    /// <summary>The DataViewFieldTypes lookup rows, for the dropdown that binds to them.</summary>
    Task<List<DataViewFieldTypesDto>> GetDataViewFieldTypesAsync(CancellationToken ct = default);

    /// <summary>The DataViewFieldFlags lookup rows, for the dropdown that binds to them.</summary>
    Task<List<DataViewFieldFlagsDto>> GetDataViewFieldFlagsAsync(CancellationToken ct = default);

    /// <summary>The DataViewUriStyles lookup rows, for the dropdown that binds to them.</summary>
    Task<List<DataViewUriStylesDto>> GetDataViewUriStylesAsync(CancellationToken ct = default);

    /// <summary>The view's fields as the admin list shows them, in configured order.</summary>
    Task<List<DataViewFieldListItemDto>> GetDataViewFieldsListAsync(int viewId, CancellationToken ct = default);

    /// <summary>
    /// The column names of a table, primary key included. Empty when the table cannot be read.
    /// </summary>
    /// <remarks>
    /// The same introspection Auto-Initialize uses to propose fields, exposed so that anything
    /// needing to check a column name against reality uses one implementation of the question.
    /// </remarks>
    Task<IReadOnlyList<string>> GetTableColumnsAsync(
        string table, string primaryKey, CancellationToken ct = default);

    /// <summary>
    /// Adds a column to the table a view edits, typed from the field that needs it. Returns the
    /// error to show, or null on success.
    /// </summary>
    /// <remarks>
    /// <para>The one place this application issues DDL, and a deliberate divergence: neither the
    /// legacy nor this port has ever altered a table. It exists because defining a field and adding
    /// its column are one intention split across two tools, and the split is where the mistakes
    /// happen — a field is saved, the column is forgotten, and the failure surfaces days later on an
    /// unrelated screen.</para>
    /// <para>Deliberately explicit: nothing calls this as part of saving a field. Someone has to ask
    /// for it, having been told the column is missing.</para>
    /// </remarks>
    Task<string?> AddColumnToViewTableAsync(
        int viewId, string columnName, string fieldType, CancellationToken ct = default);
}
