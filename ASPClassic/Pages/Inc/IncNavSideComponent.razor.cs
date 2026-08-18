using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;
using ASPClassic.Infrastructure.Navigation;
using ASPClassic.Domain.Entities.Core;
using ASPClassic.Pages.Default;

namespace ASPClassic.Pages.Inc;

/// <summary>Port of <c>inc_nav_side.asp</c> — Main sidebar navigation component.
/// Builds a recursive navigation tree from the Navigation table and renders
/// an expandable/collapsible sidebar menu with icons and links.</summary>
public partial class IncNavSideComponent : ComponentBase, IDisposable
{
    [Inject] private NavigationTreeBuilder NavigationTreeBuilder { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private ILogger<IncNavSideComponent> Logger { get; set; } = default!;
    [Inject] private ASPClassic.Application.Services.Dataview.IDataviewService DataviewService { get; set; } = default!;

    /// <summary>
    /// The published views, listed straight from the DataView table.
    /// </summary>
    /// <remarks>
    /// Navigation is a menu somebody curates, and nothing adds a link to it when a view is created
    /// — that is true of the legacy too. Listing the views separately means a view added today is
    /// reachable today, without a second, manual step that is easy to forget.
    /// </remarks>
    private List<ASPClassic.Application.DTOs.Data.DataViewDto> _dataViews = new();

    private List<NavigationNodeDto> _navigationNodes = new();
    private HashSet<int> _expandedNodes = new();
    private bool _isLoading = true;
    private bool _disposed;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _isLoading = true;
            var rootNode = await NavigationTreeBuilder.BuildTreeAsync(null);

            // The root node itself is a virtual root; its children are the top-level menu items
            if (rootNode?.Children is not null && rootNode.Children.Count > 0)
            {
                _navigationNodes = new List<NavigationNodeDto>(rootNode.Children);
            }
            else
            {
                _navigationNodes = new List<NavigationNodeDto>();
            }

        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load sidebar navigation tree");
            _navigationNodes = new List<NavigationNodeDto>();
        }
        finally
        {
            _isLoading = false;
        }

