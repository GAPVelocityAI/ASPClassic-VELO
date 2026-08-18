using Microsoft.AspNetCore.Components;
using ASPClassic.Application.Services.Page;
using ASPClassic.Application.Services.Admin;
using ASPClassic.Application.Services.Ajax;
using ASPClassic.Application.Services.Dataview;
using ASPClassic.Application.Services.Inc;
using ASPClassic.Application.Services.Sanitizer;
using ASPClassic.Infrastructure;
using ASPClassic.Application.Services.Data;

namespace ASPClassic.Pages.Page;

/// <summary>Port of <c>404</c> (404.asp).</summary>
public partial class Page404 : ComponentBase, IDisposable
{
    [Inject] private IPage404Service Page404Service { get; set; } = default!;
    [Inject] private IDataviewService DataviewService { get; set; } = default!;
    [Inject] private IIncFooterJscriptsService IncFooterJscriptsService { get; set; } = default!;
    [Inject] private IIncConfigService IncConfigService { get; set; } = default!;
    [Inject] private IDataViewLookupClassExtendable DataViewLookupClassExtendable { get; set; } = default!;
    [Inject] private IDataViewLookupCollectionClass DataViewLookupCollectionClass { get; set; } = default!;
    [Inject] private IIncCrudeconstantsService IncCrudeconstantsService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private AppState AppState { get; set; } = default!;

    private string _searchTerm = string.Empty;
    private string _redirectMessage = string.Empty;
    private bool _disposed;

    protected override async Task OnInitializedAsync()
    {
        AppState.OnCommand += HandleCommandAsync;

        // Port of legacy 404.asp top-level script:
        // The legacy code parses the query string to detect dataview routes
        // and redirects to the dataview page if a matching pattern is found.
        var uri = NavigationManager.Uri;
        var queryString = new Uri(uri).Query.TrimStart('?');
        var uriHost = new Uri(uri).Host;
        var uriScheme = new Uri(uri).Scheme;

        await Page404Service.LoadPage404Async(
            queryString,
            uriScheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? "on" : "off",
            new Uri(uri).Port.ToString(),
            uriHost);

        // Attempt dataview redirect logic ported from legacy pathStack parsing
        AttemptDataviewRedirect(queryString);
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender && !string.IsNullOrEmpty(_pendingRedirectUrl))
        {
            NavigationManager.NavigateTo(_pendingRedirectUrl, replace: true);
        }
    }

    private string? _pendingRedirectUrl;

    /// <summary>
    /// Port of legacy 404.asp pathStack redirect logic.
    /// Legacy code: splits the requested path on "/" and checks if pathStack(0) == "dataview",
    /// then reconstructs the URL with viewid, mode, and DT_ItemId query parameters.
    /// </summary>
    private void AttemptDataviewRedirect(string queryString)
    {
        if (string.IsNullOrEmpty(queryString))
            return;

        // Legacy: RequestedPath = Replace(LCase(Request.ServerVariables("QUERY_STRING")), LCase(BasePath & SITE_ROOT), "")
        // The 404 handler receives the original path in the query string after "404;"
        var requestedPath = queryString;

        // Strip the "404;" prefix and base path if present
        var semiIndex = requestedPath.IndexOf(';');
        if (semiIndex >= 0 && semiIndex < requestedPath.Length - 1)
        {
            requestedPath = requestedPath[(semiIndex + 1)..];
        }

        // Remove protocol and host portion if present
        if (requestedPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            requestedPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var parsed = new Uri(requestedPath);
                requestedPath = parsed.AbsolutePath.TrimStart('/');
            }
            catch
            {
                // Not a valid URI; continue with raw value
            }
        }

        requestedPath = requestedPath.Trim('/').ToLowerInvariant();

        var pathStack = requestedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (pathStack.Length > 1 && pathStack[0] == "dataview")
        {
            // Port of legacy SELECT CASE pathStack(0) = "dataview"
            var newUrl = $"/aspclassic-vbscript/dataview?viewid={pathStack[1]}";

            if (pathStack.Length > 2)
            {
                if (pathStack.Length > 3)
                {
                    newUrl += $"&mode={pathStack[2]}&DT_ItemId={pathStack[3]}";
                }
                else
                {
                    newUrl += $"&mode=edit&DT_ItemId={pathStack[2]}";
                }
            }

            _pendingRedirectUrl = newUrl;
            _redirectMessage = $"Redirecting to dataview: {pathStack[1]}...";
        }
    }

    /// <summary>
    /// Port of legacy search form submit — navigates to default page with search term.
    /// Legacy form had action posting to the same page; in Blazor we navigate to dashboard with search.
    /// </summary>
    private void OnSearchClick()
    {
        if (!string.IsNullOrWhiteSpace(_searchTerm))
        {
            NavigationManager.NavigateTo(
                $"/aspclassic-vbscript/default?search={Uri.EscapeDataString(_searchTerm)}");
        }
    }

    /// <summary>
    /// Handles global app commands dispatched from MainLayout or other components.
    /// Port of legacy form-level event handling integrated into the app state pattern.
    /// </summary>
    private async Task HandleCommandAsync(string command)
    {
        // 404 page does not have specific command handlers in the legacy source.
        // This is a no-op handler that allows page to participate in app-wide command dispatch.
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
