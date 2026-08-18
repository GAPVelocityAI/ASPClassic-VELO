namespace ASPClassic.Application.Services.Inc;

/// <summary>
/// Port of <c>inc_footer_jscripts.asp</c>.
/// Resolves CRUD operation result codes (MSG parameter) into user-facing notification messages
/// that the calling Blazor page can display via MudBlazor Snackbar.
/// </summary>
public interface IIncFooterJscriptsService
{
    /// <summary>
    /// Resolves the given MSG code and triggers a snackbar notification on the calling page.
    /// Legacy: SELECT CASE Request("MSG") block in inc_footer_jscripts.asp.
    /// </summary>
    /// <param name="mSG">The operation result code: "edit", "add", "delete", "autoinit", "sorted", "actiondone", "notfound", or empty.</param>
    /// <param name="ct">Cancellation token.</param>
    Task LoadIncFooterJscriptsAsync(string mSG, CancellationToken ct = default);

    /// <summary>
    /// Resolves the given MSG code into a structured notification result for callers that need
    /// programmatic access to the message and severity rather than direct snackbar display.
    /// </summary>
    Task<FooterMessageResult> ResolveMessageAsync(string mSG, CancellationToken ct = default);
}

/// <summary>
/// Carries the resolved notification message and severity from the legacy inc_footer_jscripts MSG code resolution.
/// Used by pages to display MudBlazor Snackbar notifications after CRUD operations.
/// </summary>
public class FooterMessageResult
{
    /// <summary>The user-facing notification message text. Empty if no message applies.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Severity level: "success", "info", "warning", "error".</summary>
    public string Severity { get; set; } = string.Empty;

    /// <summary>Optional error detail string (from legacy strError). Empty when no error.</summary>
    public string ErrorDetail { get; set; } = string.Empty;

    /// <summary>Whether a message was resolved (MSG was a recognized code).</summary>
    public bool HasMessage { get; set; }

    /// <summary>Whether an error detail is present.</summary>
    public bool HasError { get; set; }
}
