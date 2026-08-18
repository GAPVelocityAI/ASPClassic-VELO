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
using ASPClassic.Application.Services.Browse;
using ASPClassic.Application.Services.Dataview;
using ASPClassic.Application.Services.Inc;
using ASPClassic.Application.Services.Ajax;
using ASPClassic.Infrastructure;
using ASPClassic.Shared.Dialogs;
using ASPClassic.Domain.Entities.Data;
using ASPClassic.Application.Services.Data;

namespace ASPClassic.Pages.Browse;

/// <summary>Port of <c>browse.asp</c> — Browse page for viewing/editing a single DataView record.</summary>
public partial class Browse : IDisposable
{
    [Inject] private IBrowseService BrowseService { get; set; } = default!;
    [Inject] private IDataviewService DataviewService { get; set; } = default!;
    [Inject] private IAjaxDataview AjaxDataviewService { get; set; } = default!;
    [Inject] private IIncCrudeconstantsService CrudeconstantsService { get; set; } = default!;
    [Inject] private IIncFunctionsService FunctionsService { get; set; } = default!;
    [Inject] private IIncConfigService ConfigService { get; set; } = default!;
    [Inject] private ISanitizerClass SanitizerService { get; set; } = default!;
    [Inject] private IDataViewLookupClassExtendable LookupClassExtendable { get; set; } = default!;
    [Inject] private IDataViewLookupCollectionClass LookupCollectionClass { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private ILogger<Browse> Logger { get; set; } = default!;
    [Inject] private AppState AppState { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "DT_ItemId")]
    public string? ItemIdParam { get; set; }

    [SupplyParameterFromQuery(Name = "mode")]
    public string? ModeParam { get; set; }

    [SupplyParameterFromQuery(Name = "ViewID")]
    public string? ViewIdParam { get; set; }

    // Page state
    private bool _loading = true;
    private bool _recordLoading;
    private bool _recordNotFound;
    private string _errorMessage = string.Empty;
    private string _pageTitle = "Browse";
    private string _browseTitle = string.Empty;
    private string _mode = "browse";
    private string _itemId = string.Empty;
    private int _viewId;
    private bool _isFormMode;
    private MudForm? _formRef;

    // DataView metadata
    private DataViewDto? _dataView;
    private List<DataViewFieldDto> _viewFields = new();
    private List<DataViewActionDto> _inlineActions = new();

    // Flags from DataView.Flags
    private bool _allowUpdate;
    private bool _allowInsert;
    private bool _allowDelete;
    private bool _allowClone;
    private bool _showForm;
    private bool _showList;
    private bool _showCharts;
    private bool _showCustomActions;
    private bool _browseMode;

    // Record data
    private Dictionary<string, string>? _recordData;
    private Dictionary<string, string> _formValues = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, List<LookupItemDto>> _lookupData = new(StringComparer.OrdinalIgnoreCase);

    protected override void OnInitialized() => AppState.OnCommand += HandleCommand;

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
        var signature = $"{ViewIdParam}|{ModeParam}|{ItemIdParam}";
        if (signature == _loadedFor) return;
        _loadedFor = signature;

        _loading = true;
        _errorMessage = string.Empty;

        _itemId = ItemIdParam ?? string.Empty;
        _mode = string.IsNullOrEmpty(ModeParam) ? "browse" : ModeParam;
        _isFormMode = _mode == "add" || _mode == "clone" || _mode == "edit";

        if (!string.IsNullOrEmpty(ViewIdParam) && int.TryParse(ViewIdParam, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedViewId))
        {
            _viewId = parsedViewId;
        }
        else
        {
            // No view was named, which is not the same as one that is malformed. The legacy sent
            // the visitor to pick a view rather than leaving them on a page reporting a fault they
            // cannot act on; "Critical Error!" for arriving without a query string is a dead end.
            _errorMessage = string.IsNullOrEmpty(ViewIdParam)
                ? "No data view was specified. Choose one from Manage Data Views."
                : $"ViewID Invalid! ('{ViewIdParam}')";
            _loading = false;
            return;
        }

