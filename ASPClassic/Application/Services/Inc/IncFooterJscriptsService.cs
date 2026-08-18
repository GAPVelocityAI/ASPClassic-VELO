using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MudBlazor;
using ASPClassic.Infrastructure.Data;

namespace ASPClassic.Application.Services.Inc;

/// <summary>
/// Port of <c>inc_footer_jscripts.asp</c>.
/// Resolves CRUD operation result codes (MSG query-string parameter) into structured notification
/// messages. The legacy ASP file emitted inline JavaScript toastr calls based on a SELECT CASE
/// over Request("MSG"). In Blazor, this service uses ISnackbar to display the equivalent
/// notification directly, and also exposes a DTO-returning method for programmatic access.
///
/// The legacy code also checked a page-scoped <c>strError</c> variable and, if non-empty,
/// emitted a separate error toast. In the Blazor port, error context is passed via the
/// <c>mSG</c> parameter using the convention "error:{detail}" when an upstream
/// operation sets an error string.
/// </summary>
public class IncFooterJscriptsService : IIncFooterJscriptsService
{
    private readonly IDbContextFactory<ASPClassicVBScriptDbContext> _dbFactory;
    private readonly ILogger<IncFooterJscriptsService> _logger;
    private readonly ISnackbar _snackbar;

    public IncFooterJscriptsService(
        IDbContextFactory<ASPClassicVBScriptDbContext> dbFactory,
        ILogger<IncFooterJscriptsService> logger,
        ISnackbar snackbar)
    {
        _dbFactory = dbFactory;
        _logger = logger;
        _snackbar = snackbar;
    }

    /// <inheritdoc />
    public async Task LoadIncFooterJscriptsAsync(string mSG, CancellationToken ct = default)
    {
        var result = await ResolveMessageAsync(mSG, ct);

        // Legacy emitted toastr JS calls; Blazor equivalent is ISnackbar
        if (result.HasMessage)
        {
            var severity = result.Severity switch
            {
                "success" => MudBlazor.Severity.Success,
                "info" => MudBlazor.Severity.Info,
                "warning" => MudBlazor.Severity.Warning,
                "error" => MudBlazor.Severity.Error,
                _ => MudBlazor.Severity.Normal
            };
            _snackbar.Add(result.Message, severity);
        }

        // Legacy: IF strError <> "" THEN → toastr error strError END IF
        if (result.HasError)
        {
            _snackbar.Add(result.ErrorDetail, MudBlazor.Severity.Error);
        }
    }

    /// <inheritdoc />
    public Task<FooterMessageResult> ResolveMessageAsync(string mSG, CancellationToken ct = default)
    {
        // Legacy code:
        //   SELECT CASE Request("MSG")
        //     CASE "edit"       → toastr success "Record updated successfully"
        //     CASE "add"        → toastr success "Record added successfully"
        //     CASE "delete"     → toastr success "Record deleted successfully"
        //     CASE "autoinit"   → toastr info    "Fields auto-initialized"
        //     CASE "sorted"     → toastr info    "Order updated successfully"
        //     CASE "actiondone" → toastr success "Action completed successfully"
        //     CASE "notfound"   → toastr warning "Record not found"
        //   END SELECT
        //   IF strError <> "" THEN
        //     toastr error strError
        //   END IF

        var result = new FooterMessageResult();

        // Normalize the MSG code — legacy used Request("MSG") which is case-insensitive in VBScript
        var msgCode = (mSG ?? string.Empty).Trim();

        // Check for error prefix convention: "error:{detail}" allows upstream callers to pass
        // the legacy strError through the same channel
        string errorDetail = string.Empty;
        if (msgCode.StartsWith("error:", StringComparison.OrdinalIgnoreCase))
        {
            errorDetail = msgCode.Substring(6).Trim();
            // Strip the error prefix so the SELECT CASE below does not match it
            msgCode = string.Empty;
        }

        // SELECT CASE equivalent — case-insensitive comparison matches VBScript behavior
        switch (msgCode.ToLowerInvariant())
        {
            case "edit":
                result.Message = "Record updated successfully.";
                result.Severity = "success";
                result.HasMessage = true;
                _logger.LogInformation("Footer message resolved: edit → success");
                break;

            case "add":
                result.Message = "Record added successfully.";
                result.Severity = "success";
                result.HasMessage = true;
                _logger.LogInformation("Footer message resolved: add → success");
                break;

            case "delete":
                result.Message = "Record deleted successfully.";
                result.Severity = "success";
                result.HasMessage = true;
                _logger.LogInformation("Footer message resolved: delete → success");
                break;

            case "autoinit":
                result.Message = "Fields auto-initialized.";
                result.Severity = "info";
                result.HasMessage = true;
                _logger.LogInformation("Footer message resolved: autoinit → info");
                break;

            case "sorted":
                result.Message = "Order updated successfully.";
                result.Severity = "info";
                result.HasMessage = true;
                _logger.LogInformation("Footer message resolved: sorted → info");
                break;

            case "actiondone":
                result.Message = "Action completed successfully.";
                result.Severity = "success";
                result.HasMessage = true;
                _logger.LogInformation("Footer message resolved: actiondone → success");
                break;

            case "notfound":
                result.Message = "Record not found.";
                result.Severity = "warning";
                result.HasMessage = true;
                _logger.LogWarning("Footer message resolved: notfound → warning");
                break;

            default:
                // No recognized MSG code — legacy would simply not emit a toastr call
                if (!string.IsNullOrEmpty(msgCode))
                {
                    _logger.LogDebug("Footer message: unrecognized MSG code '{MsgCode}', no notification generated", msgCode);
                }
                break;
        }

        // Legacy: IF strError <> "" THEN → toastr error strError END IF
        if (!string.IsNullOrWhiteSpace(errorDetail))
        {
            result.ErrorDetail = errorDetail;
            result.HasError = true;
            _logger.LogWarning("Footer message includes error detail: {ErrorDetail}", errorDetail);
        }

        return Task.FromResult(result);
    }
}
