using ASPClassic.Application.DTOs.Admin;
using ASPClassic.Application.DTOs.Data;
using ASPClassic.Domain.Entities.Data;

namespace ASPClassic.Application.Services.Admin;

/// <summary>Port of <c>admin_dataviews.asp</c> — manage Data Views CRUD.</summary>
public interface IAdminDataviewsService
{
    /// <summary>Port of <c>GetDataView</c> — retrieves a single DataView by ID.</summary>
    Task<DataViewDto?> GetDataViewAsync(CancellationToken ct = default);

    /// <summary>
    /// One data view, by id.
    /// </summary>
    /// <remarks>
    /// Distinct from the parameterless overload, which returns the FIRST view ordered by ViewID —
    /// always the same one. Using that to populate an edit form loads the wrong view and saving
    /// then copies it over the row being edited.
    /// </remarks>
    Task<DataViewDto?> GetDataViewByIdAsync(int viewId, CancellationToken ct = default);

    /// <summary>
    /// Port of <c>LoadAdminDataviews</c> — handles add/edit/delete modes for DataViews.
    /// Returns a DataViewDto for the loaded/saved view, or null on error/listing mode.
    /// </summary>
    Task<DataViewDto?> LoadAdminDataviewsAsync(
        string mode,
        string itemID,
        string title,
        string dataSource,
        string published,
        string mainTable,
        string primaryKey,
        string modificationProcedure,
        CancellationToken ct = default);

    /// <summary>
    /// Port of <c>usp_Generate_Merge_For_Table</c> — generates MERGE SQL for a given table.
    /// Note: This calls a stored procedure. In SQLite mode this produces a simplified INSERT OR REPLACE equivalent.
    /// </summary>
    Task GenerateMergeForTableAsync(
        string currTable,
        string currSchema,
        bool deleteUnmatchedRows,
        bool updateExistingRows,
        bool insertNewRows,
        bool debugMode,
        bool includeTimestamp,
        bool ommitComputedCols,
        string topClause);

    /// <summary>Retrieves all DataViews ordered by Title for the admin listing grid.</summary>
    Task<List<DataViewDto>> GetAllDataViewsAsync(CancellationToken ct = default);

    /// <summary>Port of delete mode — deletes a DataView and its child DataViewFields.</summary>
    Task<AdminDataviewsSaveResultDto> DeleteDataViewAsync(int viewId, CancellationToken ct = default);

    /// <summary>Port of <c>DeleteDataViewField</c> — deletes a DataViewField by ID.</summary>
    Task<bool> DeleteDataViewFieldAsync(int fieldId, CancellationToken ct = default);

    /// <summary>
    /// Extended overload of LoadAdminDataviewsAsync that carries all form fields
    /// including view/delete procedures, descriptions, flags, and paging settings.
    /// Returns a result with success/error info, redirect target, and optional loaded DataView for edit forms.
    /// </summary>
    Task<AdminDataviewsSaveResultDto> LoadAdminDataviewsExtendedAsync(
        string mode,
        string itemID,
        string title,
        string dataSource,
        string published,
        string mainTable,
        string primaryKey,
        string modificationProcedure,
        string viewProcedure,
        string deleteProcedure,
        string viewDescription,
        string orderBy,
        string rowReorderColumn,
        string dataTableModifierButtonStyle,
        string dataTableDefaultPageSize,
        string dataTablePagingStyle,
        List<int> flagValues,
        List<int> dataTableFlagValues,
        CancellationToken ct = default);

    /// <summary>
    /// Extended overload that returns a GenerateMergeResultDto with the generated SQL or error info.
    /// </summary>
    Task<GenerateMergeResultDto> GenerateMergeForTableExtendedAsync(
        string currTable,
        string currSchema,
        bool deleteUnmatchedRows,
        bool updateExistingRows,
        bool insertNewRows,
        bool debugMode,
        bool includeTimestamp,
        bool ommitComputedCols,
        string topClause,
        CancellationToken ct = default);

    /// <summary>The DataViewFlags lookup rows, for the dropdown that binds to them.</summary>
    Task<List<DataViewFlagsDto>> GetDataViewFlagsAsync(CancellationToken ct = default);

    /// <summary>The DataViewDataTableFlags lookup rows, for the dropdown that binds to them.</summary>
    Task<List<DataViewDataTableFlagsDto>> GetDataViewDataTableFlagsAsync(CancellationToken ct = default);

    /// <summary>The DataViewModifierButtonStyles lookup rows, for the dropdown that binds to them.</summary>
    Task<List<DataViewModifierButtonStylesDto>> GetDataViewModifierButtonStylesAsync(CancellationToken ct = default);

    /// <summary>The DataViewPagingTypes lookup rows, for the dropdown that binds to them.</summary>
    Task<List<DataViewPagingTypesDto>> GetDataViewPagingTypesAsync(CancellationToken ct = default);

    /// <summary>
    /// Duplicates a data view and the fields that describe it. Returns the new ViewID, or null.
    /// </summary>
    /// <remarks>
    /// The fields are copied too. A view is a screen over a table, and its field rows are what make
    /// it renderable — duplicating the view alone produces something that looks cloned and shows
    /// nothing.
    /// </remarks>
    Task<int?> CloneDataViewAsync(int viewId, CancellationToken ct = default);
}
