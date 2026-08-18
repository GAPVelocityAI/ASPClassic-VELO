using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace ASPClassic.Pages.Page;

/// <summary>Port of <c>500.asp</c> — 500 Server Error page.</summary>
public partial class Page500 : ComponentBase, IDisposable
{
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private string _pageTitle = string.Empty;
    private string _searchTerm = string.Empty;
    private List<BreadcrumbItem> _breadcrumbs = new();

    protected override void OnInitialized()
    {
        LoadPage500();
    }

    /// <summary>
    /// Port of the top-level script in 500.asp that sets page constants and title.
    /// </summary>
    private void LoadPage500()
    {
        // Legacy: Const constPageScriptName = "500.asp"
        // Legacy: strPageTitle = "500 Server Error"
        _pageTitle = "500 Server Error";

        _breadcrumbs = new List<BreadcrumbItem>
        {
            new BreadcrumbItem("Home", href: "/aspclassic-vbscript/default"),
            new BreadcrumbItem(_pageTitle, href: null, disabled: true)
        };
    }

    /// <summary>
    /// Handles the search button click. The legacy form had a search form that could
    /// submit — in Blazor we navigate to the default/dashboard page with the search term
    /// as a query parameter when a term is provided.
    /// </summary>
    private void OnSearchClick()
    {
        if (!string.IsNullOrWhiteSpace(_searchTerm))
        {
            NavigationManager.NavigateTo(
                $"/aspclassic-vbscript/default?search={Uri.EscapeDataString(_searchTerm.Trim())}");
        }
        else
        {
            Snackbar.Add("Please enter a search term.", Severity.Warning);
        }
    }

    public void Dispose()
    {
        // No resources to release
    }
}
