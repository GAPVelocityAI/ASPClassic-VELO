using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ASPClassic.Infrastructure.Data;

namespace ASPClassic.Application.Services.Inc;

/// <summary>
/// Port of <c>Inc_Config</c> (inc_config.asp).
/// The legacy code loaded a web.config XML file via MSXML DOM and searched for
/// &lt;add&gt; elements within a named section, matching a key attribute to return
/// the corresponding value attribute. In the modern stack, IConfiguration already
/// parses appsettings.json (and environment variables, etc.) so this service
/// delegates to IConfiguration with equivalent lookup semantics.
///
/// Legacy section mapping:
///   "connectionStrings" → ConnectionStrings:{attrName}
///   "appSettings"       → AppSettings:{attrName}
///   other sections      → {sectionName}:{attrName}
///
/// The keyAttrName/valAttrName parameters existed because the legacy XML had
/// &lt;add key="X" value="Y"/&gt; and the caller specified which XML attribute
/// held the key and which held the value. In IConfiguration this is flattened —
/// section:key → value — so keyAttrName and valAttrName are noted for
/// compatibility but the lookup is section:attrName.
/// </summary>
public class IncConfigService : IIncConfigService
{
    private readonly IDbContextFactory<ASPClassicVBScriptDbContext> _dbFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IncConfigService> _logger;

