using Microsoft.AspNetCore.Components;
using ASPClassic.Application.Services.Inc;
using ASPClassic.Application.Services.Data;
using ASPClassic.Application.DTOs.Inc;
using ASPClassic.Infrastructure;

namespace ASPClassic.Pages.Default;

/// <summary>Port of <c>default.asp</c> — Home landing page.</summary>
public partial class Default : ComponentBase, IDisposable
{
    [Inject] private IIncFooterJscriptsService IncFooterJscriptsService { get; set; } = default!;
    [Inject] private IIncConfigService IncConfigService { get; set; } = default!;
    [Inject] private IDataViewLookupClassExtendable DataViewLookupClassExtendable { get; set; } = default!;
    [Inject] private IDataViewLookupCollectionClass DataViewLookupCollectionClass { get; set; } = default!;
    [Inject] private IIncCrudeconstantsService IncCrudeconstantsService { get; set; } = default!;
    [Inject] private AppState AppState { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private string _pageTitle = "Home";

    protected override async Task OnInitializedAsync()
    {
        await LoadDefaultAsync();
        AppState.OnCommand += HandleCommandAsync;
    }

    /// <summary>
    /// Port of the top-level script block in default.asp.
    /// Legacy: sets constPageScriptName, strPageTitle, opens DB connection,
    /// includes inc_crudeconstants.asp for breadcrumb/constants initialization.
    /// In Blazor, DB connection is managed by DI/DbContextFactory. Page title
    /// and config values are loaded from services.
    /// </summary>
    private async Task LoadDefaultAsync()
    {
        // Legacy: Const constPageScriptName = "default.asp"
        // Legacy: strPageTitle = "Home"
        _pageTitle = "Home";

        // Legacy: adoConn.Open — DB connection is handled by DI, no manual open needed.

        // Legacy: <!--#include file="dist/asp/inc_crudeconstants.asp" -->
        // Initialize crudeconstants state (breadcrumb collection, constants).
        var crudeState = new IncCrudeconstantsStateDto
        {
            BreadCrumbCollection = string.Empty
        };
        await IncCrudeconstantsService.LoadIncCrudeconstantsAsync(crudeState);

        // Legacy: GetPageTitle() used by <title> tag — the page title is composed
        // from site name config + strPageTitle.
        var siteTitle = await IncConfigService.GetConfigValueAsync(
            "siteSettings", "title", "value", "title", "CrudeASP");
        if (!string.IsNullOrEmpty(siteTitle))
        {
            _pageTitle = $"{siteTitle} - Home";
        }
    }

    /// <summary>
    /// Handles toolbar/menu commands dispatched from MainLayout via AppState.
    /// The Default home page is the claims root / landing page. It responds to
    /// navigation commands that the shell toolbar may emit (e.g., navigating to
    /// admin views, browse, or dataview pages). Commands that don't apply to this
    /// page are ignored gracefully.
    /// Legacy: default.asp had no toolbar handlers itself, but the included
    /// inc_header.asp / inc_nav_top.asp rendered navigation links that targeted
    /// other .asp pages. Those navigation targets are now routed commands.
    /// </summary>
    private async Task HandleCommandAsync(string command)
    {
        switch (command)
        {
            case "navigate-admin-dataviews":
                NavigationManager.NavigateTo("/aspclassic-vbscript/admin-dataviews");
                break;

            case "navigate-admin-dataviewfields":
                NavigationManager.NavigateTo("/aspclassic-vbscript/admin-dataviewfields?ViewID=-1");
                break;

            case "navigate-browse":
                NavigationManager.NavigateTo("/aspclassic-vbscript/browse?ViewID=1");
                break;

            case "navigate-dataview":
                NavigationManager.NavigateTo("/aspclassic-vbscript/dataview?ViewID=1");
                break;

            case "navigate-view":
                NavigationManager.NavigateTo("/aspclassic-vbscript/view");
                break;

            case "refresh":
                // Reload the home page data (re-initialize constants and config)
                await LoadDefaultAsync();
                await InvokeAsync(StateHasChanged);
                break;

            case "navigate-home":
                // Already on home — just refresh
                await LoadDefaultAsync();
                await InvokeAsync(StateHasChanged);
                break;

            default:
                // Log unhandled commands to status bar for diagnostics
                AppState.LogStatus($"Home: unhandled command '{command}'");
                break;
        }
    }

    public void Dispose()
    {
        AppState.OnCommand -= HandleCommandAsync;
    }
}
