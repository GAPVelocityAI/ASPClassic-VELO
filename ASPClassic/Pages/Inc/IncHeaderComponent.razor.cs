using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ASPClassic.Pages.Inc;

/// <summary>Port of <c>Inc_Header</c> (inc_header.asp). Renders content header with page title and breadcrumb navigation.</summary>
public partial class IncHeaderComponent : ComponentBase
{
    /// <summary>The page title to display in the content header, equivalent to legacy strPageTitle.</summary>
    [Parameter]
    public string PageTitle { get; set; } = string.Empty;

    /// <summary>
    /// Breadcrumb items to render between "Home" and the active page title.
    /// Each item is a tuple of (Label, Href). These correspond to the legacy
    /// AddToBreadCrumbCollection / RenderBreadCrumbCollection calls.
    /// </summary>
    [Parameter]
    public List<BreadcrumbLinkItem>? BreadcrumbLinks { get; set; } = new List<BreadcrumbLinkItem>();

    /// <summary>Child content rendered inside the main content area.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Inject]
    private NavigationManager NavigationManager { get; set; } = default!;

    private List<BreadcrumbItem> _breadcrumbItems = new();

    protected override void OnParametersSet()
    {
        LoadIncHeader();
    }

    /// <summary>
    /// Port of LoadIncHeader. Builds the breadcrumb list from parameters.
    /// Legacy code rendered: Home link → breadcrumb collection items → active page title.
    /// </summary>
    private void LoadIncHeader()
    {
        _breadcrumbItems = new List<BreadcrumbItem>();

        // Home link — legacy: <a href="<%= SITE_ROOT %>default.asp">Home</a>
        _breadcrumbItems.Add(new BreadcrumbItem("Home", href: "/aspclassic-vbscript/default", icon: Icons.Material.Filled.Home));

        // Intermediate breadcrumb items from the legacy RenderBreadCrumbCollection()
        if (BreadcrumbLinks is not null)
        {
            foreach (var link in BreadcrumbLinks)
            {
                if (!string.IsNullOrWhiteSpace(link.Label))
                {
                    _breadcrumbItems.Add(new BreadcrumbItem(
                        link.Label,
                        href: string.IsNullOrWhiteSpace(link.Href) ? null : link.Href));
                }
            }
        }

        // Active page title (last item, no link) — legacy: <li class="breadcrumb-item active"><%= strPageTitle %></li>
        if (!string.IsNullOrWhiteSpace(PageTitle))
        {
            _breadcrumbItems.Add(new BreadcrumbItem(PageTitle, href: null, disabled: true));
        }
    }
}

/// <summary>
/// Represents a single breadcrumb link item passed to IncHeaderComponent.
/// Corresponds to entries added via the legacy AddToBreadCrumbCollection subroutine.
/// </summary>
public class BreadcrumbLinkItem
{
    public string Label { get; set; } = string.Empty;
    public string? Href { get; set; }

    public BreadcrumbLinkItem() { }

    public BreadcrumbLinkItem(string label, string? href = null)
    {
        Label = label;
        Href = href;
    }
}