        // Loaded separately, and after the tree, on purpose. Sharing the try above meant a failure
        // building the tree skipped this too, so one broken menu silently emptied the other.
        await ReloadAsync();
    }

    /// <summary>Toggles the expanded/collapsed state of a parent navigation node.</summary>
    private void ToggleNode(int navId)
    {
        if (!_expandedNodes.Remove(navId))
        {
            _expandedNodes.Add(navId);
        }
    }

    /// <summary>Navigates to the target of a leaf navigation node.
    /// If the node has a ViewID, navigates to the dataview page.
    /// If it has a NavUri, navigates to that URI.
    /// Otherwise navigates to the default page (port of the legacy brand link to default.asp).</summary>
    private void NavigateToNode(NavigationNodeDto node)
    {
        var href = ResolveNavUri(node);
        if (!string.IsNullOrWhiteSpace(href))
        {
            Logger.LogInformation("Sidebar navigation to {Label} → {Href}", node.NavLabel, href);
            NavigationManager.NavigateTo(href);
        }
    }

    /// <summary>Resolves the navigation URI for a node based on ViewID or NavUri.</summary>
    private static string ResolveNavUri(NavigationNodeDto node)
    {
        // If the node has a ViewID, link to the dataview page with viewID parameter
        if (node.ViewID.HasValue && node.ViewID.Value > 0)
        {
            if (node.OpenUriInIFRAME)
            {
                // Legacy opened in iframe; in Blazor we navigate to the view page
                return $"/aspclassic-vbscript/view?navID={node.NavId}";
            }
            return $"/aspclassic-vbscript/dataview?viewID={node.ViewID.Value}";
        }

        // If the node has a direct URI, use it
        if (!string.IsNullOrWhiteSpace(node.NavUri))
        {
            var uri = node.NavUri.Trim();

            // Convert legacy .asp references to Blazor routes
            if (uri.Contains("default.asp", StringComparison.OrdinalIgnoreCase))
            {
                return "/aspclassic-vbscript/default";
            }
            if (uri.Contains("browse.asp", StringComparison.OrdinalIgnoreCase))
            {
                return "/aspclassic-vbscript/browse";
            }
            if (uri.Contains("admin_dataviews.asp", StringComparison.OrdinalIgnoreCase))
            {
                return "/aspclassic-vbscript/admin-dataviews";
            }
            if (uri.Contains("admin_dataviewfields.asp", StringComparison.OrdinalIgnoreCase))
            {
                return "/aspclassic-vbscript/admin-dataviewfields";
            }

            // For absolute URIs (http/https), return as-is
            if (uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return uri;
            }

            // For relative URIs, prefix with site root
            if (!uri.StartsWith('/'))
            {
                uri = "/" + uri;
            }
            return uri;
        }

        // Fallback to default page (matches legacy brand-link to default.asp)
        return "/aspclassic-vbscript/default";
    }

    /// <summary>Maps a legacy FontAwesome or glyph class name to a MudBlazor Material icon.
    /// The legacy markup used FontAwesome classes like "fas fa-tint", "fas fa-cog", etc.</summary>
    private static string MapGlyphToIcon(string glyphClass)
    {
        if (string.IsNullOrWhiteSpace(glyphClass))
            return Icons.Material.Filled.Circle;

        var glyph = glyphClass.Trim().ToLowerInvariant();

        // Map common FontAwesome icons used in the legacy portal to Material icons
        if (glyph.Contains("fa-home")) return Icons.Material.Filled.Home;
        if (glyph.Contains("fa-cog") || glyph.Contains("fa-gear")) return Icons.Material.Filled.Settings;
        if (glyph.Contains("fa-cogs") || glyph.Contains("fa-gears")) return Icons.Material.Filled.Tune;
        if (glyph.Contains("fa-user")) return Icons.Material.Filled.Person;
        if (glyph.Contains("fa-users")) return Icons.Material.Filled.People;
        if (glyph.Contains("fa-table")) return Icons.Material.Filled.TableChart;
        if (glyph.Contains("fa-database")) return Icons.Material.Filled.Storage;
        if (glyph.Contains("fa-chart") || glyph.Contains("fa-bar-chart")) return Icons.Material.Filled.BarChart;
        if (glyph.Contains("fa-pie-chart")) return Icons.Material.Filled.PieChart;
        if (glyph.Contains("fa-line-chart")) return Icons.Material.Filled.ShowChart;
        if (glyph.Contains("fa-file")) return Icons.Material.Filled.Description;
        if (glyph.Contains("fa-folder")) return Icons.Material.Filled.Folder;
        if (glyph.Contains("fa-search")) return Icons.Material.Filled.Search;
        if (glyph.Contains("fa-plus")) return Icons.Material.Filled.Add;
        if (glyph.Contains("fa-edit") || glyph.Contains("fa-pencil")) return Icons.Material.Filled.Edit;
        if (glyph.Contains("fa-trash") || glyph.Contains("fa-remove")) return Icons.Material.Filled.Delete;
        if (glyph.Contains("fa-eye")) return Icons.Material.Filled.Visibility;
        if (glyph.Contains("fa-download")) return Icons.Material.Filled.Download;
        if (glyph.Contains("fa-upload")) return Icons.Material.Filled.Upload;
        if (glyph.Contains("fa-envelope") || glyph.Contains("fa-mail")) return Icons.Material.Filled.Email;
        if (glyph.Contains("fa-bell")) return Icons.Material.Filled.Notifications;
        if (glyph.Contains("fa-calendar")) return Icons.Material.Filled.CalendarMonth;
        if (glyph.Contains("fa-clock")) return Icons.Material.Filled.Schedule;
        if (glyph.Contains("fa-lock")) return Icons.Material.Filled.Lock;
        if (glyph.Contains("fa-key")) return Icons.Material.Filled.Key;
        if (glyph.Contains("fa-star")) return Icons.Material.Filled.Star;
        if (glyph.Contains("fa-bookmark")) return Icons.Material.Filled.Bookmark;
        if (glyph.Contains("fa-tag")) return Icons.Material.Filled.Label;
        if (glyph.Contains("fa-tags")) return Icons.Material.Filled.LocalOffer;
        if (glyph.Contains("fa-list")) return Icons.Material.Filled.List;
        if (glyph.Contains("fa-th")) return Icons.Material.Filled.GridView;
        if (glyph.Contains("fa-tint")) return Icons.Material.Filled.Water;
        if (glyph.Contains("fa-wrench")) return Icons.Material.Filled.Build;
        if (glyph.Contains("fa-globe")) return Icons.Material.Filled.Public;
        if (glyph.Contains("fa-link")) return Icons.Material.Filled.Link;
        if (glyph.Contains("fa-sitemap")) return Icons.Material.Filled.AccountTree;
        if (glyph.Contains("fa-dashboard") || glyph.Contains("fa-tachometer")) return Icons.Material.Filled.Dashboard;
        if (glyph.Contains("fa-sign-out") || glyph.Contains("fa-power-off")) return Icons.Material.Filled.Logout;
        if (glyph.Contains("fa-info")) return Icons.Material.Filled.Info;
        if (glyph.Contains("fa-question")) return Icons.Material.Filled.Help;
        if (glyph.Contains("fa-exclamation") || glyph.Contains("fa-warning")) return Icons.Material.Filled.Warning;
        if (glyph.Contains("fa-check")) return Icons.Material.Filled.Check;
        if (glyph.Contains("fa-times") || glyph.Contains("fa-close")) return Icons.Material.Filled.Close;
        if (glyph.Contains("fa-arrow-right") || glyph.Contains("fa-chevron-right")) return Icons.Material.Filled.ChevronRight;
        if (glyph.Contains("fa-arrow-left") || glyph.Contains("fa-chevron-left")) return Icons.Material.Filled.ChevronLeft;
        if (glyph.Contains("fa-refresh") || glyph.Contains("fa-sync")) return Icons.Material.Filled.Refresh;
        if (glyph.Contains("fa-print")) return Icons.Material.Filled.Print;
        if (glyph.Contains("fa-save") || glyph.Contains("fa-floppy")) return Icons.Material.Filled.Save;
        if (glyph.Contains("fa-copy") || glyph.Contains("fa-clone")) return Icons.Material.Filled.ContentCopy;
        if (glyph.Contains("fa-filter")) return Icons.Material.Filled.FilterList;
        if (glyph.Contains("fa-sort")) return Icons.Material.Filled.Sort;
        if (glyph.Contains("fa-map")) return Icons.Material.Filled.Map;
        if (glyph.Contains("fa-image") || glyph.Contains("fa-picture")) return Icons.Material.Filled.Image;
        if (glyph.Contains("fa-comment")) return Icons.Material.Filled.Comment;
        if (glyph.Contains("fa-share")) return Icons.Material.Filled.Share;

        // Default fallback
        return Icons.Material.Filled.Circle;
    }

    public void Dispose()
    {
        _disposed = true;
    }

    /// <summary>Re-reads the view list, so one added a moment ago shows up.</summary>
    public async Task ReloadAsync()
    {
        try
        {
            _dataViews = await DataviewService.GetNavigableDataViewsAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Reloading the sidebar's data view list failed.");
        }
    }
}
