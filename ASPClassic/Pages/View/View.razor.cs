using System.Globalization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using ASPClassic.Application.DTOs.Core;
using ASPClassic.Application.Services.View;
using ASPClassic.Application.Services.Inc;
using ASPClassic.Infrastructure;
using ASPClassic.Domain.Entities.Data;
using ASPClassic.Domain.Entities.Core;

namespace ASPClassic.Pages.View;

/// <summary>Port of <c>view.asp</c> — resolves a NavID query parameter to a DataView page or external NavUri.</summary>
public partial class View : ComponentBase, IDisposable
{
    [Inject] private IViewService ViewService { get; set; } = default!;
    [Inject] private IIncFooterJscriptsService FooterJscriptsService { get; set; } = default!;
    [Inject] private IIncConfigService ConfigService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private AppState AppState { get; set; } = default!;
    [Inject] private ILogger<View> Logger { get; set; } = default!;

    /// <summary>NavID passed via query string — maps to legacy Request("NavID").</summary>
    [SupplyParameterFromQuery(Name = "NavID")]
    public string? NavID { get; set; }

    private NavigationDto? _navigationResult;
    private string _pageTitle = "View";
    private string _resolvedUri = string.Empty;
    private bool _hasDataView;
    private bool _isLoading = true;
    private string? _errorMessage;
    private bool _disposed;

    protected override void OnInitialized() => AppState.OnCommand += HandleCommandAsync;

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
        if (NavID == _loadedFor) return;
        _loadedFor = NavID;

        await LoadViewAsync(NavID);
    }

    /// <summary>Port of legacy top-level script block that reads NavID and queries Navigation table.</summary>
    private async Task LoadViewAsync(string? navID)
    {
        _isLoading = true;
        _errorMessage = null;
        _navigationResult = null;
        _hasDataView = false;
        _resolvedUri = string.Empty;
        _pageTitle = "View";

        try
        {
            if (string.IsNullOrWhiteSpace(navID))
            {
                // Legacy: if NavID is empty or non-numeric, strPageURL defaults to "404.asp"
                Logger.LogInformation("View page loaded without NavID — no navigation item to resolve.");
                _isLoading = false;
                return;
            }

            // Legacy: IF NOT IsNumeric(nNavID) THEN nNavID = ""
            if (!int.TryParse(navID, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                Logger.LogWarning("View page received non-numeric NavID: {NavID}", navID);
                _errorMessage = "Invalid navigation identifier.";
                _isLoading = false;
                return;
            }

            // Call service — equivalent to legacy SQL: SELECT * FROM portal.Navigation WHERE NavId = nNavID
            var nav = await ViewService.LoadViewAsync(navID);

            if (nav is null)
            {
                // Legacy: if rsItems.EOF, strPageURL stays "404.asp"
                Logger.LogWarning("No navigation record found for NavID={NavID}", navID);
                _errorMessage = "Navigation item not found.";
                _isLoading = false;
                return;
            }

            _navigationResult = nav;
            _pageTitle = nav.NavLabel;

            // Legacy logic:
            // IF IsNumeric(rsItems("ViewID")) THEN
            //     strPageURL = "dataview.asp?ViewID=" & rsItems("ViewID")
            // ELSE
            //     strPageURL = rsItems("NavUri")
            // END IF
            // A view id is non-zero, not positive — the built-in views are negative.
            if (nav.ViewID != 0)
            {
                _hasDataView = true;
                _resolvedUri = $"/aspclassic-vbscript/dataview?ViewID={nav.ViewID}";
            }
            else if (!string.IsNullOrEmpty(nav.NavUri))
            {
                _hasDataView = false;
                _resolvedUri = nav.NavUri;
            }

            AppState.LogStatus($"Loaded view: {nav.NavLabel}");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading view for NavID={NavID}", navID);
            _errorMessage = "An error occurred while loading the navigation item.";
            Snackbar.Add("Failed to load view.", Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>Port of legacy GetNavigation — returns current navigation DTO.</summary>
    private NavigationDto? GetNavigation()
    {
        return _navigationResult;
    }

    /// <summary>Navigate to the resolved DataView page — replaces legacy iframe src="dataview.asp?ViewID=N".</summary>
    private void NavigateToDataView()
    {
        if (_hasDataView && !string.IsNullOrEmpty(_resolvedUri))
        {
            NavigationManager.NavigateTo(_resolvedUri);
        }
    }

    /// <summary>Handles commands dispatched from MainLayout toolbar/menu via AppState.</summary>
    private async Task HandleCommandAsync(string command)
    {
        switch (command)
        {
            case "refresh":
                await LoadViewAsync(NavID);
                await InvokeAsync(StateHasChanged);
                break;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            AppState.OnCommand -= HandleCommandAsync;
            _disposed = true;
        }
    }
}
