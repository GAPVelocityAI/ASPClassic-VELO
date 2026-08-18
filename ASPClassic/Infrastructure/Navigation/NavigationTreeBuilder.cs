using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ASPClassic.Infrastructure.Data;
using ASPClassic.Domain.Entities.Core;

namespace ASPClassic.Infrastructure.Navigation;

/// <summary>
/// Builds recursive navigation tree structures from flat Navigation table data,
/// used by the MDI shell sidebar.
/// <para>Legacy source: New abstraction — ported from inc_nav_side.asp recursive rendering logic
/// that built nested &lt;ul&gt; lists from the Navigation table.</para>
/// </summary>
public class NavigationTreeBuilder
{
    private readonly IDbContextFactory<ASPClassicVBScriptDbContext> _dbFactory;
    private readonly ILogger<NavigationTreeBuilder> _logger;

    public NavigationTreeBuilder(
        IDbContextFactory<ASPClassicVBScriptDbContext> dbFactory,
        ILogger<NavigationTreeBuilder> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <summary>
    /// Builds a navigation tree starting from the given parent ID (null for root-level nodes).
    /// Returns a virtual root node whose <see cref="NavigationNodeDto.Children"/> contain the top-level items.
    /// </summary>
    public async Task<NavigationNodeDto> BuildTreeAsync(int? parentNavId = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var allNavItems = await db.Navigations
            .AsNoTracking()
            .OrderBy(n => n.NavOrder)
            .ToListAsync();

        _logger.LogDebug("Loaded {Count} navigation items from database.", allNavItems.Count);

        // Grouped by parent, as a LOOKUP rather than a dictionary: a top-level item has no parent,
        // so its key is null — and a Dictionary rejects a null key outright, on Add and on
        // TryGetValue alike. Every root item has one, so building the tree threw every single time
        // and the sidebar rendered "No menu items available" for a menu that was fully populated.
        var lookup = allNavItems.ToLookup(n => n.NavParentId);

        var root = new NavigationNodeDto
        {
            NavId = 0,
            NavLabel = "Root",
            NavParentId = null,
            NavOrder = 0
        };

        root.Children = BuildChildrenRecursive(lookup, parentNavId);

        // An item whose parent does not exist belongs to no branch, so a tree built by walking down
        // from the roots never reaches it: the row is in the table, the menu is missing it, and
        // nothing anywhere says so. Confirmed live on an entry whose parent had been typed by hand.
        // Shown at the top level rather than dropped — being able to see it is what lets it be
        // corrected.
        if (parentNavId is null)
        {
            var known = allNavItems.Select(n => n.NavId).ToHashSet();

            var orphans = allNavItems
                .Where(n => n.NavParentId is int p && !known.Contains(p))
                .OrderBy(n => n.NavOrder)
                .ToList();

            if (orphans.Count > 0)
            {
                _logger.LogWarning(
                    "{Count} navigation item(s) name a parent that does not exist ({Items}); " +
                    "they are shown at the top level so they can be found and repaired.",
                    orphans.Count,
                    string.Join(", ", orphans.Select(o => $"{o.NavLabel}→{o.NavParentId}")));

                root.Children.AddRange(BuildChildrenRecursive(
                    orphans.ToLookup(_ => (int?)null), null));
            }
        }

        return root;
    }

    /// <summary>
    /// Flattens a navigation tree into a depth-first sequence for iteration.
    /// </summary>
    public IEnumerable<NavigationNodeDto> Flatten(NavigationNodeDto root)
    {
        yield return root;

        foreach (var child in root.Children)
        {
            foreach (var descendant in Flatten(child))
            {
                yield return descendant;
            }
        }
    }

    private List<NavigationNodeDto> BuildChildrenRecursive(
        ILookup<int?, Domain.Entities.Core.Navigation> lookup,
        int? parentId)
    {
        // A lookup answers a missing key — null included — with an empty sequence.
        var children = lookup[parentId].OrderBy(n => n.NavOrder).ToList();

        if (children.Count == 0)
        {
            return new List<NavigationNodeDto>();
        }

        var result = new List<NavigationNodeDto>();

        foreach (var nav in children)
        {
            var node = new NavigationNodeDto
            {
                NavId = nav.NavId,
                NavLabel = nav.NavLabel,
                NavParentId = nav.NavParentId,
                NavOrder = nav.NavOrder,
                NavUri = nav.NavUri ?? string.Empty,
                NavGlyph = nav.NavGlyph ?? string.Empty,
                NavTooltip = nav.NavTooltip ?? string.Empty,
                ViewID = nav.ViewID,
                OpenUriInIFRAME = nav.OpenUriInIFRAME
            };

            // Recursively build children for this node
            node.Children = BuildChildrenRecursive(lookup, nav.NavId);

            result.Add(node);
        }

        return result;
    }
}
