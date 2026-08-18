using Microsoft.AspNetCore.Components;
using ASPClassic.Application.Services.Inc;
using ASPClassic.Infrastructure;

namespace ASPClassic.Pages.Blank;

/// <summary>Port of <c>blank.asp</c> (Blank starter template page).</summary>
public partial class Blank : ComponentBase, IDisposable
{
    [Inject] private IIncFooterJscriptsService IncFooterJscriptsService { get; set; } = default!;
    [Inject] private IIncConfigService IncConfigService { get; set; } = default!;
    [Inject] private AppState AppState { get; set; } = default!;

    private string _pageTitle = "New Page";
    private bool _disposed;

    protected override async Task OnInitializedAsync()
    {
        AppState.OnCommand += HandleCommandAsync;
        await LoadBlankAsync();
    }

    /// <summary>
    /// Port of the legacy LoadBlank() method.
    /// Legacy code set constants, page title, and opened the DB connection.
    /// In Blazor Server, the DB connection is managed by EF Core via DI;
    /// we replicate the page title assignment and any config loading.
    /// </summary>
    private async Task LoadBlankAsync()
    {
        // Legacy: Const constPageScriptName = "blank.asp"
        // Legacy: strPageTitle = "New Page"
        _pageTitle = "New Page";

        // Legacy: config was loaded via inc_config.asp include.
        // Load the portal title from config if available, otherwise keep default.
        var portalTitle = await IncConfigService.GetConfigValueAsync(
            "portal", "title", "value", "title", "Portal", default);

        if (!string.IsNullOrEmpty(portalTitle))
        {
            _pageTitle = portalTitle;
        }

        // Legacy: adoConn.Open — DB connection is managed by EF Core DI, no manual open needed.
    }

    private async Task HandleCommandAsync(string command)
    {
        // Blank page has no toolbar commands to handle,
        // but we subscribe to follow the AppState pattern.
        await Task.CompletedTask;
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
