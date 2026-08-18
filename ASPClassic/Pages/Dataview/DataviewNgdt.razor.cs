using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;
using ASPClassic.Application.DTOs.Data;
using ASPClassic.Application.DTOs.Dataview;
using ASPClassic.Application.Services.Dataview;
using ASPClassic.Application.Services.Ajax;
using ASPClassic.Application.Services.Inc;
using ASPClassic.Application.Services.Data;
using ASPClassic.Infrastructure;
using ASPClassic.Shared.Dialogs;
using ASPClassic.Pages.Dataview;
using ASPClassic.Domain.Entities.Data;
using ASPClassic.Pages.Default;

namespace ASPClassic.Pages.Dataview;

/// <summary>Port of <c>dataview_ngdt.asp</c> (Dataview_Ngdt — dynamic DataView CRUD with AngularJS-style data table).</summary>
public partial class DataviewNgdt : IDisposable
{
    [Inject] private IDataviewNgdtService DataviewNgdtService { get; set; } = default!;
    [Inject] private IAjaxDataview AjaxDataviewService { get; set; } = default!;
    [Inject] private IDataviewService DataviewService { get; set; } = default!;
    [Inject] private IIncConfigService IncConfigService { get; set; } = default!;
    [Inject] private IIncCrudeconstantsService IncCrudeconstantsService { get; set; } = default!;
    [Inject] private IIncFunctionsService IncFunctionsService { get; set; } = default!;
    [Inject] private ISanitizerClass SanitizerClass { get; set; } = default!;
    [Inject] private IDataViewLookupClassExtendable DataViewLookupClassExtendable { get; set; } = default!;
    [Inject] private IDataViewLookupCollectionClass DataViewLookupCollectionClass { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private AppState AppState { get; set; } = default!;
    [Inject] private ILogger<DataviewNgdt> Logger { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "ViewID")]
    public string? QueryViewID { get; set; }

    [SupplyParameterFromQuery(Name = "ItemID")]
    public string? QueryItemID { get; set; }

    [SupplyParameterFromQuery(Name = "mode")]
    public string? QueryMode { get; set; }

    [SupplyParameterFromQuery(Name = "MSG")]
    public string? QueryMsg { get; set; }

    // Page state
    private string _pageTitle = "Data View";
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;
    private string _dataViewDescription = string.Empty;
    private bool _isLoading = true;

    // DataView metadata
    private DataViewDto? _dataView;
    private List<DataViewFieldDto> _allFields = new();
    private List<DataViewFieldDto>? _visibleFields;
    private List<Dictionary<string, object?>>? _gridRows;

    // View flags (derived from DataView.Flags bitmask)
    private bool _allowUpdate;
    private bool _allowInsert;
    private bool _allowDelete;
    private bool _allowClone;
    private bool _showRowActions;
    private bool _showForm;
    private bool _showList;
    private bool _allowSearch;
    private bool _rteEnabled;
    private bool _showCharts;

    // DataTable flags (derived from DataView.DataTableFlags bitmask)
    private bool _dtInfo;
    private bool _dtColumnFooter;
    private bool _dtQuickSearch;
    private bool _dtSort;
    private bool _dtPagination;
    private bool _dtPageSizeSelection;
    private bool _dtStateSave;
    private int _dtDefaultPageSize = 25;
    private short _dtModBtnStyle;
    private string _dtPagingStyle = string.Empty;

    // Primary key and table info for CRUD
    private string _primaryKey = string.Empty;
    private string _mainTableName = string.Empty;
    private string _modificationProcedure = string.Empty;
    private string _deleteProcedure = string.Empty;
    private string _viewProcedure = string.Empty;
    private string _orderBy = string.Empty;
    private string _dataSource = "Default";
    private int _viewId;

    /// <summary>The field whose metadata the edit modal is showing; 0 until a row is chosen.</summary>
    private int _fieldId;

    protected override void OnInitialized() => AppState.OnCommand += HandleAppCommand;

    private string? _loadedFor;

