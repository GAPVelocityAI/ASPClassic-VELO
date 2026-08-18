using Microsoft.AspNetCore.Components;
using MudBlazor;
using ASPClassic.Application.DTOs.Dataview;
using ASPClassic.Application.Services.Dataview;

namespace ASPClassic.Shared.Dialogs;

/// <summary>
/// The add / edit / clone record modal that <c>browse.asp</c> and <c>dataview.asp</c> open.
/// </summary>
/// <remarks>
/// The legacy posted this form to <c>ajax_dataview.asp</c> and rendered the JSON reply. Here the
/// service is called directly — in a server-rendered component the round trip has nothing to cross,
/// so the values never become text and never have to be parsed back.
/// </remarks>
public partial class AjaxDataviewDialog
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;

    [Inject] private IDataviewService DataviewService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    /// <summary>The view whose record is being edited.</summary>
    [Parameter] public int ViewId { get; set; }

    /// <summary>"add", "edit" or "clone" — the legacy's strMode.</summary>
    [Parameter] public string Mode { get; set; } = "add";

    /// <summary>Primary key of the record, absent when adding.</summary>
    [Parameter] public string? ItemId { get; set; }

    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
    private List<DataViewFieldInfoDto> _fields = new();
    private MudForm? _form;
    private bool _loading = true;

    private string TitleForMode => Mode?.ToLowerInvariant() switch
    {
        "edit" => "Edit Record",
        "clone" => "Clone Record",
        _ => "Add Record",
    };

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var result = await DataviewService.LoadDataviewFullAsync(
                ItemId ?? string.Empty, Mode ?? "add", ViewId.ToString());

            _fields = result?.Fields ?? new List<DataViewFieldInfoDto>();

            // The field defaults are the starting point for a NEW record.
            foreach (var field in _fields)
                _values[field.FieldSource] = field.DefaultValue ?? string.Empty;

            // For an existing one they are only a fallback: what the form must show is what is
            // stored. Opening an edit on the defaults looks like a record with nothing in it, and
            // saving then writes those defaults over the real values.
            //
            // Cloning starts from the source record but saves as a new one, so it loads the same
            // way an edit does — only the eventual write differs.
            var isExisting = Mode is "edit" or "clone";

            if (isExisting && !string.IsNullOrWhiteSpace(ItemId))
            {
                var record = await DataviewService.GetDataviewRecordAsync(ViewId, ItemId);

                if (record.Count == 0)
                {
                    Snackbar.Add("That record could not be found.", Severity.Warning);
                }
                else
                {
                    foreach (var field in _fields)
                    {
                        if (record.TryGetValue(field.FieldSource, out var stored))
                            _values[field.FieldSource] = stored;
                    }
                }
            }

            await LoadFieldOptionsAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Could not load the form: {ex.Message}", Severity.Error);
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>
    /// The choices for each field that draws them from another table, keyed by field source.
    /// </summary>
    private readonly Dictionary<string, List<(string Value, string Label)>> _options =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Loads the choices for every field whose type says it has some.
    /// </summary>
    /// <remarks>
    /// The portal describes each field's type in its own metadata — 29 of them — and a form that
    /// renders every one as a text box discards all of it. That is not only a cosmetic loss: a
    /// foreign key rendered as free text accepts a word, and the row it then points at does not
    /// exist. Confirmed live, on a navigation entry whose parent was saved as "a" and which
    /// consequently appeared nowhere in the menu.
    /// </remarks>
    private async Task LoadFieldOptionsAsync()
    {
        foreach (var field in _fields.Where(f => f.ShowInForm && IsLookup(f)))
        {
            if (string.IsNullOrWhiteSpace(field.LinkedTable)) continue;

            var choices = await DataviewService.GetLookupOptionsAsync(
                field.LinkedTable, field.LinkedTableValueField, field.LinkedTableTitleField);

            if (choices.Count > 0) _options[field.FieldSource] = choices;
        }
    }

    /// <summary>Field types that present a fixed set of choices.</summary>
    /// <remarks>Values from the portal's own DataViewFieldTypes list.</remarks>
    private static bool IsLookup(DataViewFieldInfoDto f) =>
        f.FieldTypeNumeric is 5 or 6 or 20 or 21;

    /// <summary>Field types stored as a number.</summary>
    private static bool IsNumeric(DataViewFieldInfoDto f) =>
        f.FieldTypeNumeric is 3 or 4;

    /// <summary>Field types that are on or off.</summary>
    private static bool IsBoolean(DataViewFieldInfoDto f) =>
        f.FieldTypeNumeric is 9 or 22 or 23 or 26;

    /// <summary>Field types edited over several lines.</summary>
    private static bool IsMultiline(DataViewFieldInfoDto f) =>
        f.FieldTypeNumeric is 2 or 14;

    private bool HasOptions(DataViewFieldInfoDto f) => _options.ContainsKey(f.FieldSource);

    private List<(string Value, string Label)> OptionsFor(DataViewFieldInfoDto f) =>
        _options.TryGetValue(f.FieldSource, out var o) ? o : new List<(string, string)>();

    private bool GetBool(string fieldSource)
    {
        var v = GetValue(fieldSource);
        return v is "1" or "true" or "True" or "on";
    }

    private void SetBool(string fieldSource, bool on) => _values[fieldSource] = on ? "1" : "0";

    private string GetValue(string fieldSource) =>
        _values.TryGetValue(fieldSource, out var v) ? v : string.Empty;

    private void SetValue(string fieldSource, string value) => _values[fieldSource] = value;

    private void OnCancel() => MudDialog.Cancel();

    private bool _saving;

    private async Task OnSave()
    {
        // A required field left empty is the one check the legacy form made before posting.
        var missing = _fields
            .Where(f => f.ShowInForm && f.IsRequired && string.IsNullOrWhiteSpace(GetValue(f.FieldSource)))
            .Select(f => f.FieldLabel)
            .ToList();

        if (missing.Count > 0)
        {
            Snackbar.Add($"Required: {string.Join(", ", missing)}", Severity.Warning);
            return;
        }

        _saving = true;

        try
        {
            // The dialog writes the record itself, as the legacy form did by posting to
            // ajax_dataview.asp. Closing with the values and leaving the caller to persist them is
            // how a save comes to report success without a statement ever running.
            var error = await DataviewService.SaveDataviewRecordAsync(ViewId, Mode ?? "add", ItemId, _values);

            if (error != null)
            {
                Snackbar.Add(error, Severity.Error);
                return;
            }

            // Ok means written. The caller refreshes its grid on that, and on nothing else.
            MudDialog.Close(DialogResult.Ok(_values));
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Could not save: {ex.Message}", Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }
}
