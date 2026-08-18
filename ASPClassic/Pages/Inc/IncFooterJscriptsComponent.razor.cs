using Microsoft.AspNetCore.Components;
using MudBlazor;
using ASPClassic.Application.Services.Inc;
using ASPClassic.Application.Services.Ajax;

namespace ASPClassic.Pages.Inc;

/// <summary>Port of <c>inc_footer_jscripts.asp</c> (Include file <c>Inc_Footer_Jscripts</c>).</summary>
public partial class IncFooterJscriptsComponent : ComponentBase, IDisposable
{
    [Inject] private IIncFooterJscriptsService IncFooterJscriptsService { get; set; } = default!;
    [Inject] private IAjaxDataview AjaxDataviewService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    /// <summary>
    /// Message code passed from the parent page, corresponding to the legacy Request("MSG").
    /// Values: "edit", "add", "delete", "autoinit", "sorted", "actiondone", "notfound".
    /// </summary>
    [Parameter]
    public string? MSG { get; set; }

    /// <summary>
    /// Optional error message string, corresponding to the legacy strError variable.
    /// When non-empty, an error toast is displayed.
    /// </summary>
    [Parameter]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Callback invoked after the footer scripts component has finished processing notifications.
    /// </summary>
    [Parameter]
    public EventCallback OnNotificationsProcessed { get; set; }

    private bool _initialized;
    private string? _lastProcessedMsg;
    private string? _lastProcessedError;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _initialized = true;
            await LoadIncFooterJscripts(MSG ?? string.Empty);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        // Re-process notifications when MSG or ErrorMessage changes after initial render
        if (_initialized)
        {
            bool msgChanged = !string.Equals(_lastProcessedMsg, MSG, StringComparison.OrdinalIgnoreCase);
            bool errorChanged = !string.Equals(_lastProcessedError, ErrorMessage, StringComparison.Ordinal);

            if (msgChanged || errorChanged)
            {
                await LoadIncFooterJscripts(MSG ?? string.Empty);
            }
        }
    }

    /// <summary>
    /// Port of <c>LoadIncFooterJscripts(mSG)</c> — processes the MSG parameter and displays
    /// appropriate snackbar notifications matching the legacy toastr calls.
    /// </summary>
    private async Task LoadIncFooterJscripts(string mSG)
    {
        _lastProcessedMsg = mSG;
        _lastProcessedError = ErrorMessage;

        // Call the service layer (delegates to IIncFooterJscriptsService)
        try
        {
            await IncFooterJscriptsService.LoadIncFooterJscriptsAsync(mSG);
        }
        catch
        {
            // Service call is fire-and-forget for notification processing;
            // notification display continues below regardless.
        }

        // Port of the SELECT CASE Request("MSG") block from inc_footer_jscripts.asp
        // Each case maps to a specific toastr call in the legacy code.
        switch ((mSG ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "edit":
                Snackbar.Add("Item was successfully updated.", Severity.Success);
                break;

            case "add":
                Snackbar.Add("Item was successfully added.", Severity.Success);
                break;

            case "delete":
                Snackbar.Add("Item was successfully deleted.", Severity.Warning);
                break;

            case "autoinit":
                Snackbar.Add("Items have been initialized.", Severity.Success);
                break;

            case "sorted":
                Snackbar.Add("Sorting has been updated.", Severity.Success);
                break;

            case "actiondone":
                Snackbar.Add("Action Completed Successfully", Severity.Success);
                break;

            case "notfound":
                Snackbar.Add("Provided item ID was not found.", Severity.Error);
                break;

            default:
                // No MSG or unrecognized MSG — no toast displayed
                break;
        }

        // Port of: IF strError <> "" THEN toastr.error(...)
        if (!string.IsNullOrWhiteSpace(ErrorMessage))
        {
            // Legacy used Sanitizer.HTMLFormControl(strError) for XSS protection.
            // MudBlazor's Snackbar automatically HTML-encodes content, so direct assignment is safe.
            Snackbar.Add(ErrorMessage, Severity.Error);
        }

        // Notify parent that notifications have been processed
        if (OnNotificationsProcessed.HasDelegate)
        {
            await OnNotificationsProcessed.InvokeAsync();
        }
    }

    public void Dispose()
    {
        // No subscriptions to clean up for this component
    }
}