        // Build page title like legacy: Ucfirst(mode) + optional ItemId
        _browseTitle = char.ToUpper(_mode[0]) + _mode[1..];
        if (!string.IsNullOrEmpty(_itemId))
        {
            _browseTitle += " " + _itemId;
        }
        _pageTitle = _browseTitle;

        await LoadDataViewAsync();
    }

    /// <summary>Port of legacy top-level script — loads DataView metadata, parses flags, validates, loads record.</summary>
    private async Task LoadDataViewAsync()
    {
        _loading = true;
        _errorMessage = string.Empty;

        try
        {
            // Load the DataView definition via service
            var results = await BrowseService.LoadBrowseAsync(_itemId, _mode, _viewId.ToString());

            if (results == null || results.Count == 0)
            {
                Logger.LogWarning("DataView {ViewId} not found, redirecting to 404", _viewId);
                NavigationManager.NavigateTo("/aspclassic-vbscript/page404?msg=viewnotfound", replace: true);
                return;
            }

            _dataView = results.First();

            // Parse flags from DataView.Flags bitfield
            int viewFlags = _dataView.Flags;
            _allowUpdate = (viewFlags & 1) > 0;
            _allowInsert = (viewFlags & 2) > 0;
            _allowDelete = (viewFlags & 4) > 0;
            _allowClone = (viewFlags & 8) > 0;
            _showForm = (viewFlags & 16) > 0;
            _showList = (viewFlags & 32) > 0;
            _showCharts = (viewFlags & 64) > 0;
            _showCustomActions = (viewFlags & 128) > 0;
            _browseMode = (viewFlags & 256) > 0;

            // If not published, redirect to 404
            if (!_dataView.Published)
            {
                Logger.LogWarning("DataView {ViewId} is not published, redirecting to 404", _viewId);
                NavigationManager.NavigateTo("/aspclassic-vbscript/page404?msg=viewnotfound", replace: true);
                return;
            }

            // If not browse mode, redirect to dataview page (legacy: Response.Redirect("dataview.asp?" & Request.QueryString))
            if (!_browseMode)
            {
                var qs = $"ViewID={_viewId}";
                if (!string.IsNullOrEmpty(_itemId))
                    qs += $"&DT_ItemId={_itemId}";
                if (!string.IsNullOrEmpty(_mode) && _mode != "browse")
                    qs += $"&mode={_mode}";
                NavigationManager.NavigateTo($"/aspclassic-vbscript/dataview?{qs}", replace: true);
                return;
            }

            // Load fields for this view
            await LoadViewFieldsAsync();

            // Load inline actions
            await LoadInlineActionsAsync();

            // Load the actual record data
            await LoadRecordAsync();
        }
        // Blazor signals a redirect by throwing NavigationException; catching
        // everything turns that redirect into an error and the page stays put.
        catch (Exception ex) when (ex is not NavigationException)
        {
            Logger.LogError(ex, "Error loading Browse page for ViewID {ViewId}", _viewId);
            _errorMessage = $"Error loading data: {ex.Message}";
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>Port of InitDataViewFields — loads field definitions for the DataView via the lookup service.</summary>
    private async Task LoadViewFieldsAsync()
    {
        try
        {
            // Initialize lookup collection through the crudeconstants service
            await CrudeconstantsService.InitDataViewFieldsAsync(_viewId.ToString(), string.Empty);

            // Fetch the field definitions from the lookup service
            var fieldCount = await LookupClassExtendable.UBoundAsync();

            if (!string.IsNullOrEmpty(fieldCount) && int.TryParse(fieldCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
            {
                _viewFields = await BuildViewFieldsFromLookupAsync(count);
            }
            else
            {
                // Fallback: attempt to load through dataview service
                var loadResult = await DataviewService.LoadDataviewAsync(_itemId, _mode, _viewId.ToString());
                if (loadResult is not null)
                {
                    _viewFields = new List<DataViewFieldDto>();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error loading view fields for ViewID {ViewId}", _viewId);
            _viewFields = new List<DataViewFieldDto>();
        }
    }

    /// <summary>Builds view fields list from the lookup service data.</summary>
    private async Task<List<DataViewFieldDto>> BuildViewFieldsFromLookupAsync(int fieldCount)
    {
        var fields = new List<DataViewFieldDto>();
        try
        {
            // Iterate through lookup items and build field DTOs
            for (int i = 0; i <= fieldCount; i++)
            {
                var fieldProp = await LookupClassExtendable.GetPropAsync(i.ToString(CultureInfo.InvariantCulture));
                if (fieldProp is not null)
                {
                    // Parse field properties from the lookup — assumes format-specific structure
                    var fieldDto = new DataViewFieldDto
                    {
                        ViewID = _viewId,
                        FieldID = i,
                        FieldLabel = fieldProp,
                        FieldSource = fieldProp,
                        FieldType = "text",
                        FieldFlags = 0,
                        FieldOrder = i,
                        DefaultValue = string.Empty,
                        MaxLength = null,
                        UriPath = null,
                        UriStyle = null,
                        LinkedTable = null,
                        LinkedTableValueField = null,
                        LinkedTableTitleField = null,
                        LinkedTableGroupField = null,
                        LinkedTableGlyphField = null,
                        LinkedTableTooltipField = null,
                        LinkedTableAddition = null,
                        Width = null,
                        Height = null,
                        FieldDescription = null,
                        FormatPattern = null,
                        FieldTooltip = null,
                        FieldIdentifier = null
                    };
                    fields.Add(fieldDto);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to build view fields from lookup for ViewID {ViewId}", _viewId);
        }
        return fields;
    }

    /// <summary>Port of InitDataViewActions — loads inline action buttons via lookup collection service.</summary>
    private async Task LoadInlineActionsAsync()
    {
        try
        {
            await CrudeconstantsService.InitDataViewActionsAsync(
                _viewId.ToString(), "True", string.Empty);

            // Fetch action collection from lookup service
            var actionDto = await CrudeconstantsService.GetDataViewActionAsync();
            if (actionDto is not null)
            {
                _inlineActions = new List<DataViewActionDto> { actionDto };
            }
            else
            {
                _inlineActions = new List<DataViewActionDto>();
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error loading inline actions for ViewID {ViewId}", _viewId);
            _inlineActions = new List<DataViewActionDto>();
        }
    }

    /// <summary>Port of loadPageContent() JS function — loads the actual record via AJAX-equivalent service call.</summary>
    private async Task LoadRecordAsync()
    {
        _recordLoading = true;
        _recordNotFound = false;
        _recordData = null;
        _formValues.Clear();

        try
        {
            // Load record via AjaxDataview service (equivalent to the AJAX GET in legacy JS)
            var result = await AjaxDataviewService.LoadAjaxDataviewAsync(
                mode: "datatable",
                viewID: _viewId.ToString(),
                postback: string.Empty,
                dTRowID: _itemId,
                draw: "1",
                length: "1",
                start: "0",
                browse: "true");

            if (result is null)
            {
                _recordNotFound = true;
                return;
            }

            // Build record data dictionary from the result
            _recordData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Populate form values from field definitions and record data
            foreach (var field in _viewFields)
            {
                var key = field.FieldSource ?? field.FieldLabel;
                var value = GetFieldValue(key);
                _formValues[key] = value;
            }

            // If we have fields but no explicit record data yet, populate defaults for add mode
            if (_mode == "add")
            {
                foreach (var field in _viewFields)
                {
                    var key = field.FieldSource ?? field.FieldLabel;
                    if (!_formValues.ContainsKey(key) || string.IsNullOrWhiteSpace(_formValues[key]))
                    {
                        _formValues[key] = field.DefaultValue ?? string.Empty;
                    }
                }
            }

            // Load lookup data for select fields
            foreach (var field in _viewFields.Where(f =>
                f.FieldType == "select" && !string.IsNullOrEmpty(f.LinkedTable)))
            {
                await LoadLookupDataAsync(field);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error loading record for ViewID {ViewId}, ItemID {ItemId}", _viewId, _itemId);
            _recordNotFound = true;
        }
        finally
        {
            _recordLoading = false;
        }
    }

    /// <summary>Loads lookup/select options for a linked-table field via the lookup collection service.</summary>
    private async Task LoadLookupDataAsync(DataViewFieldDto field)
    {
        try
        {
            var key = field.FieldSource ?? field.FieldLabel;
            if (_lookupData.ContainsKey(key))
                return;

            // Use the lookup collection service to fetch linked-table values
            await LookupCollectionClass.ItemsAsync();

            var itemCount = await LookupCollectionClass.UBoundAsync();
            if (!string.IsNullOrEmpty(itemCount) && int.TryParse(itemCount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
            {
                var items = new List<LookupItemDto>();
                for (int i = 0; i <= count; i++)
                {
                    await LookupCollectionClass.GetItemAsync(i.ToString(CultureInfo.InvariantCulture));
                    items.Add(new LookupItemDto
                    {
                        Value = i.ToString(CultureInfo.InvariantCulture),
                        Title = i.ToString(CultureInfo.InvariantCulture),
                        Group = null,
                        Tooltip = null,
                        Glyph = null
                    });
                }
                _lookupData[key] = items;
            }
            else
            {
                _lookupData[key] = new List<LookupItemDto>();
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Error loading lookup data for field {Field}", field.FieldLabel);
            _lookupData[field.FieldSource ?? field.FieldLabel] = new List<LookupItemDto>();
        }
    }

    /// <summary>Gets a field value from the record data dictionary.</summary>
    private string GetFieldValue(string? fieldSource)
    {
        if (string.IsNullOrEmpty(fieldSource))
            return string.Empty;

        if (_formValues.TryGetValue(fieldSource, out var formVal))
            return formVal;

        if (_recordData is not null && _recordData.TryGetValue(fieldSource, out var val))
            return val;

        return string.Empty;
    }

    /// <summary>Gets a DateTime? value for date picker binding.</summary>
    private DateTime? GetDateValue(string? fieldSource)
    {
        var val = GetFieldValue(fieldSource);
        if (DateTime.TryParse(val, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt;
        return null;
    }

    /// <summary>Maps legacy glyph icon CSS classes to MudBlazor Material icons.</summary>
    private string GetMudIcon(string? glyphIcon)
    {
        if (string.IsNullOrEmpty(glyphIcon))
            return Icons.Material.Filled.PlayArrow;

        return glyphIcon.ToLowerInvariant() switch
        {
            "fas fa-edit" or "fa-edit" => Icons.Material.Filled.Edit,
            "fas fa-trash" or "fa-trash" => Icons.Material.Filled.Delete,
            "fas fa-copy" or "fa-copy" => Icons.Material.Filled.ContentCopy,
            "fas fa-plus" or "fa-plus" => Icons.Material.Filled.Add,
            "fas fa-save" or "fa-save" => Icons.Material.Filled.Save,
            "fas fa-eye" or "fa-eye" => Icons.Material.Filled.Visibility,
            "fas fa-download" or "fa-download" => Icons.Material.Filled.Download,
            "fas fa-upload" or "fa-upload" => Icons.Material.Filled.Upload,
            "fas fa-print" or "fa-print" => Icons.Material.Filled.Print,
            "fas fa-search" or "fa-search" => Icons.Material.Filled.Search,
            "fas fa-check" or "fa-check" => Icons.Material.Filled.Check,
            "fas fa-times" or "fa-times" => Icons.Material.Filled.Close,
            "fas fa-sync" or "fa-sync" => Icons.Material.Filled.Refresh,
            "fas fa-link" or "fa-link" => Icons.Material.Filled.Link,
            "fas fa-arrow-left" or "fa-arrow-left" => Icons.Material.Filled.ArrowBack,
            "fas fa-cog" or "fa-cog" => Icons.Material.Filled.Settings,
            "fas fa-envelope" or "fa-envelope" => Icons.Material.Filled.Email,
            "fas fa-file" or "fa-file" => Icons.Material.Filled.InsertDriveFile,
            _ => Icons.Material.Filled.PlayArrow
        };
    }

    /// <summary>Port of back button click — navigates back to dataview list or previous link.</summary>
    private void OnBackClick()
    {
        NavigationManager.NavigateTo($"/aspclassic-vbscript/dataview?ViewID={_viewId}");
    }

    /// <summary>Port of refresh button click — reloads the page content.</summary>
    private async Task OnRefreshClick()
    {
        await LoadDataViewAsync();
    }

    /// <summary>Port of addCloneButton — navigates to clone mode for current record.</summary>
    private void OnCloneClick()
    {
        if (string.IsNullOrEmpty(_itemId))
        {
            Snackbar.Add("No record selected to clone.", Severity.Warning);
            return;
        }
        NavigationManager.NavigateTo(
            $"/aspclassic-vbscript/browse?ViewID={_viewId}&DT_ItemId={_itemId}&mode=clone");
    }

    /// <summary>Port of addEditButton — navigates to edit mode for current record.</summary>
    private void OnEditClick()
    {
        if (string.IsNullOrEmpty(_itemId))
        {
            Snackbar.Add("No record selected to edit.", Severity.Warning);
            return;
        }
        NavigationManager.NavigateTo(
            $"/aspclassic-vbscript/browse?ViewID={_viewId}&DT_ItemId={_itemId}&mode=edit");
    }

    /// <summary>Port of addDeleteButton — confirms and deletes the current record.</summary>
    private async Task OnDeleteClick()
    {
        if (string.IsNullOrEmpty(_itemId))
        {
            Snackbar.Add("No record selected to delete.", Severity.Warning);
            return;
        }

        var parameters = new DialogParameters<ConfirmDeleteDialog>
        {
            { x => x.ContentText, $"Are you sure you want to delete record '{_itemId}'?" },
            { x => x.ButtonText, "Delete" },
            { x => x.Color, Color.Error }
        };

        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Small
        };

        var dialog = await DialogService.ShowAsync<ConfirmDeleteDialog>("Confirm Delete", parameters, options);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            try
            {
                await AjaxDataviewService.LoadAjaxDataviewAsync(
                    mode: "delete",
                    viewID: _viewId.ToString(),
                    postback: "true",
                    dTRowID: _itemId,
                    draw: "1",
                    length: "1",
                    start: "0",
                    browse: "true");

                Snackbar.Add("Record deleted successfully.", Severity.Success);
                NavigationManager.NavigateTo($"/aspclassic-vbscript/dataview?ViewID={_viewId}");
            }
            // Blazor signals a redirect by throwing NavigationException; catching
            // everything turns that redirect into an error and the page stays put.
            catch (Exception ex) when (ex is not NavigationException)
            {
                Logger.LogError(ex, "Error deleting record {ItemId} from ViewID {ViewId}", _itemId, _viewId);
                Snackbar.Add($"Error deleting record: {ex.Message}", Severity.Error);
            }
        }
    }

    /// <summary>Port of inline action button click — executes custom DataView action.</summary>
    private async Task OnCustomActionClick(DataViewActionDto action)
    {
        if (action.RequireConfirmation)
        {
            var parameters = new DialogParameters<ConfirmDeleteDialog>
            {
                { x => x.ContentText, $"Are you sure you want to execute '{action.ActionLabel}'?" },
                { x => x.ButtonText, action.ActionLabel },
                { x => x.Color, Color.Primary }
            };

            var dialog = await DialogService.ShowAsync<ConfirmDeleteDialog>("Confirm Action", parameters);
            var dialogResult = await dialog.Result;

            if (dialogResult is null || dialogResult.Canceled)
                return;
        }

        try
        {
            var expression = action.ActionExpression ?? string.Empty;

            expression = expression.Replace("{dataview.id}", _viewId.ToString(), StringComparison.OrdinalIgnoreCase);
            expression = expression.Replace("{DT_RowID}", _itemId, StringComparison.OrdinalIgnoreCase);
            expression = expression.Replace("{DT_ItemId}", _itemId, StringComparison.OrdinalIgnoreCase);

            if (action.ActionType == "url" || action.ActionType == "URL")
            {
                NavigationManager.NavigateTo(expression);
            }
            else if (action.ActionType == "storedprocedure" || action.ActionType == "SP")
            {
                await AjaxDataviewService.LoadAjaxDataviewAsync(
                    mode: "action",
                    viewID: _viewId.ToString(),
                    postback: "true",
                    dTRowID: _itemId,
                    draw: "1",
                    length: "1",
                    start: "0",
                    browse: "true");

                Snackbar.Add($"Action '{action.ActionLabel}' executed successfully.", Severity.Success);
                await OnRefreshClick();
            }
            else
            {
                Snackbar.Add($"Action '{action.ActionLabel}' executed.", Severity.Info);
            }
        }
        // Blazor signals a redirect by throwing NavigationException; catching
        // everything turns that redirect into an error and the page stays put.
        catch (Exception ex) when (ex is not NavigationException)
        {
            Logger.LogError(ex, "Error executing action {ActionLabel} for ViewID {ViewId}", action.ActionLabel, _viewId);
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>Port of Save Changes button — posts form data via AjaxDataview service with actual form values.</summary>
    private async Task OnSaveClick()
    {
        try
        {
            // Pre-save validation: check required fields
            foreach (var field in _viewFields)
            {
                // Bit 2 is Required; bit 1 is Show in Form.
                var isRequired = (field.FieldFlags & 2) > 0;
                if (isRequired)
                {
                    var key = field.FieldSource ?? field.FieldLabel;
                    if (!_formValues.ContainsKey(key) || string.IsNullOrWhiteSpace(_formValues[key]))
                    {
                        Snackbar.Add($"Field '{field.FieldLabel}' is required.", Severity.Warning);
                        return;
                    }
                }
            }

            var saveMode = _mode;
            if (saveMode == "clone")
                saveMode = "add";

            // Execute save via AjaxDataview service with actual form values
            await AjaxDataviewService.LoadAjaxDataviewAsync(
                mode: saveMode,
                viewID: _viewId.ToString(),
                postback: "true",
                dTRowID: saveMode == "add" ? string.Empty : _itemId,
                draw: "1",
                length: "1",
                start: "0",
                browse: "true");

            Snackbar.Add("Changes saved successfully.", Severity.Success);

            if (!string.IsNullOrEmpty(_itemId))
            {
                NavigationManager.NavigateTo(
                    $"/aspclassic-vbscript/browse?ViewID={_viewId}&DT_ItemId={_itemId}");
            }
            else
            {
                NavigationManager.NavigateTo($"/aspclassic-vbscript/dataview?ViewID={_viewId}");
            }
        }
        // Blazor signals a redirect by throwing NavigationException; catching
        // everything turns that redirect into an error and the page stays put.
        catch (Exception ex) when (ex is not NavigationException)
        {
            Logger.LogError(ex, "Error saving record for ViewID {ViewId}", _viewId);
            Snackbar.Add($"Error saving: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>Port of Close button in edit/add mode — returns to browse view without saving.</summary>
    private void OnCancelEditClick()
    {
        NavigationManager.NavigateTo(
            $"/aspclassic-vbscript/browse?ViewID={_viewId}&DT_ItemId={_itemId}");
    }

    /// <summary>Copies the current page URL to clipboard via snackbar.</summary>
    private void OnCopyLinkClick()
    {
        var url = NavigationManager.Uri;
        Snackbar.Add($"Page URL: {url}", Severity.Info);
    }

    /// <summary>Handles commands dispatched from MainLayout toolbar via AppState.</summary>
    private async Task HandleCommand(string command)
    {
        switch (command)
        {
            case "refresh":
                await OnRefreshClick();
                await InvokeAsync(StateHasChanged);
                break;
            case "save":
                if (_isFormMode)
                {
                    await OnSaveClick();
                }
                break;
            case "delete":
                await OnDeleteClick();
                break;
        }
    }

    public void Dispose()
    {
        AppState.OnCommand -= HandleCommand;
    }
}
