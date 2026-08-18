using ASPClassic.Application.Services.Sanitizer;
namespace ASPClassic.Application.Services.Inc;

/// <summary>Port of <c>SanitizerClass</c> (inc_functions.asp) — sanitization/escaping methods for HTML forms, display, querystrings, SQL, and JSON.</summary>
public interface ISanitizerClass
{
    Task<string?> HTMLFormControlAsync(string pInput, CancellationToken ct = default);
    Task<string?> HTMLDisplayAsync(string pInput, CancellationToken ct = default);
    Task<string?> QuerystringAsync(string pInput, CancellationToken ct = default);
    Task<string?> SQLAsync(string pInput, CancellationToken ct = default);
    Task<string?> JSONAsync(string str, CancellationToken ct = default);
    string HTMLFormControl(string pInput);
    string HTMLDisplay(string pInput);
    string Querystring(string pInput);
    string SQL(string pInput);
    string JSON(string str);
}
