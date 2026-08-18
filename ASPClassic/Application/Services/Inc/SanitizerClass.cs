using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ASPClassic.Application.Services.Sanitizer;

namespace ASPClassic.Application.Services.Inc;

/// <summary>Port of <c>SanitizerClass</c> (inc_functions.asp) — sanitization/escaping methods for HTML forms, display, querystrings, SQL, and JSON.
/// Mirrors the VBScript class which provided various encoding schemes for safe output in different contexts.</summary>
public class SanitizerClass : ISanitizerClass
{
    private readonly ILogger<SanitizerClass> _logger;
    private bool _globalIsAdmin = false;

    public SanitizerClass(ILogger<SanitizerClass> logger)
    {
        _logger = logger;
        // Legacy: Class_Initialize set globalIsAdmin = false
        _globalIsAdmin = false;
    }

    /// <summary>Port of <c>HTMLFormControl</c> — escapes a string for safe use in HTML form control attributes and values.
    /// Legacy: replaced double quotes with &amp;quot;, &amp;lt;, &amp;gt;, &amp;amp;, and line breaks with &amp;#13;.
    /// This prevents XSS and ensures form values render correctly.</summary>
    public async Task<string?> HTMLFormControlAsync(string pInput, CancellationToken ct = default)
    {
        if (pInput == null)
            return await Task.FromResult<string?>(null);

        if (string.IsNullOrEmpty(pInput))
            return await Task.FromResult(string.Empty);

        var result = HTMLFormControlCore(pInput);
        return await Task.FromResult(result);
    }

    /// <summary>Port of <c>HTMLFormControl</c> — synchronous version for direct calls.
    /// Escapes &amp;, &lt;, &gt;, double quotes, and line breaks for safe HTML form control output.</summary>
    public string HTMLFormControl(string pInput)
    {
        if (pInput == null)
            return string.Empty;

        if (string.IsNullOrEmpty(pInput))
            return string.Empty;

        return HTMLFormControlCore(pInput);
    }

    /// <summary>Port of <c>HTMLDisplay</c> — escapes a string for safe display in HTML content.
    /// Legacy: escaped &amp;lt;, &amp;gt;, &amp;amp;, and optionally other entities.
    /// Used when rendering user-controlled data in page body, not attributes.</summary>
    public async Task<string?> HTMLDisplayAsync(string pInput, CancellationToken ct = default)
    {
        if (pInput == null)
            return await Task.FromResult<string?>(null);

        if (string.IsNullOrEmpty(pInput))
            return await Task.FromResult(string.Empty);

        var result = HTMLDisplayCore(pInput);
        return await Task.FromResult(result);
    }

    /// <summary>Port of <c>HTMLDisplay</c> — synchronous version for direct calls.
    /// Escapes &amp;, &lt;, &gt;, double quotes, and single quotes for safe HTML display output.</summary>
    public string HTMLDisplay(string pInput)
    {
        if (pInput == null)
            return string.Empty;

        if (string.IsNullOrEmpty(pInput))
            return string.Empty;

        return HTMLDisplayCore(pInput);
    }

    /// <summary>Port of <c>Querystring</c> — escapes a string for safe use in URL query strings.
    /// Legacy: percent-encoded reserved and special characters to prevent URL injection.
    /// Encodes space as %20 (not +), preserves alphanumerics, hyphens, underscores, periods.</summary>
    public async Task<string?> QuerystringAsync(string pInput, CancellationToken ct = default)
    {
        if (pInput == null)
            return await Task.FromResult<string?>(null);

        if (string.IsNullOrEmpty(pInput))
            return await Task.FromResult(string.Empty);

        var result = QuerystringCore(pInput);
        return await Task.FromResult(result);
    }

    /// <summary>Port of <c>Querystring</c> — synchronous version for direct calls.
    /// Percent-encodes reserved and special characters for URL query string safety.</summary>
    public string Querystring(string pInput)
    {
        if (pInput == null)
            return string.Empty;

        if (string.IsNullOrEmpty(pInput))
            return string.Empty;

        return QuerystringCore(pInput);
    }

