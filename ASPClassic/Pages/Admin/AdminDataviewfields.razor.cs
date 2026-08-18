using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using ASPClassic.Application.DTOs.Admin;
using ASPClassic.Application.DTOs.Data;
using ASPClassic.Application.Services.Admin;
using ASPClassic.Infrastructure;
using ASPClassic.Infrastructure.Data;
using ASPClassic.Application.Services.Inc;
using ASPClassic.Application.Services.Data;
using ASPClassic.Domain.Entities.Data;

namespace ASPClassic.Pages.Admin;

/// <summary>Port of <c>admin_dataviewfields.asp</c> (Admin_Dataviewfields).</summary>
public partial class AdminDataviewfields : ComponentBase, IDisposable
{
    [Inject] private IAdminDataviewfieldsService AdminDataviewfieldsService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private AppState AppState { get; set; } = default!;
    [Inject] private IIncConfigService IncConfigService { get; set; } = default!;
    [Inject] private IDataViewLookupClassExtendable DataViewLookupExtendable { get; set; } = default!;
    [Inject] private IDataViewLookupCollectionClass DataViewLookupCollection { get; set; } = default!;
    [Inject] private IIncCrudeconstantsService IncCrudeconstantsService { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "ViewID")]
    public int? QueryViewId { get; set; }

    [SupplyParameterFromQuery(Name = "mode")]
    public string? QueryMode { get; set; }

    [SupplyParameterFromQuery(Name = "ItemID")]
    public int? QueryItemId { get; set; }

    [SupplyParameterFromQuery(Name = "MSG")]
    public string? QueryMsg { get; set; }

    private bool _loading = true;
    private string _pageTitle = "Manage Data View Fields";
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;
    private string _warningMessage = string.Empty;

    /// <summary>The column the last save asked for and could not find, if any.</summary>
    private string _missingColumn = string.Empty;
    private string _missingColumnType = string.Empty;

    private int _viewId;
    private string _dataViewTitle = string.Empty;
    private string _mode = string.Empty;
    private int _editItemId;

    private List<DataViewFieldListItemDto> _fields = new();
    private List<DataViewFieldTypesDto> _fieldTypes = new();
    private List<DataViewFieldFlagsDto> _fieldFlags = new();

    /// <summary>
    /// The flag values currently set, read out of the bitmask the edit model carries. The column
    /// stores one integer; the service takes the values individually.
    /// </summary>
    private List<int> _selectedFieldFlags =>
        _fieldFlags
            .Select(f => int.TryParse(f.FlagValue, out var v) ? v : 0)
            .Where(v => v != 0 && (_editModel.FieldFlags & v) == v)
            .ToList();
    private List<DataViewUriStylesDto> _uriStyles = new();

    private DataViewFieldEditDto _editModel = new();

    private bool _disposed;

    protected override void OnInitialized() => AppState.OnCommand = HandleAppCommand;

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
        var signature = $"{QueryViewId}|{QueryMode}|{QueryItemId}";
        if (signature == _loadedFor) return;
        _loadedFor = signature;

        await LoadPageAsync();
    }

    protected override void OnParametersSet()
    {
        if (!string.IsNullOrEmpty(QueryMsg))
        {
            _successMessage = QueryMsg switch
            {
                "add" => "Field added successfully.",
                "edit" => "Field updated successfully.",
                "delete" => "Field deleted successfully.",
                "sorted" => "Field sorting updated successfully.",
                "autoinit" => "Fields auto-initialized from table schema.",
                "notfound" => string.Empty,
                _ => string.Empty
            };
            if (QueryMsg == "notfound")
            {
                _errorMessage = "Data View not found.";
            }
        }
    }

    private async Task LoadPageAsync()
    {
        _loading = true;
        StateHasChanged();

        try
        {
            _viewId = QueryViewId ?? 0;
            _mode = QueryMode ?? string.Empty;
            _editItemId = QueryItemId ?? 0;

            // A view is identified by a non-zero id, not a positive one: this portal's own
            // system views are negative (-1 to -4). The legacy tested IsNumeric and presence and
            // never the sign, so `> 0` silently treats every built-in view as "none supplied".
            if (_viewId == 0)
            {
                _loading = false;
                StateHasChanged();
                return;
            }

            var dataView = await AdminDataviewfieldsService.GetDataViewByIdAsync(_viewId);

            if (dataView == null)
            {
                _viewId = 0;
                _errorMessage = "Data View not found.";
                _loading = false;
                StateHasChanged();
                return;
            }

            _dataViewTitle = dataView.Title ?? string.Empty;
            _pageTitle = $"Manage Data View Fields for {_dataViewTitle}";

            _fieldTypes = await AdminDataviewfieldsService.GetDataViewFieldTypesAsync();
            _fieldFlags = await AdminDataviewfieldsService.GetDataViewFieldFlagsAsync();
            _uriStyles = await AdminDataviewfieldsService.GetDataViewUriStylesAsync();

            await LoadFieldsListAsync();

            if (_mode == "edit" && _editItemId > 0)
            {
                await LoadEditModelAsync();
            }
            else if (_mode == "add")
            {
                _editModel = new DataViewFieldEditDto
                {
                    ViewID = _viewId,
                    FieldFlags = 1,
                    MaxLength = 100,
                    Width = 0,
                    Height = 0,
                    UriStyle = 1,
                    FieldType = _fieldTypes.FirstOrDefault()?.TypeValue ?? "1"
                };
            }
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading page: {ex.Message}";
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    private async Task LoadFieldsListAsync()
    {
        _fields = await AdminDataviewfieldsService.GetDataViewFieldsListAsync(_viewId);
    }

    private async Task LoadEditModelAsync()
    {
        var field = await AdminDataviewfieldsService.GetDataViewFieldByIdAsync(_editItemId);

        if (field == null)
        {
            _errorMessage = "Field not found.";
            _mode = string.Empty;
            return;
        }

        _editModel = new DataViewFieldEditDto
        {
            FieldID = field.FieldID,
            ViewID = field.ViewID,
            FieldLabel = field.FieldLabel ?? string.Empty,
            FieldSource = field.FieldSource ?? string.Empty,
            FieldType = field.FieldType ?? string.Empty,
            FieldDescription = field.FieldDescription ?? string.Empty,
            DefaultValue = field.DefaultValue ?? string.Empty,
            FieldFlags = field.FieldFlags,
            MaxLength = field.MaxLength ?? 100,
            Width = field.Width ?? 0,
            Height = field.Height ?? 0,
            UriPath = field.UriPath ?? string.Empty,
            UriStyle = field.UriStyle ?? 1,
            LinkedTable = field.LinkedTable ?? string.Empty,
            LinkedTableValueField = field.LinkedTableValueField ?? string.Empty,
            LinkedTableTitleField = field.LinkedTableTitleField ?? string.Empty,
            LinkedTableGroupField = field.LinkedTableGroupField ?? string.Empty,
            LinkedTableGlyphField = field.LinkedTableGlyphField ?? string.Empty,
            LinkedTableTooltipField = field.LinkedTableTooltipField ?? string.Empty,
            LinkedTableAddition = field.LinkedTableAddition ?? string.Empty
        };
    }

    /// <summary>Port of <c>UpdateDataViewField</c> — adds or edits a DataViewField record.
    /// Legacy: opens recordset with AddNew or updates existing by FieldID, sets all columns from form values.</summary>
    private async Task UpdateDataViewFieldAsync()
    {
        if (string.IsNullOrWhiteSpace(_editModel.FieldLabel))
        {
            _errorMessage = "Field Label is required.";
            return;
        }

        try
        {
            // The result carries the reason a save was refused. Discarding it and navigating to
            // "?MSG=add" regardless is why a rejected field reported "Field added successfully" and
            // then was not there — the one failure mode a user cannot diagnose, because the
            // application insists nothing went wrong.
            var result = _mode == "add"
                ? await AdminDataviewfieldsService.SaveDataViewFieldAsync(
                      "add", _viewId, null, _editModel, _selectedFieldFlags)
                : _mode == "edit"
                    ? await AdminDataviewfieldsService.UpdateDataViewFieldAsync(
                          _editItemId, _viewId, _editModel, _selectedFieldFlags)
                    : null;

            if (result is null) return;

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                _errorMessage = result.ErrorMessage;
                return;
            }

            // Saved with a caveat — shown, and the save still stands.
            if (!string.IsNullOrWhiteSpace(result.WarningMessage))
            {
                _warningMessage = result.WarningMessage;

                // Remembered so the column can be created from here, rather than sending someone to
                // a database tool to finish a job they started on this screen.
                _missingColumn = _editModel.FieldSource ?? string.Empty;
                _missingColumnType = _editModel.FieldType ?? "1";
                await LoadFieldsListAsync();
                _mode = string.Empty;
                return;
            }

            NavigationManager.NavigateTo(
                $"/aspclassic-vbscript/admin-dataviewfields?ViewID={_viewId}&MSG={_mode}",
                forceLoad: false);
        }
        catch (DbUpdateException ex)
        {
            _errorMessage = $"Error(s) while performing \"{_mode}\": {ex.InnerException?.Message ?? ex.Message}";
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error: {ex.Message}";
        }
    }

    /// <summary>Port of <c>DeleteDataViewField</c> — deletes a DataViewField by FieldID.
    /// Legacy: adoConn.Execute "DELETE FROM portal.DataViewField WHERE FieldID = " &amp; nItemID</summary>
    private async Task DeleteDataViewFieldAsync(int fieldId)
    {
        try
        {
            await AdminDataviewfieldsService.DeleteDataViewFieldAsync(fieldId, _viewId);

            NavigationManager.NavigateTo(
                $"/aspclassic-vbscript/admin-dataviewfields?ViewID={_viewId}&MSG=delete",
                forceLoad: false);
        }
        // Blazor signals a redirect by throwing NavigationException; catching
        // everything turns that redirect into an error and the page stays put.
        catch (Exception ex) when (ex is not NavigationException)
        {
            _errorMessage = $"Error deleting field: {ex.Message}";
        }
    }

    /// <summary>Port of legacy sortFields mode — updates FieldOrder for each field in the view.</summary>
    private async Task UpdateSortingAsync()
    {
        try
        {
            await AdminDataviewfieldsService.SortFieldsAsync(
                _viewId,
                _fields.Select((f, i) => new SortFieldOrderDto { FieldID = f.FieldID, NewOrder = i + 1 }).ToList());

            NavigationManager.NavigateTo(
                $"/aspclassic-vbscript/admin-dataviewfields?ViewID={_viewId}&MSG=sorted",
                forceLoad: false);
        }
        // Blazor signals a redirect by throwing NavigationException; catching
        // everything turns that redirect into an error and the page stays put.
        catch (Exception ex) when (ex is not NavigationException)
        {
            _errorMessage = $"Error updating sorting: {ex.Message}";
        }
    }

    /// <summary>Port of legacy autoinit mode — auto-creates fields based on table schema.
    /// Delegates to the service method which queries sys.columns metadata.</summary>
    private async Task AutoInitFieldsAsync()
    {
        try
        {
            await AdminDataviewfieldsService.AutoInitDataViewFieldsAsync(_viewId);

            NavigationManager.NavigateTo(
                $"/aspclassic-vbscript/admin-dataviewfields?ViewID={_viewId}&MSG=autoinit",
                forceLoad: false);
        }
        // Blazor signals a redirect by throwing NavigationException; catching
        // everything turns that redirect into an error and the page stays put.
        catch (Exception ex) when (ex is not NavigationException)
        {
            _errorMessage = $"Error auto-initializing fields: {ex.Message}";
        }
    }

    // ─── UI Event Handlers ───────────────────────────────────────────

    private void OnEditDataViewClick()
    {
        NavigationManager.NavigateTo(
            $"/aspclassic-vbscript/admin-dataviews?mode=edit&ItemID={_viewId}");
    }

    private void OnOpenDataViewClick()
    {
        NavigationManager.NavigateTo(
            $"/aspclassic-vbscript/dataview?ViewID={_viewId}");
    }

    private void OnAddFieldClick()
    {
        _mode = "add";
        _editItemId = 0;
        _editModel = new DataViewFieldEditDto
        {
            ViewID = _viewId,
            FieldFlags = 1,
            MaxLength = 100,
            Width = 0,
            Height = 0,
            UriStyle = 1,
            FieldType = _fieldTypes.FirstOrDefault()?.TypeValue ?? "1"
        };
        _errorMessage = string.Empty;
        StateHasChanged();
    }

    private async Task OnEditFieldClick(int fieldId)
    {
        _mode = "edit";
        _editItemId = fieldId;
        _errorMessage = string.Empty;

        await LoadEditModelAsync();
        StateHasChanged();
    }

    private async Task OnDeleteFieldClick(int fieldId)
    {
        var confirm = await DialogService.ShowMessageBox(
            "Confirm Delete",
            "Are you sure you want to delete this field?",
            yesText: "Delete",
            cancelText: "Cancel");

        if (confirm == true)
        {
            await DeleteDataViewFieldAsync(fieldId);
        }
    }

    private async Task OnSubmitClick()
    {
        await UpdateDataViewFieldAsync();
    }

    private void OnCancelEditClick()
    {
        _mode = string.Empty;
        _editItemId = 0;
        _errorMessage = string.Empty;
        StateHasChanged();
    }

    private async Task OnUpdateSortingClick()
    {
        await UpdateSortingAsync();
    }

    private async Task OnAutoInitClick()
    {
        var confirm = await DialogService.ShowMessageBox(
            "Auto-Initialize Fields",
            "This will auto-create fields based on the table schema. Existing fields will not be duplicated. Continue?",
            yesText: "Yes, auto-create",
            cancelText: "Cancel");

        if (confirm == true)
        {
            await AutoInitFieldsAsync();
        }
    }

    private void OnFlagChanged(int flagValue, bool isChecked)
    {
        if (isChecked)
        {
            _editModel.FieldFlags |= flagValue;
        }
        else
        {
            _editModel.FieldFlags &= ~flagValue;
        }
    }

    private string GetFieldTypeLabel(string fieldTypeValue)
    {
        var ft = _fieldTypes.FirstOrDefault(t => t.TypeValue == fieldTypeValue);
        return ft?.TypeLabel ?? fieldTypeValue;
    }

    /// <summary>Port of AppState command dispatch. Handles layout-to-page commands such as
    /// save, delete, refresh. The legacy admin_dataviewfields.asp handled these actions via
    /// form postback with mode=add/edit/delete/sortFields/autoinit — each command is routed
    /// to the equivalent async handler.</summary>
    private async Task HandleAppCommand(string command)
    {
        switch (command?.ToLowerInvariant())
        {
            case "save":
                // Equivalent to the legacy form submit with mode=add or mode=edit
                if (_mode == "add" || _mode == "edit")
                {
                    await UpdateDataViewFieldAsync();
                    await InvokeAsync(StateHasChanged);
                }
                break;

            case "delete":
                // Equivalent to the legacy mode=delete action — delete the currently selected/editing field
                if (_editItemId > 0)
                {
                    await DeleteDataViewFieldAsync(_editItemId);
                    await InvokeAsync(StateHasChanged);
                }
                break;

            case "refresh":
            case "reload":
                // Reload the entire page data from the database
                await LoadPageAsync();
                break;

            case "add":
                // Switch to add mode — equivalent to clicking "Add Field"
                OnAddFieldClick();
                break;

            case "sort":
            case "updatesort":
                // Equivalent to the legacy sortFields form submit
                await UpdateSortingAsync();
                await InvokeAsync(StateHasChanged);
                break;

            case "autoinit":
                // Equivalent to the legacy mode=autoinit action
                await AutoInitFieldsAsync();
                await InvokeAsync(StateHasChanged);
                break;

            case "cancel":
                // Cancel any current edit/add operation
                OnCancelEditClick();
                break;

            case "editdataview":
                // Navigate to the parent DataView edit page
                OnEditDataViewClick();
                break;

            case "opendataview":
                // Navigate to the DataView rendering page
                OnOpenDataViewClick();
                break;

            default:
                // Unknown command — log to status bar for diagnostics
                AppState.LogStatus($"Unrecognized command: {command}");
                break;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && _viewId == 0 && string.IsNullOrEmpty(_errorMessage))
        {
            NavigationManager.NavigateTo(
                "/aspclassic-vbscript/admin-dataviews?MSG=notfound", replace: true);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (AppState.OnCommand == HandleAppCommand)
            {
                AppState.OnCommand = null;
            }
            _disposed = true;
        }
    }

    /// <summary>Adds the column the last saved field is waiting for.</summary>
    private async Task OnAddMissingColumnAsync()
    {
        var column = _missingColumn;
        if (string.IsNullOrWhiteSpace(column)) return;

        var error = await AdminDataviewfieldsService.AddColumnToViewTableAsync(
            _viewId, column, _missingColumnType);

        if (error is not null)
        {
            _errorMessage = error;
            return;
        }

        _warningMessage = string.Empty;
        _missingColumn = string.Empty;
        _successMessage = $"Column '{column}' added. The field will now display and save.";

        await LoadPageAsync();
    }
}