    public IncConfigService(
        IDbContextFactory<ASPClassicVBScriptDbContext> dbFactory,
        IConfiguration configuration,
        ILogger<IncConfigService> logger)
    {
        _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Port of legacy VBScript <c>GetConfigValue(sectionName, keyAttrName, valAttrName, attrName, defaultValue)</c>.
    ///
    /// Legacy logic:
    /// 1. Find the XML section node by sectionName (e.g. "appSettings", "connectionStrings").
    /// 2. Iterate child &lt;add&gt; elements.
    /// 3. For each, compare getAttribute(keyAttrName) to attrName.
    /// 4. On match, return getAttribute(valAttrName).
    /// 5. If no match, return defaultValue.
    ///
    /// Modern equivalent: IConfiguration flattens XML/JSON into section:key paths.
    /// - "connectionStrings" with keyAttrName="name" → ConnectionStrings:{attrName}
    /// - "appSettings" with keyAttrName="key" → AppSettings:{attrName}
    /// - Any other section → {sectionName}:{attrName}
    ///
    /// We try several resolution strategies in order of specificity:
    /// 1. Exact IConfiguration path based on section mapping.
    /// 2. If the section exists as a full IConfigurationSection, iterate its children
    ///    looking for a child whose key matches attrName (case-insensitive), then
    ///    read the sub-key matching valAttrName (handles nested structures).
    /// 3. Fall back to defaultValue.
    /// </summary>
    public async Task<string?> GetConfigValueAsync(
        string sectionName,
        string keyAttrName,
        string valAttrName,
        string attrName,
        string defaultValue,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        string? result = ResolveConfigValue(sectionName, keyAttrName, valAttrName, attrName, defaultValue);

        return await Task.FromResult(result);
    }

    /// <summary>
    /// Synchronous convenience wrapper matching the legacy <c>GetConfigValue</c> function signature.
    /// Port of <c>GetConfigValue(sectionName, keyAttrName, valAttrName, attrName, defaultValue)</c>.
    ///
    /// Legacy VBScript:
    /// <code>
    /// Function GetConfigValue(sectionName, keyAttrName, valAttrName, attrName, defaultValue)
    ///   Dim xmlDoc, xmlNode, xmlChildNodes, i
    ///   GetConfigValue = defaultValue
    ///   Set xmlDoc = Server.CreateObject("MSXML2.DOMDocument")
    ///   xmlDoc.async = false
    ///   xmlDoc.load(Server.MapPath("web.config"))
    ///   Set xmlNode = xmlDoc.selectSingleNode("//configuration/" &amp; sectionName)
    ///   If Not xmlNode Is Nothing Then
    ///     Set xmlChildNodes = xmlNode.childNodes
    ///     For i = 0 To xmlChildNodes.length - 1
    ///       If xmlChildNodes(i).getAttribute(keyAttrName) = attrName Then
    ///         GetConfigValue = xmlChildNodes(i).getAttribute(valAttrName)
    ///         Exit For
    ///       End If
    ///     Next
    ///   End If
    ///   Set xmlDoc = Nothing
    /// End Function
    /// </code>
    /// </summary>
    public string? GetConfigValue(
        string sectionName,
        string keyAttrName,
        string valAttrName,
        string attrName,
        string defaultValue)
    {
        return ResolveConfigValue(sectionName, keyAttrName, valAttrName, attrName, defaultValue);
    }

    /// <summary>
    /// Core resolution logic shared by both sync and async entry points.
    /// </summary>
    private string? ResolveConfigValue(
        string sectionName,
        string keyAttrName,
        string valAttrName,
        string attrName,
        string defaultValue)
    {
        // Strategy 1: Direct path resolution using known section mappings.
        // This mirrors the legacy pattern where "connectionStrings" section had
        // <add name="Default" connectionString="..." /> and "appSettings" had
        // <add key="SiteRootPath" value="/CrudePortal/" />.
        string? configPath = MapSectionToConfigPath(sectionName, attrName);
        string? value = null;

        if (configPath is not null)
        {
            value = _configuration[configPath];
        }

        if (value is not null)
        {
            _logger.LogDebug(
                "Config value resolved via direct path. Section={Section}, Key={Key}, Path={Path}",
                sectionName, attrName, configPath);
            return value;
        }

        // Strategy 2: Enumerate section children.
        // Legacy code iterated <add> elements under a section node. In IConfiguration,
        // a section can have named children. We look for a child whose key matches
        // attrName, then read either the child's value directly or a sub-key matching
        // valAttrName (e.g. "value", "connectionString").
        var section = _configuration.GetSection(sectionName);
        if (section.Exists())
        {
            foreach (var child in section.GetChildren())
            {
                // Case 1: child key matches attrName directly
                // (appSettings:SiteRootPath = "value")
                if (string.Equals(child.Key, attrName, StringComparison.OrdinalIgnoreCase))
                {
                    // If the child has a simple value, return it
                    if (child.Value is not null)
                    {
                        value = child.Value;
                        break;
                    }

                    // If the child is a section itself, look for valAttrName sub-key
                    // (e.g. connectionStrings:Default:connectionString)
                    var subValue = child[valAttrName];
                    if (subValue is not null)
                    {
                        value = subValue;
                        break;
                    }
                }

                // Case 2: child is an indexed element (0, 1, 2...) with sub-keys
                // matching the legacy XML attribute names.
                // E.g. section "connectionStrings" child "0" with sub-keys "name" and "connectionString".
                var childKeyAttr = child[keyAttrName];
                if (childKeyAttr is not null &&
                    string.Equals(childKeyAttr, attrName, StringComparison.OrdinalIgnoreCase))
                {
                    var childValAttr = child[valAttrName];
                    if (childValAttr is not null)
                    {
                        value = childValAttr;
                        break;
                    }
                }
            }
        }

        if (value is not null)
        {
            _logger.LogDebug(
                "Config value resolved via section enumeration. Section={Section}, Key={Key}",
                sectionName, attrName);
            return value;
        }

        // Strategy 3: Fall back to the provided default value — exact legacy behavior.
        _logger.LogDebug(
            "Config value not found, returning default. Section={Section}, Key={Key}, Default={Default}",
            sectionName, attrName, defaultValue);

        return defaultValue;
    }

    /// <summary>
    /// Maps legacy XML section names to IConfiguration paths.
    /// Legacy web.config had:
    ///   &lt;connectionStrings&gt;&lt;add name="Default" connectionString="..."/&gt;&lt;/connectionStrings&gt;
    ///   &lt;appSettings&gt;&lt;add key="SiteRootPath" value="/CrudePortal/"/&gt;&lt;/appSettings&gt;
    ///
    /// IConfiguration (appsettings.json) uses:
    ///   "ConnectionStrings": { "Default": "..." }
    ///   "AppSettings": { "SiteRootPath": "/CrudePortal/" }
    /// </summary>
    private static string? MapSectionToConfigPath(string sectionName, string attrName)
    {
        // The legacy code used exact section names from web.config.
        // Map them to the standard IConfiguration key paths.
        if (string.Equals(sectionName, "connectionStrings", StringComparison.OrdinalIgnoreCase))
        {
            // ASP.NET Core convention: ConnectionStrings:{name}
            return $"ConnectionStrings:{attrName}";
        }

        if (string.Equals(sectionName, "appSettings", StringComparison.OrdinalIgnoreCase))
        {
            // Custom convention: AppSettings:{key}
            return $"AppSettings:{attrName}";
        }

        // Generic fallback: {section}:{key}
        return $"{sectionName}:{attrName}";
    }
}
