using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MudBlazor;
using ASPClassic.Application.DTOs.Data;
using ASPClassic.Application.DTOs.Dataview;
using ASPClassic.Application.Services.Dataview;
using ASPClassic.Application.Services.Ajax;
using ASPClassic.Application.Services.Inc;
using ASPClassic.Application.Services.Data;
using ASPClassic.Infrastructure;
using ASPClassic.Infrastructure.Data;
using ASPClassic.Shared.Dialogs;
using ASPClassic.Domain.Entities.Data;
using ASPClassic.Infrastructure.Engines;

namespace ASPClassic.Pages.Dataview;

/// <summary>Port of <c>dataview.asp</c> (Dataview form).</summary>
public partial class Dataview : IDisposable
{
    [Inject] private IDataviewService DataviewService { get; set; } = default!;
    [Inject] private IAjaxDataview AjaxDataviewService { get; set; } = default!;
    [Inject] private IIncConfigService IncConfigService { get; set; } = default!;
    [Inject] private IIncCrudeconstantsService IncCrudeconstantsService { get; set; } = default!;
    [Inject] private IIncFunctionsService IncFunctionsService { get; set; } = default!;
    [Inject] private ISanitizerClass SanitizerClass { get; set; } = default!;
    [Inject] private IDataViewLookupClassExtendable DataViewLookupClassExtendable { get; set; } = default!;
    [Inject] private IDataViewLookupCollectionClass DataViewLookupCollectionClass { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<Dataview> Logger { get; set; } = default!;
    [Inject] private AppState AppState { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ASPClassic.Application.Services.Admin.IAdminDataviewfieldsService AdminDataviewfieldsService { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "ViewID")]
    public string? ViewIdParam { get; set; }

    [SupplyParameterFromQuery(Name = "DT_ItemId")]
    public string? DtItemIdParam { get; set; }

    [SupplyParameterFromQuery(Name = "mode")]
    public string? ModeParam { get; set; }

    /// <summary>
    /// The per-column filters the address carries, keyed by database column.
    /// </summary>
    /// <remarks>
    /// This is how the portal narrows a shared screen to one parent. "Data View Fields" lists every
    /// field of every view; the Fields button on a view's row opens it with
    /// <c>dataview[search]=&lt;that view's id&gt;</c>, and the grid shows only its fields.
    /// <para>The parameter is named for the FIELD IDENTIFIER, not the column — the identifier is
    /// the client-side handle for a field, and the column it maps to is FieldSource. The Data View
    /// column of the field list carries the identifier "dataview", which is where that parameter
    /// name comes from.</para>
    /// </remarks>
    private Dictionary<string, string> _columnFilters = new(StringComparer.OrdinalIgnoreCase);

    // Page state
    private string _pageTitle = "DataTable View";
    private string? _errorMessage;
    private bool _loading = true;
    private DataViewDto? _viewData;
    private int _viewId;
    private string _mode = "none";
    private string _dtItemId = string.Empty;

    // Flag-decoded booleans (from Flags column — nViewFlags)
    private bool _allowUpdate;
    private bool _allowInsert;
    private bool _allowDelete;
    private bool _allowClone;
    private bool _showForm;
    private bool _showList;
    private bool _showCharts;
    private bool _showCustomActions;
    private bool _browseMode;

    // Flag-decoded booleans (from DataTableFlags — nDtFlags)
    private bool _dtInfo;
    private bool _dtColumnFooter;
    private bool _dtQuickSearch;
    private bool _dtSort;
    private bool _dtPagination;
    private bool _dtPageSizeSelection;
    private bool _dtStateSave;
    private bool _allowSearch;
    private bool _allowColumnsToggle;
    private bool _allowRowDetails;
    private bool _allowRowSelection;
    private bool _exportClipboard;
    private bool _exportCsv;
    private bool _exportExcel;
    private bool _exportPdf;
    private bool _exportPrint;
    private bool _allowExport;
    private bool _fixedHeaders;
    private bool _showRowActions;

    // Row reorder
    private bool _hasRowReorder;
    private string _rowReorderColMasked = string.Empty;

    // Grid data
    private List<Dictionary<string, object?>> _gridData = new();
    private HashSet<Dictionary<string, object?>> _selectedItems = new();
    private List<DataViewFieldDto> _visibleFields = new();
    private List<DataViewActionDto> _toolbarActions = new();
    private List<DataViewActionDto> _inlineActions = new();
    private HashSet<int> _hiddenColumnIds = new();

    // Search
    private string? _searchText;

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
        var signature = $"{ViewIdParam}|{ModeParam}|{DtItemIdParam}|{new Uri(NavigationManager.Uri).Query}";
        if (signature == _loadedFor) return;
        _loadedFor = signature;

        _loading = true;
        _errorMessage = null;

        _dtItemId = DtItemIdParam ?? string.Empty;
        _mode = string.IsNullOrEmpty(ModeParam) ? "none" : ModeParam;

        if (!string.IsNullOrEmpty(ViewIdParam) && int.TryParse(ViewIdParam, NumberStyles.Integer, CultureInfo.InvariantCulture, out var vid))
        {
            _viewId = vid;
        }
        else
        {
            _errorMessage = "ViewID Invalid!";
            _loading = false;
            return;
        }

        await LoadDataviewAsync(_dtItemId, _mode, ViewIdParam!);
    }

