using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ASPClassic.Domain.Entities.Data;

namespace ASPClassic.Infrastructure.Helpers;

/// <summary>
/// Helper wrapping the portal.FormatValueForJson stored procedure logic for safely encoding
/// values into JSON output within DataView rendering.
/// <para>Legacy source: New abstraction — ported from inc_functions.asp FormatValueForJson
/// which escaped special characters for safe embedding in JSON strings.</para>
/// </summary>
public class JsonValueFormatter
{
    private readonly ILogger<JsonValueFormatter> _logger;

    public JsonValueFormatter(ILogger<JsonValueFormatter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Formats a raw string value for safe inclusion in a JSON string literal.
    /// Escapes backslashes, double quotes, control characters, newlines, carriage returns, and tabs.
    /// This replicates the legacy FormatValueForJson VBScript function behavior.
    /// </summary>
    public Task<string> FormatValueForJsonAsync(string value)
    {
        var result = SanitizeForJson(value);
        return Task.FromResult(result);
    }

    /// <summary>
    /// Synchronously escapes a raw string for safe JSON embedding.
    /// Handles: backslash, double quote, newline, carriage return, tab, and other control chars.
    /// </summary>
    public string SanitizeForJson(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(raw.Length + 16);

        foreach (var ch in raw)
        {
            switch (ch)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                case '\b':
                    sb.Append("\\b");
                    break;
                case '\f':
                    sb.Append("\\f");
                    break;
                default:
                    if (char.IsControl(ch))
                    {
                        // Encode other control characters as Unicode escapes
                        sb.Append("\\u");
                        sb.Append(((int)ch).ToString("x4"));
                    }
                    else
                    {
                        sb.Append(ch);
                    }
                    break;
            }
        }

        return sb.ToString();
    }
}
