using System;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ASPClassic.Infrastructure;
using ASPClassic.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ASPClassic.Domain.Entities.Data;

namespace ASPClassic.Application.Services.Sanitizer;

/// <summary>Port of <c>SanitizerClass</c> from <c>inc_functions.asp</c>.
/// Provides string sanitization for HTML form controls, HTML display, querystrings, SQL, and JSON.
/// The legacy class cached a VBScript RegExp object in m_RegExp for JSON escaping;
/// here we use a compiled .NET Regex equivalent.</summary>
public class SanitizerClass : ISanitizerClass
{
    private readonly IDbContextFactory<ASPClassicVBScriptDbContext> _dbFactory;
    private readonly ILogger<SanitizerClass> _logger;
    private readonly AppState _appState;

    /// <summary>
    /// Compiled regex matching the same character classes as the legacy VBScript pattern:
    /// [\\\"\x00-\x1f\x7f-\x9f\u00ad\u0600-\u0604\u070f\u17b4\u17b5\u200c-\u200f\u2028-\u202f\u2060-\u206f\ufeff\ufff0-\uffff]
    /// See: https://github.com/douglascrockford/JSON-js/blob/43d7836c8ec9b31a02a31ae0c400bdae04d3650d/json2.js#L196
    /// </summary>
    private static readonly Regex JsonEscapeRegex = new Regex(
        @"[\\\""\x00-\x1f\x7f-\x9f\u00ad\u0600-\u0604\u070f\u17b4\u17b5\u200c-\u200f\u2028-\u202f\u2060-\u206f\ufeff\ufff0-\uffff]",
        RegexOptions.Compiled);

    public SanitizerClass(
        IDbContextFactory<ASPClassicVBScriptDbContext> dbFactory,
        ILogger<SanitizerClass> logger,
        AppState appState)
    {
        _dbFactory = dbFactory;
        _logger = logger;
        _appState = appState;
    }

    /// <summary>Port of <c>SanitizerClass.HTMLFormControl</c>.
    /// Legacy: IF IsNull(pInput) THEN pInput = ""; HTMLFormControl = Server.HTMLEncode(pInput)</summary>
    public async Task<string?> HTMLFormControlAsync(string pInput, CancellationToken ct = default)
    {
        await Task.CompletedTask;
        return HTMLFormControl(pInput);
    }

    /// <summary>Synchronous port of <c>SanitizerClass.HTMLFormControl</c>.
    /// Legacy: IF IsNull(pInput) THEN pInput = ""; HTMLFormControl = Server.HTMLEncode(pInput)</summary>
    public string HTMLFormControl(string pInput)
    {
        // Legacy: IF IsNull(pInput) THEN pInput = ""
        // Legacy: HTMLFormControl = Server.HTMLEncode(pInput)
        var input = pInput ?? string.Empty;
        return WebUtility.HtmlEncode(input);
    }

    /// <summary>Port of <c>SanitizerClass.HTMLDisplay</c>.
    /// Legacy: if not globalIsAdmin then HTMLEncode, else pass through raw.
    /// Maps globalIsAdmin to AppState.IsAdmin. When IsAdmin is true, the admin
    /// sees raw HTML (for rich-text/formatted content they manage). Non-admins
    /// get HTML-encoded output for XSS protection.</summary>
    public async Task<string?> HTMLDisplayAsync(string pInput, CancellationToken ct = default)
    {
        await Task.CompletedTask;
        return HTMLDisplay(pInput);
    }

    /// <summary>Synchronous port of <c>SanitizerClass.HTMLDisplay</c>.
    /// Legacy: if not globalIsAdmin then HTMLEncode, else pass through raw.</summary>
    public string HTMLDisplay(string pInput)
    {
        // Legacy: IF IsNull(pInput) THEN pInput = ""
        var input = pInput ?? string.Empty;

        // Legacy: IF NOT globalIsAdmin THEN
        //             HTMLDisplay = Server.HTMLEncode(pInput)
        //         ELSE
        //             HTMLDisplay = pInput
        //         END IF
        bool isAdmin = _appState.IsAdmin;

        if (!isAdmin)
        {
            return WebUtility.HtmlEncode(input);
        }
        else
        {
            return input;
        }
    }

    /// <summary>Port of <c>SanitizerClass.Querystring</c>.
    /// Legacy: IF IsNull(pInput) THEN pInput = ""; Querystring = Server.URLEncode(pInput)</summary>
    public async Task<string?> QuerystringAsync(string pInput, CancellationToken ct = default)
    {
        await Task.CompletedTask;
        return Querystring(pInput);
    }

    /// <summary>Synchronous port of <c>SanitizerClass.Querystring</c>.
    /// Legacy: IF IsNull(pInput) THEN pInput = ""; Querystring = Server.URLEncode(pInput)</summary>
    public string Querystring(string pInput)
    {
        // Legacy: IF IsNull(pInput) THEN pInput = ""
        var input = pInput ?? string.Empty;

        // Legacy: Querystring = Server.URLEncode(pInput)
        // Uri.EscapeDataString is the modern equivalent of Server.URLEncode,
        // encoding all characters except unreserved ones (RFC 3986).
        return Uri.EscapeDataString(input);
    }

    /// <summary>Port of <c>SanitizerClass.SQL</c>.
    /// Legacy: IF IsNull(pInput) THEN SQL = "NULL" ELSE SQL = REPLACE(pInput, "'", "''")</summary>
    public async Task<string?> SQLAsync(string pInput, CancellationToken ct = default)
    {
        await Task.CompletedTask;
        return SQL(pInput);
    }

    /// <summary>Synchronous port of <c>SanitizerClass.SQL</c>.
    /// Legacy: IF IsNull(pInput) THEN SQL = "NULL" ELSE SQL = REPLACE(pInput, "'", "''")</summary>
    public string SQL(string pInput)
    {
        // Legacy: IF IsNull(pInput) THEN SQL = "NULL"
        if (pInput == null)
        {
            return "NULL";
        }

        // Legacy: SQL = REPLACE(pInput, "'", "''")
        // Doubles single quotes to prevent SQL injection in dynamic SQL construction.
        // Note: in the modernized app, parameterized queries via EF Core are preferred,
        // but this method is preserved for cases where dynamic SQL is still constructed
        // (e.g., DataViewQueryEngine building ad-hoc queries from DataView metadata).
        return pInput.Replace("'", "''");
    }

    /// <summary>Port of <c>SanitizerClass.JSON</c>.
    /// Replicates the legacy VBScript JSON escaping logic character-by-character,
    /// using the same regex pattern from Douglas Crockford's json2.js.
    /// Handles: backslash, double-quote, newline, carriage return, backspace,
    /// and all other matched chars as \uXXXX unicode escapes.</summary>
    public async Task<string?> JSONAsync(string str, CancellationToken ct = default)
    {
        await Task.CompletedTask;
        return JSON(str);
    }

    /// <summary>Synchronous port of <c>SanitizerClass.JSON</c>.
    /// Replicates the legacy VBScript JSON escaping logic character-by-character.</summary>
    public string JSON(string str)
    {
        // Legacy: IF IsNull(Str) THEN JSON = "": Exit Function
        if (str == null)
        {
            return string.Empty;
        }

        // Legacy uses regex to find characters that need escaping, then iterates over
        // matches building a Parts() array with text-between-matches and escape sequences,
        // finally joining them. The .NET equivalent walks MatchCollection the same way.

        var matches = JsonEscapeRegex.Matches(str);

        // Legacy: If AnchorIndex = 1 Then JSON = Str: Exit Function
        // (no matches found — return original string unchanged)
        if (matches.Count == 0)
        {
            return str;
        }

        // Build result using StringBuilder, replicating the legacy Parts() array + Join("") approach
        var sb = new StringBuilder(str.Length + (matches.Count * 5));
        int anchorIndex = 0; // 0-based in .NET (was 1-based AnchorIndex in VBScript)

        foreach (Match match in matches)
        {
            int matchIndex = match.Index;

            // Legacy: Parts(NextPartIndex) = Mid(Str, AnchorIndex, MatchIndex - AnchorIndex)
            // Append text between previous anchor and this match start
            if (matchIndex > anchorIndex)
            {
                sb.Append(str, anchorIndex, matchIndex - anchorIndex);
            }

            // Legacy: CharCode = AscW(Mid(Str, MatchIndex, 1))
            char ch = str[matchIndex];
            int charCode = (int)ch;

            // Legacy Select Case for specific named escapes, then \uXXXX fallback
            // Case 34: \"  Case 10: \n  Case 13: \r  Case 92: \\  Case 8: \b  Case Else: \uXXXX
            string escaped = charCode switch
            {
                34 => "\\\"",
                10 => "\\n",
                13 => "\\r",
                92 => "\\\\",
                8  => "\\b",
                _  => "\\u" + charCode.ToString("X4", CultureInfo.InvariantCulture)
            };

            // Legacy: Parts(NextPartIndex) = Escaped
            sb.Append(escaped);

            // Legacy: AnchorIndex = MatchIndex + 1
            anchorIndex = matchIndex + 1;
        }

        // Legacy: Parts(NextPartIndex) = Mid(Str, AnchorIndex) — remaining text after last match
        if (anchorIndex < str.Length)
        {
            sb.Append(str, anchorIndex, str.Length - anchorIndex);
        }

        // Legacy: JSON = Join(Parts, "")
        return sb.ToString();
    }
}