    /// <summary>
    /// Blazor keeps the SAME component instance when only the query string changes, so
    /// OnInitializedAsync does not run again — only this does. Loading from initialization alone
    /// leaves the address bar showing one record and the page showing another, with nothing to
    /// suggest anything went wrong.
    /// </summary>
    /// <remarks>
    /// Guarded on the parameters themselves: OnParametersSetAsync runs on every render pass, and
    /// reloading unconditionally would re-query the database on each one.
    /// </remarks>
    protected override async Task OnParametersSetAsync()
    {
        var signature = $"{QueryViewID}|{QueryMode}|{QueryItemID}|{QueryMsg}";
        if (signature == _loadedFor) return;
        _loadedFor = signature;

        // Show success message from redirect after save/delete
        if (!string.IsNullOrEmpty(QueryMsg))
        {
            _successMessage = QueryMsg switch
            {
                "add" => "Record added successfully.",
                "edit" => "Record updated successfully.",
                "delete" => "Record deleted successfully.",
                _ => $"Operation '{QueryMsg}' completed."
            };
        }

        await LoadDataviewNgdt(QueryItemID ?? string.Empty, QueryMode ?? "none", QueryViewID ?? string.Empty, string.Empty);
    }

    private async Task HandleAppCommand(string command)
    {
        switch (command)
        {
            case "refresh":
                await OnRefreshClick();
                break;
        }
    }