    /// <summary>Port of <c>LoadDataview</c> — loads the DataView definition and grid data.</summary>
    private async Task LoadDataviewAsync(string dTItemId, string mode, string viewID)
    {
        _loading = true;
        _errorMessage = null;

        try
        {
            var viewInfo = await DataviewService.GetDataViewInfoAsync(_viewId);

            if (viewInfo is null)
            {
                _errorMessage = "ViewID Not Found!";
                _loading = false;
                return;
            }

            _viewData = viewInfo;

            if (!_viewData.Published)
            {
                // Legacy: Response.Redirect "404.asp?msg=viewnotfound"
                NavigationManager.NavigateTo("/aspclassic-vbscript/page404?msg=viewnotfound", replace: true);
                return;
            }

            _pageTitle = _viewData.Title ?? "DataTable View";

            // Decode Flags (nViewFlags)
            int nViewFlags = _viewData.Flags;
            _allowUpdate = (nViewFlags & 1) > 0;
            _allowInsert = (nViewFlags & 2) > 0;
            _allowDelete = (nViewFlags & 4) > 0;
            _allowClone = (nViewFlags & 8) > 0;
            _showForm = (nViewFlags & 16) > 0;
            _showList = (nViewFlags & 32) > 0;
            _showCharts = (nViewFlags & 64) > 0;
            _showCustomActions = (nViewFlags & 128) > 0;
            _browseMode = (nViewFlags & 256) > 0;

            // Decode DataTableFlags (nDtFlags)
            int nDtFlags = _viewData.DataTableFlags;
            _dtInfo = (nDtFlags & 1) > 0;
            _dtColumnFooter = (nDtFlags & 2) > 0;
            _dtQuickSearch = (nDtFlags & 4) > 0;
            _dtSort = (nDtFlags & 8) > 0;
            _dtPagination = (nDtFlags & 16) > 0;
            _dtPageSizeSelection = (nDtFlags & 32) > 0;
            _dtStateSave = (nDtFlags & 64) > 0;
            _allowSearch = (nDtFlags & 128) > 0;
            _allowColumnsToggle = (nDtFlags & 256) > 0;
            _allowRowDetails = (nDtFlags & 512) > 0;
            _allowRowSelection = (nDtFlags & 1024) > 0;
            _exportClipboard = (nDtFlags & 2048) > 0;
            _exportCsv = (nDtFlags & 4096) > 0;
            _exportExcel = (nDtFlags & 8192) > 0;
            _exportPdf = (nDtFlags & 16384) > 0;
            _exportPrint = (nDtFlags & 32768) > 0;
            _fixedHeaders = (nDtFlags & 65536) > 0;

            _allowExport = _exportClipboard || _exportCsv || _exportExcel || _exportPdf || _exportPrint;

            // Row reorder
            string rowReorderCol = _viewData.RowReorderColumn ?? string.Empty;
            _hasRowReorder = !string.IsNullOrEmpty(rowReorderCol);

            // Load fields from service
            _visibleFields = await DataviewService.GetDataViewFieldsAsync(_viewId);

            ReadColumnFiltersFromUrl();

            // Determine rowReorderColMasked (port of legacy loop over dvFields)
            _rowReorderColMasked = string.Empty;
            if (_hasRowReorder)
            {
                foreach (var f in _visibleFields)
                {
                    if (string.Equals(rowReorderCol, f.FieldSource, StringComparison.OrdinalIgnoreCase)
                        && string.IsNullOrEmpty(_rowReorderColMasked))
                    {
                        // Keyed by column, for the same reason the cells are.
                        _rowReorderColMasked = !string.IsNullOrWhiteSpace(f.FieldSource)
                            ? f.FieldSource
                            : string.Empty;
                    }
                }
                if (string.IsNullOrEmpty(_rowReorderColMasked))
                {
                    _rowReorderColMasked = rowReorderCol;
                }
            }

            // The column SET is the legacy's `AND 9` test — a field earns a column if it appears in
            // the form or in the items list (bit 1 Show in Form, bit 8 Show in Items List).
            _visibleFields = _visibleFields
                .Where(f => (f.FieldFlags & 9) > 0)
                .OrderBy(f => f.FieldOrder)
                .ToList();

            // But a column is only SHOWN when it is in the items list. The legacy emits every column
            // of that set and marks the rest `"visible": false` (inc_crudeconstants.asp), which is
            // what the columns-visibility toggle then reveals. Rendering them all instead turns a
            // four-column grid into a seventeen-column one and buries the columns that matter.
            _hiddenColumnIds = _visibleFields
                .Where(f => (f.FieldFlags & 8) == 0)
                .Select(f => f.FieldID)
                .ToHashSet();

            _showRowActions = _showCustomActions || _allowUpdate || _allowDelete || _allowClone
                              || _hasRowReorder || _allowRowDetails;

            // Load actions (inline and toolbar)
            _inlineActions = await DataviewService.GetDataViewActionsAsync(_viewId, isPerRow: true);
            _toolbarActions = await DataviewService.GetDataViewActionsAsync(_viewId, isPerRow: false);

            // Load grid data
            await LoadGridDataAsync();
        }
        // Blazor signals a redirect by throwing NavigationException; catching
        // everything turns that redirect into an error and the page stays put.
        catch (Exception ex) when (ex is not NavigationException)
        {
            Logger.LogError(ex, "Error loading DataView {ViewId}", _viewId);
            _errorMessage = $"Error loading DataView: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>Port of <c>GetDataView</c> — returns the current DataView definition.</summary>
    private DataViewDto? GetDataView()
    {
        return _viewData;
    }

    private async Task LoadGridDataAsync()
    {
        try
        {
            var result = await DataviewService.GetDataViewRowsAsync(
                _viewId,
                start: 0,
                length: _viewData?.DataTableDefaultPageSize ?? 50,
                searchValue: _searchText ?? string.Empty,
                columnFilters: _columnFilters);

            if (!string.IsNullOrEmpty(result?.Error))
            {
                // The query ran and failed. Saying so beats an empty grid, which reads as "no data".
                _errorMessage = result.Error;
                _gridData = new List<Dictionary<string, object?>>();
                Logger.LogWarning("Grid query for ViewID={ViewId} failed: {Error}", _viewId, result.Error);
                return;
            }

            // The rows the engine returned. Discarding them and substituting an empty list — which
            // is what stood here — makes every grid in the application show "No records found"
            // however much data there is, and logs a success while doing it.
            _gridData = (result?.Data ?? new List<Dictionary<string, string>>())
                .Select(row => row.ToDictionary(
                    kv => kv.Key,
                    kv => (object?)kv.Value,
                    StringComparer.OrdinalIgnoreCase))
                .ToList();

            Logger.LogInformation("Grid data loaded for ViewID={ViewId}, {Rows} row(s), fields={FieldCount}",
                _viewId, _gridData.Count, _visibleFields.Count);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load grid data for ViewID={ViewId}", _viewId);
            _gridData = new List<Dictionary<string, object?>>();
        }
    }

    // --- Toolbar handlers ---

    /// <summary>Port of respite_crud.addAddButton — opens add/insert dialog.</summary>
    private async Task OnAddClick()
    {
        var parameters = new DialogParameters<AjaxDataviewDialog>
        {
            { x => x.ViewId, _viewId },
            { x => x.Mode, "add" },
            { x => x.ItemId, null }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Large,
            FullWidth = true,
            CloseOnEscapeKey = true
        };

        var dialog = await DialogService.ShowAsync<AjaxDataviewDialog>("Add Record", parameters, options);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            await LoadGridDataAsync();
            Snackbar.Add("Record added successfully.", Severity.Success);
        }
    }

    /// <summary>Port of respite_crud.addRefreshButton.</summary>
    private async Task OnRefreshClick()
    {
        await LoadGridDataAsync();
        Snackbar.Add("Data refreshed.", Severity.Info);
    }

    /// <summary>Port of respite_crud.addSelectAllButton.</summary>
    private void OnSelectAllClick()
    {
        _selectedItems = new HashSet<Dictionary<string, object?>>(_gridData);
    }

    /// <summary>Port of respite_crud.addDeSelectAllButton.</summary>
    private void OnDeselectAllClick()
    {
        _selectedItems.Clear();
    }

    /// <summary>Port of respite_crud.addDeleteSelectedButton — bulk delete.</summary>
    private async Task OnDeleteSelectedClick()
    {
        if (_selectedItems.Count == 0)
        {
            Snackbar.Add("No items selected.", Severity.Warning);
            return;
        }

        var confirmParams = new DialogParameters<ConfirmDeleteDialog>
        {
            { x => x.ContentText, $"Are you sure you want to delete {_selectedItems.Count} selected record(s)?" }
        };

        var dialog = await DialogService.ShowAsync<ConfirmDeleteDialog>("Confirm Delete", confirmParams);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            var deleted = 0;
            var failed = new List<string>();

            foreach (var item in _selectedItems.ToList())
            {
                string? pkValue = GetPrimaryKeyValue(item);

                if (string.IsNullOrEmpty(pkValue))
                {
                    failed.Add("a row with no identifier");
                    continue;
                }

                try
                {
                    var error = await DataviewService.DeleteDataviewRecordAsync(_viewId, pkValue);

                    if (error == null) deleted++;
                    else failed.Add($"{pkValue}: {error}");
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to delete item {PK} in ViewID={ViewId}", pkValue, _viewId);
                    failed.Add($"{pkValue}: {ex.Message}");
                }
            }

            _selectedItems.Clear();
            await LoadGridDataAsync();

            // Reported as it happened. "Selected records deleted" after every one of them failed is
            // worse than no message at all.
            if (failed.Count == 0)
            {
                Snackbar.Add($"{deleted} record(s) deleted.", Severity.Success);
            }
            else
            {
                Snackbar.Add(
                    $"{deleted} deleted, {failed.Count} failed: {string.Join("; ", failed.Take(3))}",
                    deleted > 0 ? Severity.Warning : Severity.Error);
            }
        }
    }

