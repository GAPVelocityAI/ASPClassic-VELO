using ASPClassic.Application.DTOs.Data;
using ASPClassic.Application.DTOs.Inc;
using ASPClassic.Domain.Entities.Data;
using ASPClassic.Application.Services.Data;

namespace ASPClassic.Application.Services.Inc;

/// <summary>Port of <c>Inc_Crudeconstants</c> module from inc_crudeconstants.asp.</summary>
public interface IIncCrudeconstantsService
{
    /// <summary>
    /// Loads all DataViewField records for the given ViewID, ordered by FieldOrder.
    /// Port of <c>InitDataViewFields</c>.
    /// </summary>
    Task InitDataViewFieldsAsync(string pViewID, string pDBConnection, CancellationToken ct = default);

    /// <summary>
    /// Loads all DataViewAction records for the given ViewID, optionally filtering to inline-only.
    /// Port of <c>InitDataViewActions</c>.
    /// </summary>
    Task InitDataViewActionsAsync(string pViewID, string pIsInline, string pDBConnection, CancellationToken ct = default);

    /// <summary>
    /// Transforms a serialized field collection key into structured column definitions.
    /// Port of <c>InitDataViewFieldsJS</c>.
    /// </summary>
    Task InitDataViewFieldsJSAsync(string dvFields, CancellationToken ct = default);

    /// <summary>
    /// Transforms a serialized action collection key into structured inline action button definitions.
    /// Port of <c>InitDataViewInlineActionButtonsJS</c>.
    /// </summary>
    Task InitDataViewInlineActionButtonsJSAsync(string dvActionsInline, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a single DataViewField by FieldID (first available).
    /// Port of legacy getter.
    /// </summary>
    Task<DataViewFieldDto?> GetDataViewFieldAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves a single DataViewAction by ActionID (first available).
    /// Port of legacy getter.
    /// </summary>
    Task<DataViewActionDto?> GetDataViewActionAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves the first DataViewFlags record.
    /// Port of legacy getter.
    /// </summary>
    Task<DataViewFlagsDto?> GetDataViewFlagsAsync(CancellationToken ct = default);

    /// <summary>
    /// Orchestrates full constants loading for a page — loads flags, breadcrumb state.
    /// Port of top-level inc_crudeconstants.asp script block.
    /// </summary>
    Task<DataViewFlagsDto?> LoadIncCrudeconstantsAsync(IncCrudeconstantsStateDto state, CancellationToken ct = default);

    /// <summary>
    /// Returns the upper bound (count - 1) of the currently loaded fields collection.
    /// Port of <c>DataViewLookupClassExtendable.UBound</c>.
    /// </summary>
    Task<string?> UBoundAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns all items in the currently loaded collection.
    /// Port of <c>DataViewLookupClassExtendable.Items</c>.
    /// </summary>
    Task ItemsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets a named property value from the lookup dictionary by key.
    /// Port of <c>DataViewLookupClassExtendable.GetProp</c>.
    /// </summary>
    Task<string?> GetPropAsync(string pKey, CancellationToken ct = default);

    /// <summary>
    /// Sets a named property value in the lookup dictionary by key.
    /// Port of <c>DataViewLookupClassExtendable.SetProp</c>.
    /// </summary>
    Task SetPropAsync(string pKey, string pValue, CancellationToken ct = default);

    /// <summary>
    /// Returns the loaded field collection for direct access by callers.
    /// </summary>
    DataViewFieldCollectionDto GetCurrentFieldCollection();

    /// <summary>
    /// Returns the loaded action collection for direct access by callers.
    /// </summary>
    DataViewActionCollectionDto GetCurrentActionCollection();
}
