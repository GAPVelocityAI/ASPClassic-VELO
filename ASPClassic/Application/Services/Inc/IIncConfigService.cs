using System.Threading;
using System.Threading.Tasks;

namespace ASPClassic.Application.Services.Inc;

/// <summary>Port of <c>Inc_Config</c> (inc_config.asp) — XML configuration reader.</summary>
public interface IIncConfigService
{
    /// <summary>
    /// Reads a configuration value by section, key attribute name, value attribute name, and key value.
    /// Port of legacy <c>GetConfigValue(sectionName, keyAttrName, valAttrName, attrName, defaultValue)</c>.
    /// In the Blazor world, maps appSettings keys to IConfiguration sections.
    /// </summary>
    Task<string?> GetConfigValueAsync(
        string sectionName,
        string keyAttrName,
        string valAttrName,
        string attrName,
        string defaultValue,
        CancellationToken ct = default);

    /// <summary>
    /// Synchronous convenience wrapper matching the legacy <c>GetConfigValue</c> function signature.
    /// Port of <c>GetConfigValue(sectionName, keyAttrName, valAttrName, attrName, defaultValue)</c>.
    /// </summary>
    string? GetConfigValue(
        string sectionName,
        string keyAttrName,
        string valAttrName,
        string attrName,
        string defaultValue);
}
