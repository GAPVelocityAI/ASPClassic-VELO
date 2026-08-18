using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using ASPClassic.Domain.Entities.Data;

namespace ASPClassic.Infrastructure.Security;

/// <summary>
/// Utility for evaluating per-user DataView access rights, flag-based visibility,
/// and action authorization checks.
/// <para>Legacy source: New abstraction — the original ASP Classic app had no formal security
/// checks on DataView access. This helper provides a hook for future ACL enforcement.
/// Currently all views/actions are permitted (open access, matching legacy behavior).</para>
/// </summary>
public class DataViewSecurityHelper
{
    private readonly ILogger<DataViewSecurityHelper> _logger;

    public DataViewSecurityHelper(ILogger<DataViewSecurityHelper> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Determines whether the specified user can access the given DataView.
    /// Currently returns true for all users (matching legacy open-access behavior).
    /// </summary>
    public bool CanUserAccessView(int viewId, string userName)
    {
        // Legacy ASP Classic had no per-user DataView access control.
        // All published views were accessible to any authenticated user.
        // This method provides the extension point for future ACL enforcement.
        _logger.LogTrace("Access check: user '{UserName}' requesting view {ViewId} — GRANTED (open access).", userName, viewId);
        return true;
    }

    /// <summary>
    /// Determines whether the specified user is permitted to execute the given action.
    /// Currently returns true for all users (matching legacy open-access behavior).
    /// </summary>
    public bool IsActionPermitted(int actionId, string userName)
    {
        _logger.LogTrace("Action check: user '{UserName}' requesting action {ActionId} — GRANTED (open access).", userName, actionId);
        return true;
    }

    /// <summary>
    /// Filters a collection of viewIds to only those accessible by the specified user.
    /// Currently returns all viewIds unchanged (matching legacy open-access behavior).
    /// </summary>
    public IEnumerable<int> FilterAccessibleViews(IEnumerable<int> viewIds, string userName)
    {
        var viewIdList = viewIds.ToList();
        _logger.LogTrace("Filtering {Count} views for user '{UserName}' — all granted (open access).", viewIdList.Count, userName);
        return viewIdList;
    }
}
