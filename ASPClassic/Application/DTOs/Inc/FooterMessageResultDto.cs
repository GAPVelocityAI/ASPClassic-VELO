namespace ASPClassic.Application.DTOs.Inc;

// This DTO is retained for backward compatibility but the canonical type is
// ASPClassic.Application.Services.Inc.FooterMessageResult defined alongside the interface.
// New code should use FooterMessageResult directly.

/// <summary>
/// Carries the resolved notification message and severity from the legacy inc_footer_jscripts MSG code resolution.
/// Used by pages to display MudBlazor Snackbar notifications after CRUD operations.
/// </summary>
public class FooterMessageResultDto
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