    /// <summary>Port of <c>LoadDataviewNgdt</c> — loads the DataView definition, fields, and grid data.</summary>
    private async Task LoadDataviewNgdt(string itemID, string mode, string viewID, string postback)
    {
        _isLoading = true;
        _errorMessage = string.Empty;

        try
        {
            if (string.IsNullOrEmpty(viewID) || !int.TryParse(viewID, NumberStyles.Integer, CultureInfo.InvariantCulture, out _viewId))
            {
                _errorMessage = "ViewID Invalid!";
                return;
            }

            // Load DataView definition
            var dataViewInfo = await DataviewService.GetDataViewInfoAsync(_viewId);
            if (dataViewInfo == null)
            {
                _errorMessage = "ViewID Not Found!";
                return;
            }

            _dataView = dataViewInfo;
            _pageTitle = dataViewInfo.Title ?? "Data View";
            _dataViewDescription = dataViewInfo.ViewDescription ?? string.Empty;
            _viewProcedure = dataViewInfo.ViewProcedure ?? string.Empty;
            _modificationProcedure = dataViewInfo.ModificationProcedure ?? string.Empty;
            _deleteProcedure = dataViewInfo.DeleteProcedure ?? string.Empty;
            _mainTableName = dataViewInfo.MainTable ?? string.Empty;
            _orderBy = dataViewInfo.OrderBy ?? string.Empty;
            _primaryKey = dataViewInfo.Primarykey ?? string.Empty;
            _dataSource = dataViewInfo.DataSource ?? "Default";
            _dtModBtnStyle = dataViewInfo.DataTableModifierButtonStyle;
            _dtDefaultPageSize = dataViewInfo.DataTableDefaultPageSize > 0 ? dataViewInfo.DataTableDefaultPageSize : 25;
            _dtPagingStyle = dataViewInfo.DataTablePagingStyle ?? string.Empty;

            // Decode view flags bitmask (nViewFlags = DataView.Flags)
            int nViewFlags = dataViewInfo.Flags;
            _allowUpdate = (nViewFlags & 1) > 0;
            _allowInsert = (nViewFlags & 2) > 0;
            _allowDelete = (nViewFlags & 4) > 0;
            _allowClone = (nViewFlags & 8) > 0;
            _showRowActions = _allowUpdate || _allowDelete || _allowClone;
            _showForm = (nViewFlags & 16) > 0;
            _showList = (nViewFlags & 32) > 0;
            _allowSearch = (nViewFlags & 64) > 0;
            _rteEnabled = (nViewFlags & 128) > 0;
            _showCharts = (nViewFlags & 256) > 0;

            // Decode DataTable flags bitmask
            int nDtFlags = dataViewInfo.DataTableFlags;
            _dtInfo = (nDtFlags & 1) > 0;
            _dtColumnFooter = (nDtFlags & 2) > 0;
            _dtQuickSearch = (nDtFlags & 4) > 0;
            _dtSort = (nDtFlags & 8) > 0;
            _dtPagination = (nDtFlags & 16) > 0;
            _dtPageSizeSelection = (nDtFlags & 32) > 0;
            _dtStateSave = (nDtFlags & 64) > 0;

            // Load fields and grid data
            await LoadFieldsAndData();

            // Handle data manipulation if mode is add/edit/delete and postback
            if (!string.IsNullOrEmpty(mode) && mode != "none")
            {
                if ((mode == "add" && _allowInsert) || (mode == "edit" && _allowUpdate))
                {
                    // Postback was handled — the legacy code redirects after success.
                    // In Blazor, the service handles the save; we just refresh.
                }
                else if (mode == "delete" && _allowDelete && !string.IsNullOrEmpty(itemID))
                {
                    // Deletion handled by service on postback.
                }
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading DataView: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>Port of <c>GetDataViewField</c> — retrieves the DataViewField metadata for the current view.</summary>
    private async Task<DataViewFieldDto?> GetDataViewField()
    {
        return await DataviewNgdtService.GetDataViewFieldAsync(_fieldId);
    }

    /// <summary>Port of <c>GetDataView</c> — retrieves the DataView metadata for the current view.</summary>
    private async Task<DataViewDto?> GetDataView()
    {
        return await DataviewNgdtService.GetDataViewAsync(_viewId);
    }

    private async Task LoadFieldsAndData()
    {
        // Build the field list from the DataView by querying DataViewFields.
        await LoadAllFieldsFromService();

        // Filter visible fields (those with "Show in List" flag = bit 8)
        _visibleFields = _allFields
            .Where(f => (f.FieldFlags & 8) > 0)
            .OrderBy(f => f.FieldOrder)
            .ToList();

        // Load grid data via AJAX service (mirrors the ng-repeat over dataviewContents.data)
        await LoadGridData();
    }

    /// <summary>Loads the field definitions this view declares.</summary>
    /// <remarks>
    /// What stood here made four service calls, used none of their results, and left a comment
    /// saying "the fields should now be initialized in the service state" — they were not, so
    /// <c>_allFields</c> stayed empty and the grid rendered no columns at all. The accessor that
    /// answers this question already existed.
    /// </remarks>
    private async Task LoadAllFieldsFromService()
    {
        try
        {
            _allFields = await DataviewService.GetDataViewFieldsAsync(_viewId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not load the fields of view {ViewId}", _viewId);
            _allFields = new List<DataViewFieldDto>();
        }
    }

    /// <summary>Loads the rows the grid shows.</summary>
    /// <remarks>
    /// The previous version asked for the view DEFINITION, discarded it, and assigned an empty list
    /// with a comment promising that "the real data comes from the query engine" — which nothing
    /// called. Every NGDT grid showed no rows, whatever the table held.
    /// </remarks>
    private async Task LoadGridData()
    {
        try
        {
            var result = await DataviewService.GetDataViewRowsAsync(
                _viewId, start: 0, length: _dtDefaultPageSize > 0 ? _dtDefaultPageSize : 100,
                searchValue: string.Empty);

            if (!string.IsNullOrEmpty(result?.Error))
            {
                Logger.LogWarning("Grid query for view {ViewId} failed: {Error}", _viewId, result.Error);
                _gridRows = new List<Dictionary<string, object?>>();
                return;
            }

            _gridRows = (result?.Data ?? new List<Dictionary<string, string>>())
                .Select(row => row.ToDictionary(
                    kv => kv.Key, kv => (object?)kv.Value, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not load grid data for view {ViewId}", _viewId);
            _gridRows = new List<Dictionary<string, object?>>();
        }
    }

    private async Task OnAddClick()
    {
        if (!_allowInsert) return;

        var parameters = new DialogParameters<DataviewNgdtEditDialog>
        {
            { x => x.ViewId, _viewId },
            { x => x.Mode, "add" },
            { x => x.ItemId, string.Empty },
            { x => x.Fields, _allFields.Where(f => (f.FieldFlags & 1) > 0).OrderBy(f => f.FieldOrder).ToList() },
            { x => x.AllowDelete, _allowDelete },
            { x => x.DataSource, _dataSource },
            { x => x.MainTableName, _mainTableName },
            { x => x.PrimaryKey, _primaryKey },
            { x => x.ModificationProcedure, _modificationProcedure },
            { x => x.PageTitle, _pageTitle }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseOnEscapeKey = true
        };

        var dialog = await DialogService.ShowAsync<DataviewNgdtEditDialog>(
            $"Add - {_pageTitle}", parameters, options);
        var dialogResult = await dialog.Result;

        if (dialogResult != null && !dialogResult.Canceled)
        {
            Snackbar.Add("Record added successfully.", Severity.Success);
            await OnRefreshClick();
        }
    }

    private async Task OnEditClick(Dictionary<string, object?> row)
    {
        if (!_allowUpdate) return;

        string itemId = string.Empty;
        if (row.TryGetValue("_ItemID", out var idVal) && idVal != null)
        {
            itemId = idVal.ToString() ?? string.Empty;
        }
        else if (!string.IsNullOrEmpty(_primaryKey) && row.TryGetValue(_primaryKey, out var pkVal) && pkVal != null)
        {
            itemId = pkVal.ToString() ?? string.Empty;
        }

        var parameters = new DialogParameters<DataviewNgdtEditDialog>
        {
            { x => x.ViewId, _viewId },
            { x => x.Mode, "edit" },
            { x => x.ItemId, itemId },
            { x => x.Fields, _allFields.Where(f => (f.FieldFlags & 1) > 0).OrderBy(f => f.FieldOrder).ToList() },
            { x => x.RowData, row },
            { x => x.AllowDelete, _allowDelete },
            { x => x.DataSource, _dataSource },
            { x => x.MainTableName, _mainTableName },
            { x => x.PrimaryKey, _primaryKey },
            { x => x.ModificationProcedure, _modificationProcedure },
            { x => x.PageTitle, _pageTitle }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseOnEscapeKey = true
        };

        var dialog = await DialogService.ShowAsync<DataviewNgdtEditDialog>(
            $"Edit - {_pageTitle}", parameters, options);
        var dialogResult = await dialog.Result;

        if (dialogResult != null && !dialogResult.Canceled)
        {
            Snackbar.Add("Record updated successfully.", Severity.Success);
            await OnRefreshClick();
        }
    }

    private async Task OnCloneClick(Dictionary<string, object?> row)
    {
        if (!_allowClone) return;

        // Clone uses "add" mode but pre-populates the form with existing row data
        var parameters = new DialogParameters<DataviewNgdtEditDialog>
        {
            { x => x.ViewId, _viewId },
            { x => x.Mode, "add" },
            { x => x.ItemId, string.Empty },
            { x => x.Fields, _allFields.Where(f => (f.FieldFlags & 1) > 0).OrderBy(f => f.FieldOrder).ToList() },
            { x => x.RowData, row },
            { x => x.AllowDelete, false },
            { x => x.DataSource, _dataSource },
            { x => x.MainTableName, _mainTableName },
            { x => x.PrimaryKey, _primaryKey },
            { x => x.ModificationProcedure, _modificationProcedure },
            { x => x.PageTitle, _pageTitle }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseOnEscapeKey = true
        };

        var dialog = await DialogService.ShowAsync<DataviewNgdtEditDialog>(
            $"Clone - {_pageTitle}", parameters, options);
        var dialogResult = await dialog.Result;

        if (dialogResult != null && !dialogResult.Canceled)
        {
            Snackbar.Add("Record cloned successfully.", Severity.Success);
            await OnRefreshClick();
        }
    }

    private async Task OnDeleteClick(Dictionary<string, object?> row)
    {
        if (!_allowDelete) return;

        string itemId = string.Empty;
        if (row.TryGetValue("_ItemID", out var idVal) && idVal != null)
        {
            itemId = idVal.ToString() ?? string.Empty;
        }
        else if (!string.IsNullOrEmpty(_primaryKey) && row.TryGetValue(_primaryKey, out var pkVal) && pkVal != null)
        {
            itemId = pkVal.ToString() ?? string.Empty;
        }

        // Build summary of the row for the delete confirmation dialog
        // Legacy shows fields with flag bit 8 (show in list)
        var summaryFields = _allFields
            .Where(f => (f.FieldFlags & 8) > 0)
            .OrderBy(f => f.FieldOrder)
            .Select(f =>
            {
                var displayValue = string.Empty;
                if (row.TryGetValue(f.FieldLabel, out var val) && val != null)
                {
                    displayValue = val.ToString() ?? string.Empty;
                }
                return new KeyValuePair<string, string>(f.FieldLabel, displayValue);
            })
            .ToList();

        var parameters = new DialogParameters<ConfirmDeleteDialog>
        {
            { x => x.ContentText, "Are you sure you want to delete this item?" }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        var dialog = await DialogService.ShowAsync<ConfirmDeleteDialog>(
            "Deleting Item", parameters, options);
        var dialogResult = await dialog.Result;

        if (dialogResult != null && !dialogResult.Canceled)
        {
            try
            {
                // Perform deletion via the service
                await DataviewNgdtService.LoadDataviewNgdtAsync(
                    itemId, "delete", _viewId.ToString(), "true");

                Snackbar.Add("Record deleted successfully.", Severity.Success);
                await OnRefreshClick();
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Error deleting record: {ex.Message}", Severity.Error);
            }
        }
    }

    private async Task OnRefreshClick()
    {
        await LoadDataviewNgdt(string.Empty, "none", _viewId.ToString(), string.Empty);
        await InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        AppState.OnCommand -= HandleAppCommand;
    }
}
