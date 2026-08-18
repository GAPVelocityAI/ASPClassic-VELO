using System.Threading;
using System.Threading.Tasks;

namespace ASPClassic.Application.Services.Sanitizer;

/// <summary>Port of <c>SanitizerClass</c> from <c>inc_functions.asp</c>.
/// Provides string sanitization for HTML form controls, HTML display, querystrings, SQL, and JSON.</summary>
public interface ISanitizerClass
{
    /// <summary>HTML-encodes input for use inside form control value attributes.</summary>
    Task<string?> HTMLFormControlAsync(string pInput, CancellationToken ct = default);

    /// <summary>HTML-encodes input for display, unless the current user is an admin (admins see raw HTML).</summary>
    Task<string?> HTMLDisplayAsync(string pInput, CancellationToken ct = default);

    /// <summary>URL-encodes input for use in querystrings.</summary>
    Task<string?> QuerystringAsync(string pInput, CancellationToken ct = default);

    /// <summary>Escapes single quotes for SQL string literals; returns "NULL" for null input.</summary>
    Task<string?> SQLAsync(string pInput, CancellationToken ct = default);

    /// <summary>Escapes a string for safe inclusion in a JSON value (handles control chars, Unicode ranges).</summary>
    Task<string?> JSONAsync(string str, CancellationToken ct = default);

    /// <summary>Synchronous HTML-encode for form control values. Port of legacy <c>HTMLFormControl</c>.</summary>
    string HTMLFormControl(string pInput);

    /// <summary>Synchronous HTML-encode for display. Port of legacy <c>HTMLDisplay</c>.</summary>
    string HTMLDisplay(string pInput);

    /// <summary>Synchronous URL-encode for querystring values. Port of legacy <c>Querystring</c>.</summary>
    string Querystring(string pInput);

    /// <summary>Synchronous SQL escape (doubles single quotes). Port of legacy <c>SQL</c>.</summary>
    string SQL(string pInput);

    /// <summary>Synchronous JSON escape. Port of legacy <c>JSON</c>.</summary>
    string JSON(string str);
}
