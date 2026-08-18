using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using ASPClassic.Infrastructure;
using System.Globalization;
using ASPClassic.Domain.Entities.Data;

namespace ASPClassic.Pages.Inc;

/// <summary>Port of <c>inc_footer.asp</c> — shared footer component with admin sidebar.</summary>
public partial class IncFooterComponent : ComponentBase, IDisposable
{
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private AppState AppState { get; set; } = default!;

    /// <summary>Whether the current user has admin privileges (maps to legacy globalIsAdmin).</summary>
    [Parameter] public bool IsAdmin { get; set; }

    /// <summary>The current DataView ID when on a dataview page, used for context-sensitive admin links.</summary>
    [Parameter] public int? CurrentViewID { get; set; }

    /// <summary>Current script/page name, used by LoadIncFooter to determine context-sensitive rendering.</summary>
    [Parameter] public string ScriptName { get; set; } = string.Empty;

    private bool _adminDrawerOpen;
    private bool _isOnDataviewPage;

    /// <summary>Whether the current page is the dataview page (maps to legacy check of SCRIPT_NAME ending with /dataview.asp).</summary>
    private bool IsOnDataviewPage => _isOnDataviewPage;

    protected override void OnInitialized()
    {
        NavigationManager.LocationChanged += OnLocationChanged;
        EvaluateCurrentPage();
    }

    protected override void OnParametersSet()
    {
        EvaluateCurrentPage();
        LoadIncFooter(ScriptName);
    }

    /// <summary>
    /// Port of legacy LoadIncFooter(sCRIPTNAME).
    /// Legacy logic:
    ///   IF globalIsAdmin THEN
    ///     IF Right(Request.ServerVariables("SCRIPT_NAME"), Len("/dataview.asp")) = "/dataview.asp" THEN
    ///       — show context-specific admin links for the current DataView
    ///     END IF
    ///   END IF
    /// In Blazor, this is handled declaratively via IsAdmin and IsOnDataviewPage parameters,
    /// but we keep this method as the explicit port of the legacy entry point.
    /// </summary>
    private void LoadIncFooter(string scriptName)
    {
        if (!IsAdmin)
        {
            _isOnDataviewPage = false;
            return;
        }

        // Legacy: IF Right(Request.ServerVariables("SCRIPT_NAME"), Len("/dataview.asp")) = "/dataview.asp"
        // In Blazor, check if the current URI path ends with "/dataview" (the modern equivalent)
        if (!string.IsNullOrEmpty(scriptName))
        {
            _isOnDataviewPage = scriptName.EndsWith("/dataview.asp", StringComparison.OrdinalIgnoreCase)
                             || scriptName.EndsWith("/dataview", StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            EvaluateCurrentPage();
        }
    }

    private void EvaluateCurrentPage()
    {
        var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        var path = uri.AbsolutePath;
        _isOnDataviewPage = path.EndsWith("/dataview", StringComparison.OrdinalIgnoreCase)
                         || path.Contains("/dataview?", StringComparison.OrdinalIgnoreCase);

        // If on a dataview page and CurrentViewID was not explicitly set, try to extract from query string
        if (_isOnDataviewPage && !CurrentViewID.HasValue)
        {
            var queryString = uri.Query;
            if (!string.IsNullOrEmpty(queryString))
            {
                var queryParams = System.Web.HttpUtility.ParseQueryString(queryString);
                var viewIdStr = queryParams["ViewID"];
                if (int.TryParse(viewIdStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedViewId) && parsedViewId != 0)
                {
                    CurrentViewID = parsedViewId;
                }
            }
        }
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        EvaluateCurrentPage();
        InvokeAsync(StateHasChanged);
    }

    private void ToggleAdminDrawer()
    {
        _adminDrawerOpen = !_adminDrawerOpen;
    }

    /// <summary>
    /// Port of legacy GetWord function — returns the localized word for a given key.
    /// In the footer context, the keys are simple English labels.
    /// Falls back to the key itself if no translation is found.
    /// </summary>
    private string GetWord(string key)
    {
        // The legacy GetWord function performs dictionary lookup for i18n.
        // In this component context, the footer labels are simple English strings.
        // A full implementation would use IStringLocalizer, but since no .resx files
        // were provided for footer-specific strings, we return the key directly
        // which matches the legacy English-default behavior.
        return key;
    }

    public void Dispose()
    {
        NavigationManager.LocationChanged -= OnLocationChanged;
    }
}