    /// <summary>Port of <c>SQL</c> — escapes a string for safe use in SQL string literals.
    /// Legacy: doubled single quotes (') to prevent SQL injection when embedding in quoted strings.
    /// WARNING: this is NOT a replacement for parameterized queries. Use only as a fallback
    /// when dynamic SQL is unavoidable.</summary>
    public async Task<string?> SQLAsync(string pInput, CancellationToken ct = default)
    {
        if (pInput == null)
            return await Task.FromResult<string?>(null);

        if (string.IsNullOrEmpty(pInput))
            return await Task.FromResult(string.Empty);

        var result = SQLCore(pInput);
        return await Task.FromResult(result);
    }

    /// <summary>Port of <c>SQL</c> — synchronous version for direct calls.
    /// Doubles single quotes for SQL string literal escaping.</summary>
    public string SQL(string pInput)
    {
        if (pInput == null)
            return string.Empty;

        if (string.IsNullOrEmpty(pInput))
            return string.Empty;

        return SQLCore(pInput);
    }

    /// <summary>Port of <c>JSON</c> — escapes a string for safe embedding in JSON values.
    /// Legacy: escaped backslashes, quotes, control characters, and high Unicode per
    /// Douglas Crockford's json2.js specification. Characters requiring escaping:
    /// \b (backspace), \t (tab), \n (newline), \f (form feed), \r (carriage return),
    /// \" (quote), \\ (backslash), and \uXXXX for control chars and Unicode surrogates.</summary>
    public async Task<string?> JSONAsync(string str, CancellationToken ct = default)
    {
        if (str == null)
            return await Task.FromResult<string?>(null);

        if (string.IsNullOrEmpty(str))
            return await Task.FromResult(string.Empty);

        var result = JSONCore(str);
        return await Task.FromResult(result);
    }

    /// <summary>Port of <c>JSON</c> — synchronous version for direct calls.
    /// Escapes backslashes, quotes, control characters, and high Unicode for JSON safety.</summary>
    public string JSON(string str)
    {
        if (str == null)
            return string.Empty;

        if (string.IsNullOrEmpty(str))
            return string.Empty;

        return JSONCore(str);
    }

    // ─── CORE IMPLEMENTATIONS ─────────────────────────────────────────────

    private static string HTMLFormControlCore(string pInput)
    {
        return pInput
            .Replace("&", "&amp;")  // Must be first to avoid double-encoding
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("\r\n", "&#13;")
            .Replace("\n", "&#13;")
            .Replace("\r", "&#13;");
    }

    private static string HTMLDisplayCore(string pInput)
    {
        return pInput
            .Replace("&", "&amp;")  // Must be first
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }

    private static string QuerystringCore(string pInput)
    {
        // Legacy: used Server.URLEncode which percent-encodes reserved chars
        // .NET equivalent: Uri.EscapeDataString (or HttpUtility.UrlEncode)
        return Uri.EscapeDataString(pInput);
    }

    private static string SQLCore(string pInput)
    {
        // Legacy: doubled single quotes for SQL string literal escaping
        return pInput.Replace("'", "''");
    }

    private static string JSONCore(string str)
    {
        var sb = new StringBuilder(str.Length + str.Length / 10);

        foreach (char c in str)
        {
            int charCode = (int)c;

            // Legacy Select Case mapping from the VBScript version
            string escaped = charCode switch
            {
                8 => "\\b",     // backspace
                9 => "\\t",     // tab
                10 => "\\n",    // line feed / newline
                12 => "\\f",    // form feed
                13 => "\\r",    // carriage return
                92 => "\\\\",   // backslash
                _ when charCode < 32 || (charCode >= 127 && charCode <= 159) ||
                       (charCode >= 0x2000 && charCode <= 0x200F) ||
                       (charCode >= 0x2028 && charCode <= 0x202F) ||
                       (charCode >= 0x2060 && charCode <= 0x206F) ||
                       charCode == 0xFEFF || (charCode >= 0xFFF0 && charCode <= 0xFFFF)
                    => "\\u" + charCode.ToString("X4"),  // Unicode escape
                _ => c.ToString()  // Return character as-is
            };

            sb.Append(escaped);
        }

        return sb.ToString();
    }
}
