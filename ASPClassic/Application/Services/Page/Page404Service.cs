using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ASPClassic.Infrastructure.Data;
using ASPClassic.Application.DTOs.Core;

namespace ASPClassic.Application.Services.Page;

/// <summary>
/// Port of <c>404.asp</c>. Resolves friendly URLs from 404 redirects by parsing the
/// query string path segments and mapping them to dataview routes. If no friendly URL
/// match is found, prepares the 404 error page data.
/// </summary>
public class Page404Service : IPage404Service
{
    private readonly IDbContextFactory<ASPClassicVBScriptDbContext> _dbFactory;
    private readonly ILogger<Page404Service> _logger;

    public Page404Service(
        IDbContextFactory<ASPClassicVBScriptDbContext> dbFactory,
        ILogger<Page404Service> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task LoadPage404Async(
        string queryString,
        string https,
        string serverPort,
        string serverName,
        CancellationToken ct = default)
    {
        // Legacy: constPageScriptName = "404.asp"
        // Legacy: strPageTitle = "404 Page Not Found"
        const string pageTitle = "404 Page Not Found";

        var result = new Page404ResultDto
        {
            PageTitle = pageTitle
        };

        // Legacy: Dim query_string : query_string = request.ServerVariables("QUERY_STRING")
        // Legacy: if query_string <> "" then query_string = "?" & query_string end if
        // (The query_string variable in legacy was used only for link construction;
        //  the actual parsing uses the raw QUERY_STRING below.)

        // Legacy: IF Request.ServerVariables("HTTPS") = "off" THEN
        //           ServerProtocol = "http://" ELSE ServerProtocol = "https://"
        string serverProtocol = string.Equals(https, "off", StringComparison.OrdinalIgnoreCase)
            ? "http://"
            : "https://";

        // Legacy: IF Request.ServerVariables("SERVER_PORT") <> "80" THEN
        //           ServerPort = ":" & Request.ServerVariables("SERVER_PORT")
        //         ELSE ServerPort = ""
        string portSuffix = !string.Equals(serverPort, "80", StringComparison.Ordinal)
            ? ":" + serverPort
            : string.Empty;

        // Legacy: Dim BasePath : BasePath = "404;" & ServerProtocol &
        //           request.ServerVariables("SERVER_NAME") & ServerPort
        string basePath = "404;" + serverProtocol + serverName + portSuffix;
        result.BasePath = basePath;

        // Legacy: Dim RequestedPath : RequestedPath = Replace(
        //           LCase(Request.ServerVariables("QUERY_STRING")),
        //           LCase(BasePath & SITE_ROOT), "")
        string basePathWithRoot = basePath.ToLowerInvariant();
        string requestedPath = (queryString ?? string.Empty).ToLowerInvariant()
            .Replace(basePathWithRoot, string.Empty);
        result.RequestedPath = requestedPath;

        _logger.LogInformation(
            "404 handler invoked. QueryString={QueryString}, BasePath={BasePath}, RequestedPath={RequestedPath}",
            queryString, basePath, requestedPath);

        // Legacy: Dim pathStack : pathStack = Split(RequestedPath, "/")
        // Legacy: IF UBound(pathStack) > 0 THEN ...
        string[] pathStack = requestedPath.Split('/', StringSplitOptions.None);

        // VBScript UBound returns the highest index (length - 1).
        // The condition UBound(pathStack) > 0 means at least 2 segments.
        if (pathStack.Length > 1)
        {
            // Legacy: SELECT CASE pathStack(0)
            //           case "dataview"
            string firstSegment = pathStack[0].Trim();

            if (string.Equals(firstSegment, "dataview", StringComparison.OrdinalIgnoreCase))
            {
                // Legacy: newURL = SITE_ROOT & "dataview.asp?viewid=" & pathStack(1)
                // In the Blazor app, the dataview page route is /aspclassic-vbscript/dataview
                string viewIdSegment = pathStack.Length > 1 ? pathStack[1] : string.Empty;

                // Validate that viewId segment exists in the database before redirecting
                if (int.TryParse(viewIdSegment, NumberStyles.Integer, CultureInfo.InvariantCulture, out int viewId))
                {
                    await using var db = await _dbFactory.CreateDbContextAsync(ct);
                    bool viewExists = await db.DataViews
                        .AsNoTracking()
                        .AnyAsync(dv => dv.ViewID == viewId, ct);

                    if (viewExists)
                    {
                        string newUrl = "/aspclassic-vbscript/dataview?viewid=" + viewIdSegment;

                        // Legacy: IF UBound(pathStack) > 1 THEN
                        if (pathStack.Length > 2)
                        {
                            // Legacy: IF UBound(pathStack) > 2 THEN
                            if (pathStack.Length > 3)
                            {
                                // Legacy: newURL = newURL & "&mode=" & pathStack(2) &
                                //           "&DT_ItemId=" & pathStack(3)
                                newUrl = newUrl + "&mode=" + pathStack[2]
                                                + "&DT_ItemId=" + pathStack[3];
                            }
                            else
                            {
                                // Legacy: newURL = newURL & "&mode=edit&DT_ItemId=" & pathStack(2)
                                newUrl = newUrl + "&mode=edit&DT_ItemId=" + pathStack[2];
                            }
                        }

                        _logger.LogInformation(
                            "404 handler resolved friendly URL. Redirecting to {RedirectUrl}",
                            newUrl);

                        result.RedirectUrl = newUrl;
                        return;
                    }

                    _logger.LogWarning(
                        "404 handler: dataview friendly URL referenced non-existent ViewID={ViewId}",
                        viewId);
                }
                else
                {
                    _logger.LogWarning(
                        "404 handler: dataview friendly URL had non-numeric ViewID segment '{Segment}'",
                        viewIdSegment);
                }
            }
            else
            {
                _logger.LogDebug(
                    "404 handler: path segment '{Segment}' did not match any known friendly URL pattern",
                    firstSegment);
            }
        }

        // No redirect resolved — the 404 page should be displayed.
        // Legacy: the page then renders the 404 HTML with inc_meta, inc_header, inc_footer includes.
        // That rendering is handled by the Page404.razor component; this service just returns the data.
        _logger.LogInformation(
            "404 handler: no friendly URL match. Displaying 404 page for RequestedPath={RequestedPath}",
            requestedPath);
    }
}