    /// <summary>Port of respite_crud.addExportButton with per-format options.</summary>
    /// <summary>
    /// Downloads the view's rows in the chosen format.
    /// </summary>
    /// <remarks>
    /// What stood here logged the request, announced "Export initiated" and returned — so the menu
    /// worked, the message appeared, and no file was ever produced.
    /// <para>The file comes from an endpoint rather than from the circuit, because a websocket
    /// cannot hand the browser a download. Opened in a new tab so the current page survives; the
    /// response is an attachment, so the tab closes itself.</para>
    /// </remarks>
    private async Task OnExportClick(string format)
    {
        // Only the formats that produce a file here. The rest were client-side DataTables buttons
        // in the legacy, and saying so is better than announcing an export that cannot happen.
        if (format is not ("excel" or "csv"))
        {
            Snackbar.Add(
                $"{format} export is not available — use Excel or CSV.", Severity.Warning);
            return;
        }

        if (_gridData.Count == 0)
        {
            Snackbar.Add("There is nothing to export.", Severity.Warning);
            return;
        }

        // The filters the screen is applying travel with the request, so the file matches what
        // is on screen rather than the whole table.
        var query = new List<string> { $"format={format}" };

        if (!string.IsNullOrWhiteSpace(_searchText))
            query.Add($"search={Uri.EscapeDataString(_searchText)}");

        foreach (var (column, value) in _columnFilters)
            query.Add($"filter.{Uri.EscapeDataString(column)}={Uri.EscapeDataString(value)}");

        var url = $"/aspclassic-vbscript/export/{_viewId}?{string.Join("&", query)}";

        try
        {
            await JS.InvokeVoidAsync("open", url, "_blank");
            Logger.LogInformation("Export of view {ViewId} as {Format} requested.", _viewId, format);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Export failed for ViewID={ViewId}, format={Format}", _viewId, format);
            Snackbar.Add($"Could not start the download: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>Port of respite_crud.addToggleColumnsButton.</summary>
    private void ToggleColumnVisibility(int fieldId)
    {
        if (_hiddenColumnIds.Contains(fieldId))
            _hiddenColumnIds.Remove(fieldId);
        else
            _hiddenColumnIds.Add(fieldId);
    }

    private bool IsColumnVisible(int fieldId)
    {
        return !_hiddenColumnIds.Contains(fieldId);
    }

    // --- Inline row action handlers ---

    /// <summary>Port of respite_crud.addEditButton — opens edit dialog for a row.</summary>
    private async Task OnEditClick(Dictionary<string, object?> row)
    {
        string? pkValue = GetPrimaryKeyValue(row);

        var parameters = new DialogParameters<AjaxDataviewDialog>
        {
            { x => x.ViewId, _viewId },
            { x => x.Mode, "edit" },
            { x => x.ItemId, pkValue }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Large,
            FullWidth = true,
            CloseOnEscapeKey = true
        };

        var dialog = await DialogService.ShowAsync<AjaxDataviewDialog>("Edit Record", parameters, options);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            await LoadGridDataAsync();
            Snackbar.Add("Record updated successfully.", Severity.Success);
        }
    }

    /// <summary>Port of respite_crud.addDeleteButton — confirms and deletes a single row.</summary>
    private async Task OnDeleteClick(Dictionary<string, object?> row)
    {
        string? pkValue = GetPrimaryKeyValue(row);

        var confirmParams = new DialogParameters<ConfirmDeleteDialog>
        {
            { x => x.ContentText, "Are you sure you want to delete this record?" }
        };

        var dialog = await DialogService.ShowAsync<ConfirmDeleteDialog>("Confirm Delete", confirmParams);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            if (string.IsNullOrEmpty(pkValue))
            {
                // Without the key nothing can be deleted safely; guessing would delete some other
                // record.
                Snackbar.Add("This row carries no identifier, so it cannot be deleted.", Severity.Error);
                return;
            }

            try
            {
                var error = await DataviewService.DeleteDataviewRecordAsync(_viewId, pkValue);

                if (error != null)
                {
                    Snackbar.Add(error, Severity.Error);
                    return;
                }

                await LoadGridDataAsync();
                Snackbar.Add("Record deleted.", Severity.Success);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Delete failed for PK={PK}, ViewID={ViewId}", pkValue, _viewId);
                Snackbar.Add($"Delete failed: {ex.Message}", Severity.Error);
            }
        }
    }

    /// <summary>Port of respite_crud.addCloneButton — clones a row.</summary>
    private async Task OnCloneClick(Dictionary<string, object?> row)
    {
        string? pkValue = GetPrimaryKeyValue(row);

        var parameters = new DialogParameters<AjaxDataviewDialog>
        {
            { x => x.ViewId, _viewId },
            { x => x.Mode, "clone" },
            { x => x.ItemId, pkValue }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Large,
            FullWidth = true,
            CloseOnEscapeKey = true
        };

        var dialog = await DialogService.ShowAsync<AjaxDataviewDialog>("Clone Record", parameters, options);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            await LoadGridDataAsync();
            Snackbar.Add("Record cloned successfully.", Severity.Success);
        }
    }

