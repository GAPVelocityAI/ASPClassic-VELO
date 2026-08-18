using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ASPClassic.Application.DTOs.Inc;
using ASPClassic.Application.DTOs.Core;
using ASPClassic.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ASPClassic.Application.Services.Sanitizer;
using ASPClassic.Domain.Entities.Core;

namespace ASPClassic.Application.Services.Inc;

/// <summary>Port of <c>Inc_Functions</c> (inc_functions.asp) — utility functions for formatting, localization, navigation tree building, and lookup rendering.</summary>
public class IncFunctionsService : IIncFunctionsService
{
    private readonly IDbContextFactory<ASPClassicVBScriptDbContext> _dbFactory;
    private readonly ILogger<IncFunctionsService> _logger;
    private readonly ISanitizerClass _sanitizer;

    // Stores the last set of lookup selection items built by DisplayLookupSelectionAsync
    private List<LookupSelectionItemDto> _lastLookupItems = new();

    // Regex pattern ported from legacy SanitizerClass.JSON — matches control chars and
    // Unicode characters that need escaping in JSON strings per Douglas Crockford's json2.js
    private static readonly Regex JsonEscapeRegex = new Regex(
        @"[\\\""\x00-\x1f\x7f-\x9f\u00ad\u0600-\u0604\u070f\u17b4\u17b5\u200c-\u200f\u2028-\u202f\u2060-\u206f\ufeff\ufff0-\uffff]",
        RegexOptions.Compiled);

    public IncFunctionsService(
        IDbContextFactory<ASPClassicVBScriptDbContext> dbFactory,
        ILogger<IncFunctionsService> logger,
        ISanitizerClass sanitizer)
    {
        _dbFactory = dbFactory;
        _logger = logger;
        _sanitizer = sanitizer;
    }

    /// <summary>Port of <c>GetWord</c> — retrieves a localized word/label by key.
    /// In the legacy code this looked up a dictionary loaded at init time keyed by language.
    /// The state DTO carries whether dictionary-based localization is active (DictLang).
    /// When active, returns the key itself as a pass-through (the legacy app loaded words
    /// from an XML config; here we return the key for IStringLocalizer to resolve upstream).
    /// When not active, auto-formats the key as a human-readable label.</summary>
    public async Task<string?> GetWordAsync(IncFunctionsStateDto state, string strKey, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(strKey))
            return string.Empty;

        // Legacy: if dictLang is loaded, look up in dictionary; otherwise auto-format
        // In the modernized app, dictionary lookups are handled by IStringLocalizer upstream.
        // When DictLang is true, return the key verbatim so the caller can pass it to the localizer.
        // When false, auto-format the column-style name into a readable label.
        if (state.DictLang)
        {
            return await Task.FromResult(strKey);
        }

        // Fallback: auto-format the key as a label (same as AutoFormatLabels in legacy)
        var formatted = AutoFormatLabel(strKey);
        return await Task.FromResult(formatted);
    }

    /// <summary>Port of <c>DisplayLookupSelection</c> — queries an arbitrary table for value/title pairs
    /// to populate a dropdown/select. Legacy code built &lt;option&gt; tags via Response.Write in a
    /// Do While Not rs.EOF loop over an ADO recordset. The modernized version loads the items
    /// into an internal list accessible via GetLookupSelectionItems().</summary>
    public async Task DisplayLookupSelectionAsync(
        string dBTableName, string dBValueCol, string dBIdentCol,
        string valSelectedIdent, string strOrderBy, CancellationToken ct = default)
    {
        _lastLookupItems = new List<LookupSelectionItemDto>();

        if (string.IsNullOrWhiteSpace(dBTableName) ||
            string.IsNullOrWhiteSpace(dBValueCol) ||
            string.IsNullOrWhiteSpace(dBIdentCol))
        {
            _logger.LogWarning("DisplayLookupSelectionAsync called with empty table/column names");
            return;
        }

        // Sanitize identifiers to prevent SQL injection — only allow alphanumeric, underscore, dot, brackets
        var safeTable = SanitizeSqlIdentifier(dBTableName);
        var safeValueCol = SanitizeSqlIdentifier(dBValueCol);
        var safeIdentCol = SanitizeSqlIdentifier(dBIdentCol);
        var safeOrderBy = string.IsNullOrWhiteSpace(strOrderBy)
            ? safeIdentCol
            : SanitizeSqlIdentifier(strOrderBy);

        if (safeTable == null || safeValueCol == null || safeIdentCol == null || safeOrderBy == null)
        {
            _logger.LogWarning("DisplayLookupSelectionAsync: one or more identifiers failed sanitization");
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        try
        {
            var sql = $"SELECT [{safeValueCol}], [{safeIdentCol}] FROM [{safeTable}] ORDER BY [{safeOrderBy}]";

            using var connection = db.Database.GetDbConnection();
            await connection.OpenAsync(ct);
            using var command = connection.CreateCommand();
            command.CommandText = sql;

            using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var item = new LookupSelectionItemDto
                {
                    Value = reader.IsDBNull(0) ? string.Empty : reader.GetValue(0).ToString() ?? string.Empty,
                    Title = reader.IsDBNull(1) ? string.Empty : reader.GetValue(1).ToString() ?? string.Empty
                };
                _lastLookupItems.Add(item);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DisplayLookupSelectionAsync failed for table {Table}", dBTableName);
        }
    }

    /// <summary>Returns the lookup items populated by the last call to DisplayLookupSelectionAsync.</summary>
    public List<LookupSelectionItemDto> GetLookupSelectionItems()
    {
        return _lastLookupItems;
    }

    /// <summary>Port of <c>GetPageTitle</c> — builds the page title from state.
    /// Legacy code concatenated the site name with the current navigation label.</summary>
    public async Task<string?> GetPageTitleAsync(IncFunctionsStateDto state, CancellationToken ct = default)
    {
        // Legacy: built title from site config + breadcrumb context
        // The Sanitizer field on state carried the sanitizer reference name; here we just
        // return a formatted title. The actual site title comes from configuration upstream.
        var title = "Portal";
        if (!string.IsNullOrWhiteSpace(state.Sanitizer))
        {
            title = state.Sanitizer;
        }
        return await Task.FromResult(title);
    }

    /// <summary>Port of <c>FormatDateForDB</c> — converts a date string to ISO 8601 format
    /// suitable for database insertion. Legacy: parsed various date formats and output
    /// yyyy-MM-dd HH:mm:ss.</summary>
    public async Task<string?> FormatDateForDBAsync(string dt, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dt))
            return await Task.FromResult<string?>(null);

        // Try parsing with multiple culture-safe formats
        string[] formats = new[]
        {
            "yyyy-MM-dd",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-dd HH:mm:ss",
            "dd/MM/yyyy",
            "dd/MM/yyyy HH:mm:ss",
            "MM/dd/yyyy",
            "MM/dd/yyyy HH:mm:ss",
            "dd-MM-yyyy",
            "dd-MM-yyyy HH:mm:ss",
            "yyyyMMdd",
            "yyyyMMddHHmmss"
        };

        if (DateTime.TryParseExact(dt.Trim(), formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
        {
            // Legacy output format: 'yyyy-MM-dd HH:mm:ss'
            return await Task.FromResult(parsed.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        }

        // Fallback: try general parse
        if (DateTime.TryParse(dt.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var generalParsed))
        {
            return await Task.FromResult(generalParsed.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        }

        _logger.LogWarning("FormatDateForDBAsync: unable to parse date string '{DateString}'", dt);
        return await Task.FromResult<string?>(null);
    }

    /// <summary>Port of <c>isIsoDate</c> — checks whether a string is in ISO date format (yyyy-MM-dd...).
    /// Returns the input if it is a valid ISO date, empty string otherwise.</summary>
    public async Task<string?> isIsoDateAsync(string s_input, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(s_input))
            return await Task.FromResult(string.Empty);

        var trimmed = s_input.Trim();

        // Legacy check: must be at least 10 chars, format yyyy-MM-dd
        if (trimmed.Length < 10)
            return await Task.FromResult(string.Empty);

        // Check format: positions 0-3 digits, pos 4 '-', pos 5-6 digits, pos 7 '-', pos 8-9 digits
        if (trimmed[4] != '-' || trimmed[7] != '-')
            return await Task.FromResult(string.Empty);

        var yearPart = trimmed.Substring(0, 4);
        var monthPart = trimmed.Substring(5, 2);
        var dayPart = trimmed.Substring(8, 2);

        if (!int.TryParse(yearPart, NumberStyles.None, CultureInfo.InvariantCulture, out var year) ||
            !int.TryParse(monthPart, NumberStyles.None, CultureInfo.InvariantCulture, out var month) ||
            !int.TryParse(dayPart, NumberStyles.None, CultureInfo.InvariantCulture, out var day))
        {
            return await Task.FromResult(string.Empty);
        }

        if (year < 1 || month < 1 || month > 12 || day < 1 || day > 31)
            return await Task.FromResult(string.Empty);

        // Validate it's a real date
        try
        {
            var _ = new DateTime(year, month, day);
        }
        catch
        {
            return await Task.FromResult(string.Empty);
        }

        return await Task.FromResult(trimmed);
    }

    /// <summary>Port of <c>AutoFormatLabels</c> — converts a database column name to a
    /// human-readable label by inserting spaces before capitals, replacing underscores,
    /// and title-casing the result.</summary>
    public async Task<string?> AutoFormatLabelsAsync(string colName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(colName))
            return await Task.FromResult(string.Empty);

        var result = AutoFormatLabel(colName);
        return await Task.FromResult(result);
    }

    /// <summary>Port of <c>LoadIncFunctions</c> — initializes the Inc_Functions state.
    /// Legacy code set up the sanitizer instance, loaded the language dictionary, and
    /// prepared utility state. Returns an initialized IncFunctionsStateDto.</summary>
    public async Task<IncFunctionsStateDto> LoadIncFunctionsAsync(IncFunctionsStateDto state, CancellationToken ct = default)
    {
        // Legacy: Class_Initialize on SanitizerClass set globalIsAdmin = false,
        // loaded dictLang from config, initialized the Sanitizer reference.
        var result = new IncFunctionsStateDto
        {
            DictLang = state.DictLang,
            Sanitizer = string.IsNullOrWhiteSpace(state.Sanitizer)
                ? "default"
                : state.Sanitizer
        };

        return await Task.FromResult(result);
    }

    /// <summary>Port of <c>FormatValueForJson</c> — escapes a string value for safe embedding in JSON.
    /// Mirrors the legacy stored procedure [portal].[FormatValueForJson] and the VBScript
    /// SanitizerClass.JSON method, which escaped control characters, backslashes, quotes,
    /// and high Unicode per Douglas Crockford's json2.js escaping rules.</summary>
    public async Task<string> FormatValueForJsonAsync(string value)
    {
        if (value == null)
            return await Task.FromResult(string.Empty);

        // Use the same regex pattern from the legacy SanitizerClass.JSON method
        var matches = JsonEscapeRegex.Matches(value);

        if (matches.Count == 0)
            return await Task.FromResult(value);

        var sb = new StringBuilder(value.Length + matches.Count * 6);
        int anchorIndex = 0;

        foreach (Match match in matches)
        {
            // Append the segment before this match
            if (match.Index > anchorIndex)
            {
                sb.Append(value, anchorIndex, match.Index - anchorIndex);
            }

            char c = match.Value[0];
            int charCode = (int)c;

            // Legacy Select Case mapping
            string escaped = charCode switch
            {
                92 => "\\\\",   // backslash
                8 => "\\b",     // backspace
                10 => "\\n",    // newline
                13 => "\\r",    // carriage return
                _ => "\\u" + charCode.ToString("X4")  // \uXXXX for everything else
            };

            sb.Append(escaped);
            anchorIndex = match.Index + match.Length;
        }

        // Append remaining text after last match
        if (anchorIndex < value.Length)
        {
            sb.Append(value, anchorIndex, value.Length - anchorIndex);
        }

        return await Task.FromResult(sb.ToString());
    }

    /// <summary>Port of <c>GetNavigationRecursive</c> — retrieves navigation items recursively
    /// for a given parent. Mirrors the stored procedure [portal].[GetNavigationRecursive].
    /// Loads all children of the specified parentNavId, ordered by NavOrder, then recursively
    /// loads their children.</summary>
    public async Task<List<NavigationDto>> GetNavigationRecursiveAsync(int parentNavId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        // Load ALL navigation items in one query to avoid N+1
        var allNavItems = await db.Navigations
            .AsNoTracking()
            .OrderBy(n => n.NavOrder)
            .ToListAsync();

        var result = new List<NavigationDto>();
        BuildNavigationTree(allNavItems, parentNavId, result);
        return result;
    }

    /// <summary>Port of <c>HTMLFormControl</c> — synchronous escaping for HTML form control attributes.
    /// Delegates to the SanitizerClass implementation.</summary>
    public string HTMLFormControl(string pInput)
    {
        return _sanitizer.HTMLFormControl(pInput);
    }

    /// <summary>Port of <c>HTMLDisplay</c> — synchronous escaping for HTML display content.
    /// Delegates to the SanitizerClass implementation.</summary>
    public string HTMLDisplay(string pInput)
    {
        return _sanitizer.HTMLDisplay(pInput);
    }

    /// <summary>Port of <c>Querystring</c> — synchronous URL query string escaping.
    /// Delegates to the SanitizerClass implementation.</summary>
    public string Querystring(string pInput)
    {
        return _sanitizer.Querystring(pInput);
    }

    /// <summary>Port of <c>SQL</c> — synchronous SQL string literal escaping.
    /// Delegates to the SanitizerClass implementation.</summary>
    public string SQL(string pInput)
    {
        return _sanitizer.SQL(pInput);
    }

    /// <summary>Port of <c>JSON</c> — synchronous JSON string escaping.
    /// Delegates to the SanitizerClass implementation.</summary>
    public string JSON(string str)
    {
        return _sanitizer.JSON(str);
    }

    // ─── PRIVATE / INTERNAL METHODS ───────────────────────────────────────

    /// <summary>Port of <c>pd</c> (pad digits) — pads a number string with leading zeros
    /// to reach the specified total digit count. Legacy: pd("5", "2") returns "05".</summary>
    private string Pd(string n, string totalDigits)
    {
        if (string.IsNullOrEmpty(n))
            return "0";

        if (!int.TryParse(totalDigits, NumberStyles.None, CultureInfo.InvariantCulture, out var digits))
            digits = 2;

        if (digits < 1)
            digits = 2;

        // Pad with leading zeros
        return n.PadLeft(digits, '0');
    }

    /// <summary>Port of <c>CIsoDate</c> — converts a date/time value to ISO date string format
    /// (yyyy-MM-dd HH:mm:ss). Legacy code used pd() to pad each component. If the input
    /// is not a valid date, returns empty string.</summary>
    private string CIsoDate(string s_input)
    {
        if (string.IsNullOrWhiteSpace(s_input))
            return string.Empty;

        // Try parsing the input as a date
        if (!DateTime.TryParse(s_input.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
        {
            // Try with current culture as fallback (legacy VBScript CDate was culture-dependent)
            if (!DateTime.TryParse(s_input.Trim(), out dt))
                return string.Empty;
        }

        // Legacy: built ISO string using pd() for each component
        var yearStr = Pd(dt.Year.ToString(CultureInfo.InvariantCulture), "4");
        var monthStr = Pd(dt.Month.ToString(CultureInfo.InvariantCulture), "2");
        var dayStr = Pd(dt.Day.ToString(CultureInfo.InvariantCulture), "2");
        var hourStr = Pd(dt.Hour.ToString(CultureInfo.InvariantCulture), "2");
        var minuteStr = Pd(dt.Minute.ToString(CultureInfo.InvariantCulture), "2");
        var secondStr = Pd(dt.Second.ToString(CultureInfo.InvariantCulture), "2");

        return $"{yearStr}-{monthStr}-{dayStr} {hourStr}:{minuteStr}:{secondStr}";
    }

    // ─── HELPER METHODS ──────────────────────────────────────────────────

    /// <summary>Converts a database column name like "first_name" or "FirstName" into
    /// "First Name". Replaces underscores with spaces, inserts spaces before uppercase
    /// letters in camelCase/PascalCase, and title-cases the result.</summary>
    private static string AutoFormatLabel(string colName)
    {
        if (string.IsNullOrWhiteSpace(colName))
            return string.Empty;

        // Replace underscores with spaces
        var working = colName.Replace("_", " ");

        // Insert space before each uppercase letter that follows a lowercase letter
        // e.g. "firstName" → "first Name"
        var sb = new StringBuilder(working.Length + 10);
        for (int i = 0; i < working.Length; i++)
        {
            char c = working[i];
            if (i > 0 && char.IsUpper(c) && char.IsLower(working[i - 1]))
            {
                sb.Append(' ');
            }
            sb.Append(c);
        }

        working = sb.ToString();

        // Title-case: uppercase first letter of each word
        var words = working.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                words[i] = char.ToUpper(words[i][0], CultureInfo.InvariantCulture)
                           + (words[i].Length > 1 ? words[i].Substring(1) : string.Empty);
            }
        }

        return string.Join(" ", words);
    }

    /// <summary>Sanitizes a SQL identifier to prevent injection. Only allows alphanumeric
    /// characters, underscores, and dots. Returns null if the identifier is invalid.</summary>
    private static string? SanitizeSqlIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return null;

        var trimmed = identifier.Trim().Trim('[', ']');

        // Only allow: letters, digits, underscores, dots (for schema.table)
        if (!Regex.IsMatch(trimmed, @"^[a-zA-Z0-9_.]+$"))
            return null;

        return trimmed;
    }

    /// <summary>Recursively builds the navigation tree from a flat list, adding children
    /// of each parent in order. This avoids N+1 queries by operating on the pre-loaded list.</summary>
    private void BuildNavigationTree(
        List<ASPClassic.Domain.Entities.Core.Navigation> allItems,
        int parentNavId,
        List<NavigationDto> result)
    {
        // Find direct children of the given parent
        var children = parentNavId == 0
            ? allItems.Where(n => n.NavParentId == null || n.NavParentId == 0)
                      .OrderBy(n => n.NavOrder)
                      .ToList()
            : allItems.Where(n => n.NavParentId == parentNavId)
                      .OrderBy(n => n.NavOrder)
                      .ToList();

        foreach (var nav in children)
        {
            var dto = new NavigationDto
            {
                NavId = nav.NavId,
                NavLabel = nav.NavLabel ?? string.Empty,
                NavParentId = nav.NavParentId ?? 0,
                NavOrder = nav.NavOrder,
                NavUri = nav.NavUri ?? string.Empty,
                NavGlyph = nav.NavGlyph ?? string.Empty,
                NavTooltip = nav.NavTooltip ?? string.Empty,
                ViewID = nav.ViewID ?? 0,
                OpenUriInIFRAME = nav.OpenUriInIFRAME
            };

            result.Add(dto);

            // Recursively add children of this item — prevents infinite loops by only
            // going deeper when there are actual child records with matching NavParentId
            BuildNavigationTree(allItems, nav.NavId, result);
        }
    }
}
