using Microsoft.AspNetCore.Components;

namespace ASPClassic.Pages.Inc;

/// <summary>Port of <c>Inc_Meta</c> (inc_meta.asp).
/// The legacy include injected charset, viewport, Font Awesome, AdminLTE CSS, iCheck,
/// Pace, Summernote, Toastr, Google Fonts, jQuery, Bootstrap, DataTables, SlimScroll,
/// and FastClick into every page's &lt;head&gt;.
/// In Blazor Server those global resources are loaded once in _Host.cshtml / App.razor.
/// This component handles per-page dynamic head content: page title, meta description,
/// meta keywords, canonical URL, and any additional stylesheet a parent page requires.</summary>
public partial class IncMetaComponent : ComponentBase
{
    /// <summary>The page title rendered inside &lt;title&gt; via Blazor's PageTitle component.
    /// Legacy equivalent: the &lt;title&gt; tag set per-page in the ASP layout.</summary>
    [Parameter]
    public string PageTitle { get; set; } = "Portal";

    /// <summary>Optional meta description for the page.
    /// Legacy equivalent: not explicitly set in inc_meta.asp but supported for SEO parity.</summary>
    [Parameter]
    public string? MetaDescription { get; set; }

    /// <summary>Optional meta keywords for the page.</summary>
    [Parameter]
    public string? MetaKeywords { get; set; }

    /// <summary>Optional canonical URL for the page.
    /// Legacy equivalent: derived from Request.ServerVariables in the ASP pages.</summary>
    [Parameter]
    public string? CanonicalUrl { get; set; }

    /// <summary>Optional additional CSS stylesheet URL to inject into the page head.
    /// Legacy equivalent: page-specific CSS links that some ASP pages added alongside inc_meta.asp.
    /// For example, the legacy dataview pages added DataTables CSS; in Blazor this is handled
    /// by MudBlazor's built-in grid styling, but this parameter allows pages to add extra
    /// stylesheets if needed.</summary>
    [Parameter]
    public string? AdditionalCss { get; set; }

    /// <summary>Optional site root path override. In the legacy app, SITE_ROOT was a global
    /// VBScript constant used to prefix all asset URLs (e.g., &lt;%= SITE_ROOT %&gt;dist/css/adminlte.min.css).
    /// In Blazor, static assets use the standard wwwroot path. This parameter is retained for
    /// any component that needs to construct dynamic asset paths.</summary>
    [Parameter]
    public string SiteRoot { get; set; } = "/";

    /// <summary>Optional charset override. Legacy default was utf-8 via
    /// &lt;meta charset="utf-8"&gt; in inc_meta.asp. Blazor Server sets this in _Host.cshtml,
    /// but this parameter is available for components that render in non-standard contexts.</summary>
    [Parameter]
    public string Charset { get; set; } = "utf-8";

    /// <summary>Optional viewport content override. Legacy default was
    /// "width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no".
    /// Blazor Server sets viewport in _Host.cshtml; this is available for overrides.</summary>
    [Parameter]
    public string ViewportContent { get; set; } = "width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no";
}
