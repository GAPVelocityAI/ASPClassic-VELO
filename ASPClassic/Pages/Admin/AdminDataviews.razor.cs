using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using ASPClassic.Application.DTOs.Data;
using ASPClassic.Application.DTOs.Admin;
using ASPClassic.Application.Services.Admin;
using ASPClassic.Application.Services.Inc;
using ASPClassic.Application.Services.Data;
using ASPClassic.Infrastructure;
using ASPClassic.Infrastructure.Data;
using ASPClassic.Domain.Entities.Data;

namespace ASPClassic.Pages.Admin;

/// <summary>Port of <c>Admin_Dataviews</c> (admin_dataviews.asp).</summary>
public partial class AdminDataviews : ComponentBase, IDisposable
{
    [Inject] private IAdminDataviewsService AdminDataviewsService { get; set; } = default!;
    [Inject] private IIncConfigService IncConfigService { get; set; } = default!;
    [Inject] private IDataViewLookupClassExtendable DataViewLookupExtendable { get; set; } = default!;
    [Inject] private IDataViewLookupCollectionClass DataViewLookupCollection { get; set; } = default!;
    [Inject] private IIncCrudeconstantsService IncCrudeconstantsService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private ILogger<AdminDataviews> Logger { get; set; } = default!;
    [Inject] private AppState AppState { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "MSG")]
    [Parameter]
    public string? Msg { get; set; }

    // ── State ──
    private List<DataViewDto> _dataViews = new();
    private List<DataViewFlagsDto> _viewFlags = new();
    private List<DataViewDataTableFlagsDto> _dataTableFlags = new();
    private List<DataViewModifierButtonStylesDto> _modifierButtonStyles = new();
    private List<DataViewPagingTypesDto> _pagingStyles = new();
    private List<string> _dataSources = new() { "Default" };

    private bool _isLoading;
    private bool _isSaving;
    private bool _showForm;
    private string _editMode = "add";
    private int _editItemId;
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;
    private string _pageTitle = "Manage Data Views";

    private DataViewEditModel _formModel = new();

    protected override async Task OnInitializedAsync()
    {
        AppState.OnCommand += HandleCommand;
        await LoadLookupsAsync();
        await LoadDataViewsAsync();

        if (!string.IsNullOrEmpty(Msg))
        {
            _successMessage = Msg switch
            {
                "edit" => "Data View updated successfully.",
                "delete" => "Data View deleted successfully.",
                "add" => "Data View added successfully.",
                _ => $"Operation '{Msg}' completed."
            };
        }
    }

    private async Task HandleCommand(string command)
    {
        await InvokeAsync(StateHasChanged);
    }

    private async Task LoadLookupsAsync()
    {
        var flags = await AdminDataviewsService.GetDataViewFlagsAsync();
        _viewFlags = flags;

        var dataTableFlags = await AdminDataviewsService.GetDataViewDataTableFlagsAsync();
        _dataTableFlags = dataTableFlags;

        var modifierButtonStyles = await AdminDataviewsService.GetDataViewModifierButtonStylesAsync();
        _modifierButtonStyles = modifierButtonStyles;

        var pagingStyles = await AdminDataviewsService.GetDataViewPagingTypesAsync();
        _pagingStyles = pagingStyles;

        // Load data sources from config service
        var defaultDs = await IncConfigService.GetConfigValueAsync(
            "connectionStrings", "name", "connectionString", "Default", string.Empty);
        // For now, provide "Default" as the standard data source
        if (!_dataSources.Contains("Default"))
            _dataSources.Add("Default");
    }

    private async Task LoadDataViewsAsync()
    {
        _isLoading = true;
        StateHasChanged();

        try
        {
            _dataViews = await AdminDataviewsService.GetAllDataViewsAsync();
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    /// <summary>Port of legacy GetDataView — loads a single DataView for editing.</summary>
    private async Task<DataViewDto?> GetDataViewAsync(int viewId)
    {
        // By id. This took a viewId and then called the parameterless overload, which returns the
        // first view in the table whatever it is asked for.
        return await AdminDataviewsService.GetDataViewByIdAsync(viewId);
    }

    /// <summary>Port of legacy LoadAdminDataviews — saves (add/edit) a DataView.</summary>
    private async Task LoadAdminDataviewsAsync()
    {
        _isSaving = true;
        _errorMessage = string.Empty;
        StateHasChanged();

        try
        {
            // The whole form, not a subset. The narrow overload takes eight values and passes
            // empty strings and empty flag lists for everything else — so editing a description
            // never sent it, and a save that "succeeded" would have blanked the description, the
            // order-by and the paging style, and zeroed BOTH flag bitmasks. That is data loss, not
            // a failed save, and it is what made an edit of one field look like a validation error.
            var result = await AdminDataviewsService.LoadAdminDataviewsExtendedAsync(
                _editMode,
                // A ViewID is non-zero, not positive — the portal's own screens are -1 to -4.
                _editItemId != 0 ? _editItemId.ToString(CultureInfo.InvariantCulture) : string.Empty,
                _formModel.Title,
                _formModel.DataSource,
                _formModel.Published.ToString(CultureInfo.InvariantCulture),
                _formModel.MainTable,
                _formModel.Primarykey,
                _formModel.ModificationProcedure,
                _formModel.ViewProcedure,
                _formModel.DeleteProcedure,
                _formModel.ViewDescription,
                _formModel.OrderBy,
                _formModel.RowReorderColumn,
                _formModel.DataTableModifierButtonStyle.ToString(CultureInfo.InvariantCulture),
                _formModel.DataTableDefaultPageSize.ToString(CultureInfo.InvariantCulture),
                _formModel.DataTablePagingStyle,
                BitsOf(_formModel.Flags),
                BitsOf(_formModel.DataTableFlags));

            if (!result.Success)
            {
                // Say what actually went wrong. "Please check your inputs" describes every possible
                // failure and helps with none of them.
                _errorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "Failed to save Data View."
                    : result.ErrorMessage;
                return;
            }

            if (_editMode == "add")
            {
                var newViewId = result.NewViewID ?? result.DataView?.ViewID ?? 0;
                NavigationManager.NavigateTo(
                    $"/aspclassic-vbscript/admin-dataviewfields?mode=autoinit&ViewID={newViewId}");
                return;
            }

            _showForm = false;
            _successMessage = "Data View updated successfully.";
            await LoadDataViewsAsync();
        }
        // Blazor signals a redirect by throwing NavigationException; catching
        // everything turns that redirect into an error and the page stays put.
        catch (Exception ex) when (ex is not NavigationException)
        {
            _errorMessage = $"Error while performing \"{_editMode}\": {ex.Message}";
        }
        finally
        {
            _isSaving = false;
            StateHasChanged();
        }
    }

    /// <summary>Port of legacy delete — DELETE FROM DataViewField WHERE ViewID = x; DELETE FROM DataView WHERE ViewID = x.</summary>
    private async Task DeleteDataViewAsync(int viewId)
    {
        await AdminDataviewsService.DeleteDataViewAsync(viewId);
    }

    // ── UI Event Handlers ──

    private void OnAddClick()
    {
        _editMode = "add";
        _editItemId = 0;
        _errorMessage = string.Empty;
        _successMessage = string.Empty;
        _showForm = true;

        // Initialize form with defaults from flag lookups (matches legacy: default flags from lookup defaults)
        _formModel = new DataViewEditModel
        {
            DataSource = "Default",
            DataTableDefaultPageSize = 10,
            DataTablePagingStyle = _pagingStyles.FirstOrDefault()?.StyleValue ?? string.Empty
        };

        // Set default flags from lookup defaults
        int defaultViewFlags = 0;
        foreach (var flag in _viewFlags)
        {
            if (bool.TryParse(flag.FlagDefault, out var isDefault) && isDefault &&
                int.TryParse(flag.FlagValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var flagVal))
            {
                defaultViewFlags += flagVal;
            }
        }
        _formModel.Flags = defaultViewFlags;

        int defaultDtFlags = 0;
        foreach (var flag in _dataTableFlags)
        {
            if (bool.TryParse(flag.FlagDefault, out var isDefault) && isDefault &&
                int.TryParse(flag.FlagValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var flagVal))
            {
                defaultDtFlags += flagVal;
            }
        }
        _formModel.DataTableFlags = defaultDtFlags;

        StateHasChanged();
    }

    private async Task OnEditClick(DataViewDto item)
    {
        _editMode = "edit";
        _editItemId = item.ViewID;
        _errorMessage = string.Empty;
        _successMessage = string.Empty;
        _showForm = true;

        // BY ID. The parameterless overload returns the FIRST view ordered by ViewID — always the
        // same one, whichever row was clicked — so the form was populated from that view and saving
        // then copied it over the row being edited. The legacy is explicit about this:
        // SELECT * FROM portal.DataView WHERE ViewID = nItemID.
        var entity = await AdminDataviewsService.GetDataViewByIdAsync(item.ViewID);

        if (entity != null)
        {
            _formModel = new DataViewEditModel
            {
                Title = entity.Title ?? string.Empty,
                DataSource = entity.DataSource ?? "Default",
                MainTable = entity.MainTable ?? string.Empty,
                Primarykey = entity.Primarykey ?? string.Empty,
                OrderBy = entity.OrderBy ?? string.Empty,
                RowReorderColumn = entity.RowReorderColumn ?? string.Empty,
                ViewDescription = entity.ViewDescription ?? string.Empty,
                ModificationProcedure = entity.ModificationProcedure ?? string.Empty,
                ViewProcedure = entity.ViewProcedure ?? string.Empty,
                DeleteProcedure = entity.DeleteProcedure ?? string.Empty,
                Published = entity.Published,
                Flags = entity.Flags,
                DataTableModifierButtonStyle = entity.DataTableModifierButtonStyle,
                DataTableDefaultPageSize = entity.DataTableDefaultPageSize,
                DataTableFlags = entity.DataTableFlags,
                DataTablePagingStyle = entity.DataTablePagingStyle ?? string.Empty
            };
        }
        else
        {
            _errorMessage = "Item Not Found";
            _showForm = false;
        }

        StateHasChanged();
    }

    private async Task OnDeleteClick(DataViewDto item)
    {
        var confirmed = await DialogService.ShowMessageBox(
            "Confirm Delete",
            $"Are you sure you want to delete Data View \"{item.Title}\" (ID: {item.ViewID})?",
            yesText: "Delete",
            cancelText: "Cancel");

        if (confirmed == true)
        {
            try
            {
                await DeleteDataViewAsync(item.ViewID);
                _successMessage = "Data View deleted successfully.";
                _showForm = false;
                await LoadDataViewsAsync();
            }
            catch (Exception ex)
            {
                _errorMessage = $"Error while performing delete: {ex.Message}";
            }
        }
    }

    private async Task OnSubmitClick()
    {
        // Pre-save validation (matches legacy: Title required)
        if (string.IsNullOrWhiteSpace(_formModel.Title))
        {
            _errorMessage = "Title is required.";
            return;
        }

        // Validate primary key if main table specified but no PK
        if (!string.IsNullOrWhiteSpace(_formModel.MainTable) && string.IsNullOrWhiteSpace(_formModel.Primarykey))
        {
            _errorMessage = "Primary Key must be specified for this table!";
            return;
        }

        await LoadAdminDataviewsAsync();
    }

    private void OnCancelFormClick()
    {
        _showForm = false;
        _errorMessage = string.Empty;
        StateHasChanged();
    }

    /// <summary>Navigate to Manage Fields page for the currently editing item.</summary>
    private void OnManageFieldsClick()
    {
        // A ViewID is non-zero, not positive: the portal's own screens are -1 to -4. Guarding on
        // `> 0` sends an empty id for those, and the save then falls through to "Invalid input!"
        // — a message about the form, for a view that was never identified.
        if (_editItemId != 0)
        {
            NavigationManager.NavigateTo($"/aspclassic-vbscript/admin-dataviewfields?ViewID={_editItemId}");
        }
    }

    /// <summary>Navigate to Manage Fields page for a grid row item.</summary>
    private void OnManageFieldsForItem(int viewId)
    {
        NavigationManager.NavigateTo($"/aspclassic-vbscript/admin-dataviewfields?ViewID={viewId}");
    }

    /// <summary>Opens the data view a grid row names.</summary>
    private void OnOpenDataViewForItem(int viewId)
    {
        NavigationManager.NavigateTo($"/aspclassic-vbscript/dataview?ViewID={viewId}");
    }

    /// <summary>Duplicates a data view together with its fields, then reloads the list.</summary>
    private async Task OnCloneClick(DataViewDto item)
    {
        try
        {
            var newId = await AdminDataviewsService.CloneDataViewAsync(item.ViewID);

            if (newId is null)
            {
                Snackbar.Add("That data view could not be cloned.", Severity.Error);
                return;
            }

            await LoadDataViewsAsync();
            Snackbar.Add($"Cloned '{item.Title}' as view {newId}.", Severity.Success);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Cloning view {ViewId} failed", item.ViewID);
            Snackbar.Add($"Could not clone: {ex.Message}", Severity.Error);
        }
    }

    /// <summary>Navigate to the Data View display page.</summary>
    private void OnOpenDataViewClick()
    {
        if (_editItemId != 0)
        {
            NavigationManager.NavigateTo($"/aspclassic-vbscript/dataview?ViewID={_editItemId}");
        }
    }

    /// <summary>Navigate to Data View page when clicking title link in grid (matches legacy: href="dataview.asp?ViewID=...").</summary>
    private void OnViewTitleClick(DataViewDto item)
    {
        NavigationManager.NavigateTo($"/aspclassic-vbscript/dataview?ViewID={item.ViewID}");
    }

    // ── Flag Helpers ──

    /// <summary>Check if a bitwise flag is set (matches legacy: (rsItems("Flags") AND objChild.Value) > 0).</summary>
    private static bool IsFlagSet(int currentFlags, string flagValueStr)
    {
        if (int.TryParse(flagValueStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var flagVal) && flagVal > 0)
        {
            return (currentFlags & flagVal) > 0;
        }
        return false;
    }

    /// <summary>Toggle a bitwise flag on/off.</summary>
    private void ToggleFlag(bool isChecked, string flagValueStr, bool isViewFlag)
    {
        if (!int.TryParse(flagValueStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var flagVal) || flagVal <= 0)
            return;

        if (isViewFlag)
        {
            if (isChecked)
                _formModel.Flags |= flagVal;
            else
                _formModel.Flags &= ~flagVal;
        }
        else
        {
            if (isChecked)
                _formModel.DataTableFlags |= flagVal;
            else
                _formModel.DataTableFlags &= ~flagVal;
        }

        StateHasChanged();
    }

    /// <summary>Map a Font Awesome glyph class to a MudBlazor Material icon.</summary>
    private static string MapGlyphToMudIcon(string? glyph)
    {
        if (string.IsNullOrEmpty(glyph))
            return Icons.Material.Filled.Flag;

        // Map common FA icons to Material equivalents
        if (glyph.Contains("check")) return Icons.Material.Filled.Check;
        if (glyph.Contains("eye")) return Icons.Material.Filled.Visibility;
        if (glyph.Contains("edit") || glyph.Contains("pencil")) return Icons.Material.Filled.Edit;
        if (glyph.Contains("trash") || glyph.Contains("delete")) return Icons.Material.Filled.Delete;
        if (glyph.Contains("plus") || glyph.Contains("add")) return Icons.Material.Filled.Add;
        if (glyph.Contains("search")) return Icons.Material.Filled.Search;
        if (glyph.Contains("filter")) return Icons.Material.Filled.FilterList;
        if (glyph.Contains("sort")) return Icons.Material.Filled.Sort;
        if (glyph.Contains("download")) return Icons.Material.Filled.Download;
        if (glyph.Contains("upload")) return Icons.Material.Filled.Upload;
        if (glyph.Contains("print")) return Icons.Material.Filled.Print;
        if (glyph.Contains("copy") || glyph.Contains("clone")) return Icons.Material.Filled.ContentCopy;
        if (glyph.Contains("lock")) return Icons.Material.Filled.Lock;
        if (glyph.Contains("cog") || glyph.Contains("gear") || glyph.Contains("settings")) return Icons.Material.Filled.Settings;
        if (glyph.Contains("list") || glyph.Contains("bars")) return Icons.Material.Filled.List;
        if (glyph.Contains("table")) return Icons.Material.Filled.TableChart;
        if (glyph.Contains("chart") || glyph.Contains("graph")) return Icons.Material.Filled.BarChart;
        if (glyph.Contains("refresh") || glyph.Contains("sync")) return Icons.Material.Filled.Refresh;
        if (glyph.Contains("info")) return Icons.Material.Filled.Info;
        if (glyph.Contains("warning") || glyph.Contains("exclamation")) return Icons.Material.Filled.Warning;
        if (glyph.Contains("times") || glyph.Contains("close")) return Icons.Material.Filled.Close;
        if (glyph.Contains("save")) return Icons.Material.Filled.Save;
        if (glyph.Contains("file")) return Icons.Material.Filled.Description;
        if (glyph.Contains("user") || glyph.Contains("person")) return Icons.Material.Filled.Person;
        if (glyph.Contains("calendar") || glyph.Contains("date")) return Icons.Material.Filled.CalendarToday;
        if (glyph.Contains("arrow")) return Icons.Material.Filled.ArrowForward;

        return Icons.Material.Filled.Flag;
    }

    public void Dispose()
    {
        AppState.OnCommand -= HandleCommand;
    }

    // ── Edit Model ──

    /// <summary>Form model for add/edit operations. Mirrors DataView entity fields.</summary>
    private sealed class DataViewEditModel
    {
        public string Title { get; set; } = string.Empty;
        public string DataSource { get; set; } = "Default";
        public string MainTable { get; set; } = string.Empty;
        public string Primarykey { get; set; } = string.Empty;
        public string ModificationProcedure { get; set; } = string.Empty;
        public string ViewProcedure { get; set; } = string.Empty;
        public string DeleteProcedure { get; set; } = string.Empty;
        public string ViewDescription { get; set; } = string.Empty;
        public string OrderBy { get; set; } = string.Empty;
        public string RowReorderColumn { get; set; } = string.Empty;
        public bool Published { get; set; }
        public int Flags { get; set; }
        public short DataTableModifierButtonStyle { get; set; }
        public int DataTableFlags { get; set; }
        public int DataTableDefaultPageSize { get; set; } = 10;
        public string DataTablePagingStyle { get; set; } = string.Empty;
    }

    /// <summary>The individual bits of a bitmask, as the service expects them.</summary>
    /// <remarks>
    /// The save sums a list of values, mirroring the legacy reading a posted checkbox group. The
    /// form holds the mask as one integer, so it is decomposed here rather than the service being
    /// changed — the sum of the bits is the mask it came from.
    /// </remarks>
    private static List<int> BitsOf(int mask)
    {
        var bits = new List<int>();

        for (var bit = 1; bit != 0 && bit <= mask; bit <<= 1)
            if ((mask & bit) != 0) bits.Add(bit);

        return bits;
    }
}