    /// <summary>Port of respite_crud.addDetailsButton — shows row detail view.</summary>
    private async Task OnRowDetailsClick(Dictionary<string, object?> row)
    {
        var parameters = new DialogParameters<AjaxDataviewDialog>
        {
            { x => x.ViewId, _viewId },
            { x => x.Mode, "details" },
            { x => x.ItemId, GetPrimaryKeyValue(row) }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseOnEscapeKey = true
        };

        await DialogService.ShowAsync<AjaxDataviewDialog>("Record Details", parameters, options);
    }

    /// <summary>Port of custom toolbar action buttons — handles ActionType dispatch
    /// (legacy Select Case on ActionType: "javascript", "url", "db_command", "db_procedure").</summary>
    private async Task OnToolbarActionClick(DataViewActionDto action)
    {
        if (action.RequireConfirmation)
        {
            var confirmParams = new DialogParameters<ConfirmDeleteDialog>
            {
                { x => x.ContentText, $"Are you sure you want to execute '{action.ActionLabel}'?" }
            };

            var dialog = await DialogService.ShowAsync<ConfirmDeleteDialog>("Confirm Action", confirmParams);
            var result = await dialog.Result;
            if (result is null || result.Canceled)
                return;
        }

        switch (action.ActionType?.ToLowerInvariant())
        {
            case "url":
                var expression = ResolveActionExpression(action.ActionExpression ?? string.Empty, null);

                // ajax_dataview.asp was an endpoint the legacy POSTed to, not a page to open. The
                // one action that targets it asks for the field list to be generated, which is work
                // this application does directly.
                if (expression.StartsWith("ajax_dataview.asp", StringComparison.OrdinalIgnoreCase)
                    && expression.Contains("mode=autoinit", StringComparison.OrdinalIgnoreCase))
                {
                    await RunAutoInitAsync(expression);
                    break;
                }

                string url = TranslateLegacyUrl(expression);
                if (action.OpenURLInNewWindow)
                {
                    NavigationManager.NavigateTo(url, forceLoad: true);
                }
                else
                {
                    NavigationManager.NavigateTo(url);
                }
                break;

            case "javascript":
                Logger.LogWarning("JavaScript action type not supported in Blazor: {Expression}",
                    action.ActionExpression);
                Snackbar.Add("This action type is not available in the modernized application.", Severity.Warning);
                break;

            case "db_command":
            case "db_procedure":
                try
                {
                    await DataviewService.GetDataViewContentsCommandAsync(_viewId);
                    await LoadGridDataAsync();
                    Snackbar.Add($"Action '{action.ActionLabel}' executed.", Severity.Success);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "DB action failed: {ActionLabel}", action.ActionLabel);
                    Snackbar.Add($"Action failed: {ex.Message}", Severity.Error);
                }
                break;

            default:
                Logger.LogWarning("Unrecognized action type: {ActionType}", action.ActionType);
                Snackbar.Add($"Action type '{action.ActionType}' is not recognized.", Severity.Warning);
                break;
        }
    }

    /// <summary>Port of custom inline action buttons per row — handles ActionType dispatch.</summary>
    private async Task OnInlineActionClick(DataViewActionDto action, Dictionary<string, object?> row)
    {
        if (action.RequireConfirmation)
        {
            var confirmParams = new DialogParameters<ConfirmDeleteDialog>
            {
                { x => x.ContentText, $"Are you sure you want to execute '{action.ActionLabel}'?" }
            };

            var dialog = await DialogService.ShowAsync<ConfirmDeleteDialog>("Confirm Action", confirmParams);
            var result = await dialog.Result;
            if (result is null || result.Canceled)
                return;
        }

        string? pkValue = GetPrimaryKeyValue(row);

        switch (action.ActionType?.ToLowerInvariant())
        {
            case "url":
                string url = TranslateLegacyUrl(
                    ResolveActionExpression(action.ActionExpression ?? string.Empty, row));
                if (action.OpenURLInNewWindow)
                {
                    NavigationManager.NavigateTo(url, forceLoad: true);
                }
                else
                {
                    NavigationManager.NavigateTo(url);
                }
                break;

            case "javascript":
                Logger.LogWarning("Inline JavaScript action not supported in Blazor: {Expression}",
                    action.ActionExpression);
                Snackbar.Add("This action type is not available in the modernized application.", Severity.Warning);
                break;

            case "db_command":
            case "db_procedure":
                try
                {
                    await DataviewService.GetDataViewContentsCommandAsync(_viewId);
                    await LoadGridDataAsync();
                    Snackbar.Add($"Action '{action.ActionLabel}' executed.", Severity.Success);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Inline DB action failed: {ActionLabel}", action.ActionLabel);
                    Snackbar.Add($"Action failed: {ex.Message}", Severity.Error);
                }
                break;

            default:
                Logger.LogWarning("Unrecognized inline action type: {ActionType}", action.ActionType);
                Snackbar.Add($"Action type '{action.ActionType}' is not recognized.", Severity.Warning);
                break;
        }
    }

    private async Task OnSearchChanged()
    {
        await LoadGridDataAsync();
    }

    private void OnSelectedItemsChanged(HashSet<Dictionary<string, object?>> items)
    {
        _selectedItems = items;
    }

    private string? GetPrimaryKeyValue(Dictionary<string, object?> row)
    {
        string pk = _viewData?.Primarykey ?? string.Empty;
        if (!string.IsNullOrEmpty(pk) && row.TryGetValue(pk, out var val))
        {
            return val?.ToString();
        }
        // No fallback to "whatever column came first". That value is not this record's identity,
        // and using it means the edit or delete lands on a different row — or on none, silently.
        // Returning null lets the caller refuse the action instead of performing the wrong one.
        Logger.LogWarning(
            "Row carries no value for primary key '{Key}' of view {ViewId}; " +
            "the record cannot be identified.", pk, _viewId);

        return null;
    }

    private static string GetMudIcon(string? glyphIcon)
    {
        if (string.IsNullOrEmpty(glyphIcon))
            return Icons.Material.Filled.TouchApp;

        // Map common Font Awesome / Glyphicon classes to MudBlazor Material icons
        if (glyphIcon.Contains("fa-plus", StringComparison.OrdinalIgnoreCase))
            return Icons.Material.Filled.Add;
        if (glyphIcon.Contains("fa-edit", StringComparison.OrdinalIgnoreCase) ||
            glyphIcon.Contains("fa-pencil", StringComparison.OrdinalIgnoreCase))
            return Icons.Material.Filled.Edit;
        if (glyphIcon.Contains("fa-trash", StringComparison.OrdinalIgnoreCase) ||
            glyphIcon.Contains("fa-remove", StringComparison.OrdinalIgnoreCase))
            return Icons.Material.Filled.Delete;
        if (glyphIcon.Contains("fa-copy", StringComparison.OrdinalIgnoreCase) ||
            glyphIcon.Contains("fa-clone", StringComparison.OrdinalIgnoreCase))
            return Icons.Material.Filled.ContentCopy;
        if (glyphIcon.Contains("fa-search", StringComparison.OrdinalIgnoreCase))
            return Icons.Material.Filled.Search;
        if (glyphIcon.Contains("fa-download", StringComparison.OrdinalIgnoreCase))
            return Icons.Material.Filled.Download;
        if (glyphIcon.Contains("fa-upload", StringComparison.OrdinalIgnoreCase))
            return Icons.Material.Filled.Upload;
        if (glyphIcon.Contains("fa-refresh", StringComparison.OrdinalIgnoreCase))
            return Icons.Material.Filled.Refresh;
        if (glyphIcon.Contains("fa-magic", StringComparison.OrdinalIgnoreCase))
            return Icons.Material.Filled.AutoFixHigh;
        if (glyphIcon.Contains("fa-cog", StringComparison.OrdinalIgnoreCase) ||
            glyphIcon.Contains("fa-gear", StringComparison.OrdinalIgnoreCase))
            return Icons.Material.Filled.Settings;
        if (glyphIcon.Contains("fa-eye", StringComparison.OrdinalIgnoreCase))
            return Icons.Material.Filled.Visibility;
        if (glyphIcon.Contains("fa-file", StringComparison.OrdinalIgnoreCase))
            return Icons.Material.Filled.InsertDriveFile;
        if (glyphIcon.Contains("fa-print", StringComparison.OrdinalIgnoreCase))
            return Icons.Material.Filled.Print;
        if (glyphIcon.Contains("fa-check", StringComparison.OrdinalIgnoreCase))
            return Icons.Material.Filled.Check;
        if (glyphIcon.Contains("fa-times", StringComparison.OrdinalIgnoreCase))
            return Icons.Material.Filled.Close;
        if (glyphIcon.Contains("fa-arrow", StringComparison.OrdinalIgnoreCase))
            return Icons.Material.Filled.ArrowForward;
        if (glyphIcon.Contains("fa-play", StringComparison.OrdinalIgnoreCase))
            return Icons.Material.Filled.PlayArrow;
        if (glyphIcon.Contains("fa-stop", StringComparison.OrdinalIgnoreCase))
            return Icons.Material.Filled.Stop;
        if (glyphIcon.Contains("fa-link", StringComparison.OrdinalIgnoreCase))
            return Icons.Material.Filled.Link;

        return Icons.Material.Filled.TouchApp;
    }

    private async Task HandleAppCommand(string command)
    {
        switch (command)
        {
            case "refresh":
                await OnRefreshClick();
                break;
            case "add":
                await OnAddClick();
                break;
        }
    }

    public void Dispose()
    {
        AppState.OnCommand -= HandleAppCommand;
    }

    // ── Action expressions ────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the placeholders a stored action URL carries, against the clicked row.
    /// </summary>
    /// <remarks>
    /// <para>The stored expressions use two forms: <c>{{row[Column]}}</c> for a value from the row
    /// that was clicked, and <c>{{urlparam[name]}}</c> for one carried in the current address —
    /// including <c>{{urlparam[dataview[search]]}}</c>, whose name itself contains brackets.</para>
    /// <para>The port substituted <c>{{dataview.id}}</c> and <c>{{row.id}}</c>, which appear in no
    /// stored action. Nothing matched, so the placeholder travelled into the address bar verbatim
    /// and the target page received the literal text instead of an id.</para>
    /// </remarks>
    private string ResolveActionExpression(string expression, Dictionary<string, object?>? row)
    {
        if (string.IsNullOrWhiteSpace(expression)) return string.Empty;

        var query = System.Web.HttpUtility.ParseQueryString(
            new Uri(NavigationManager.Uri).Query);

        // Innermost first, so a name that itself contains brackets resolves correctly.
        var resolved = RxUrlParamPlaceholder.Replace(expression, m =>
            query[m.Groups["name"].Value] ?? string.Empty);

        resolved = RxRowPlaceholder.Replace(resolved, m =>
        {
            var column = m.Groups["name"].Value;

            // DT_RowId is the DataTables name for the row's key, not a column of the table.
            if (column.Equals("DT_RowId", StringComparison.OrdinalIgnoreCase))
                return row is null ? string.Empty : GetPrimaryKeyValue(row) ?? string.Empty;

            return row is not null && row.TryGetValue(column, out var v)
                ? v?.ToString() ?? string.Empty
                : string.Empty;
        });

        // Kept for expressions written against the port's own vocabulary.
        resolved = resolved.Replace("{{dataview.id}}", _viewId.ToString());

        if (row is not null && GetPrimaryKeyValue(row) is { } pk)
            resolved = resolved.Replace("{{row.id}}", pk);

        return resolved;
    }

    private static readonly System.Text.RegularExpressions.Regex RxUrlParamPlaceholder =
        new(@"\{\{urlparam\[(?<name>.+?)\]\}\}", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex RxRowPlaceholder =
        new(@"\{\{row\[(?<name>[^\]]+)\]\}\}", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Turns a legacy page reference into this application's route.
    /// </summary>
    /// <remarks>
    /// Stored actions point at <c>dataview.asp</c>, <c>admin_dataviews.asp</c> and so on, because
    /// that is what the pages were called. Navigating to those verbatim reaches nothing here.
    /// </remarks>
    private static string TranslateLegacyUrl(string url)
    {
        var match = System.Text.RegularExpressions.Regex.Match(
            url, @"^(?<page>[\w_]+)\.asp(?<query>\?.*)?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!match.Success) return url;

        var route = match.Groups["page"].Value.Replace('_', '-').ToLowerInvariant();

        return $"/aspclassic-vbscript/{route}{match.Groups["query"].Value}";
    }

    /// <summary>Generates the field list for the view named in an auto-init action.</summary>
    private async Task RunAutoInitAsync(string expression)
    {
        var query = System.Web.HttpUtility.ParseQueryString(
            expression.Contains('?') ? expression[expression.IndexOf('?')..] : string.Empty);

        if (!int.TryParse(query["ViewID"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var targetView)
            || targetView == 0)
        {
            Snackbar.Add("Select the data view whose fields should be generated.", Severity.Warning);
            return;
        }

        try
        {
            await AdminDataviewfieldsService.AutoInitDataViewFieldsAsync(targetView);
            await LoadGridDataAsync();
            Snackbar.Add($"Fields generated from the table's columns for view {targetView}.", Severity.Success);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Auto-init failed for ViewID={ViewId}", targetView);
            Snackbar.Add($"Could not generate the fields: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>
    /// Reads every <c>&lt;identifier&gt;[search]</c> parameter and maps it to the column it filters.
    /// </summary>
    /// <remarks>
    /// The identifier is a field's client-side name; the column is its FieldSource. Filtering on
    /// the identifier would match no column at all, and filtering everything as a global search
    /// returns every row that merely contains the value somewhere.
    /// </remarks>
    private void ReadColumnFiltersFromUrl()
    {
        _columnFilters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var query = System.Web.HttpUtility.ParseQueryString(new Uri(NavigationManager.Uri).Query);

        foreach (var key in query.AllKeys)
        {
            if (key is null || !key.EndsWith("[search]", StringComparison.OrdinalIgnoreCase)) continue;

            var identifier = key[..^"[search]".Length];
            var value = query[key];
            if (string.IsNullOrWhiteSpace(value)) continue;

            var field = _visibleFields.FirstOrDefault(f =>
                string.Equals(f.FieldIdentifier, identifier, StringComparison.OrdinalIgnoreCase));

            if (field?.FieldSource is { Length: > 0 } column)
            {
                _columnFilters[column] = value;
            }
            else
            {
                Logger.LogWarning(
                    "View {ViewId} carries a filter for '{Identifier}', which names no field of this view.",
                    _viewId, identifier);
            }
        }
    }
}
