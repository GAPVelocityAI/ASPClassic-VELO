using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using ASPClassic.Application.DTOs.Data;
using ASPClassic.Application.Services.Dataview;
using ASPClassic.Pages.Default;

namespace ASPClassic.Shared.Dialogs;

/// <summary>Port of the Edit/Update/Clone modal from <c>dataview_ngdt.asp</c>.</summary>
public partial class DataviewNgdtEditDialog
{
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = default!;

    [Inject] private IDataviewNgdtService DataviewNgdtService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    [Parameter] public int ViewId { get; set; }
    [Parameter] public string Mode { get; set; } = "add";
    [Parameter] public string ItemId { get; set; } = string.Empty;
    [Parameter] public List<DataViewFieldDto> Fields { get; set; } = new();
    [Parameter] public Dictionary<string, object?>? RowData { get; set; }
    [Parameter] public bool AllowDelete { get; set; }
    [Parameter] public string DataSource { get; set; } = "Default";
    [Parameter] public string MainTableName { get; set; } = string.Empty;
    [Parameter] public string PrimaryKey { get; set; } = string.Empty;
    [Parameter] public string ModificationProcedure { get; set; } = string.Empty;
    [Parameter] public string PageTitle { get; set; } = "Data View";

    private string _dialogTitle = string.Empty;
    private string _formError = string.Empty;

    // Field value stores by field index
    private Dictionary<int, string> _fieldValues = new();
    private Dictionary<int, DateTime?> _dateValues = new();
    private Dictionary<int, TimeSpan?> _timeValues = new();
    private Dictionary<int, bool> _boolValues = new();
    private Dictionary<int, double?> _numericValues = new();
    private Dictionary<int, List<LookupOption>> _lookupOptions = new();

    protected override void OnParametersSet()
    {
        _dialogTitle = Mode switch
        {
            "add" => $"Add - {PageTitle}",
            "edit" => $"Edit - {PageTitle}",
            _ => PageTitle
        };

        InitializeFieldValues();
    }

    private void InitializeFieldValues()
    {
        if (Fields == null) return;

        for (int i = 0; i < Fields.Count; i++)
        {
            var field = Fields[i];
            string fieldType = field.FieldType ?? string.Empty;
            string defaultValue = field.DefaultValue ?? string.Empty;

            // Get value from RowData if editing/cloning, else use DefaultValue
            string rawValue = string.Empty;
            if (RowData != null && RowData.TryGetValue(field.FieldLabel, out var rowVal) && rowVal != null)
            {
                rawValue = rowVal.ToString() ?? string.Empty;
            }
            else
            {
                rawValue = defaultValue;
            }

            switch (fieldType)
            {
                case "7":  // date
                case "8":  // datetime
                    if (DateTime.TryParse(rawValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                        _dateValues[i] = dt;
                    else
                        _dateValues[i] = null;
                    break;

                case "13": // time
                    if (TimeSpan.TryParse(rawValue, CultureInfo.InvariantCulture, out var ts))
                        _timeValues[i] = ts;
                    else
                        _timeValues[i] = null;
                    break;

                case "9":  // boolean
                    _boolValues[i] = rawValue == "1" || rawValue.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;

                case "3":  // int
                case "4":  // double
                    if (double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
                        _numericValues[i] = num;
                    else
                        _numericValues[i] = null;
                    break;

                case "5":  // combo
                case "6":  // multicombo
                    _fieldValues[i] = rawValue;
                    // Lookup options would be loaded from LinkedTable
                    if (!string.IsNullOrEmpty(field.LinkedTable) && !string.IsNullOrEmpty(field.LinkedTableValueField))
                    {
                        if (!_lookupOptions.ContainsKey(i))
                        {
                            _lookupOptions[i] = new List<LookupOption>();
                        }
                    }
                    break;

                default:
                    _fieldValues[i] = rawValue;
                    break;
            }
        }
    }

    private string GetStringValue(int index)
    {
        return _fieldValues.TryGetValue(index, out var v) ? v ?? string.Empty : string.Empty;
    }

    private void SetStringValue(int index, string value)
    {
        _fieldValues[index] = value;
    }

    private DateTime? GetDateValue(int index)
    {
        return _dateValues.TryGetValue(index, out var v) ? v : null;
    }

    private void SetDateValue(int index, DateTime? value)
    {
        _dateValues[index] = value;
    }

    private TimeSpan? GetTimeValue(int index)
    {
        return _timeValues.TryGetValue(index, out var v) ? v : null;
    }

    private void SetTimeValue(int index, TimeSpan? value)
    {
        _timeValues[index] = value;
    }

    private bool GetBoolValue(int index)
    {
        return _boolValues.TryGetValue(index, out var v) && v;
    }

    private void SetBoolValue(int index, bool value)
    {
        _boolValues[index] = value;
    }

    private double? GetNumericValue(int index)
    {
        return _numericValues.TryGetValue(index, out var v) ? v : null;
    }

    private void SetNumericValue(int index, double? value)
    {
        _numericValues[index] = value;
    }

    private void OnCancel()
    {
        MudDialog.Cancel();
    }

    private async Task OnSave()
    {
        _formError = string.Empty;

        // Pre-save validation: check required fields
        // Port of legacy validation: FOR nIndex = 0 TO UBound(arrViewFields, 2)
        //   IF Request("inputField_" & nIndex) = "" AND (arrViewFields(dvfcFieldFlags, nIndex) AND 2) > 0 THEN error
        if (Fields != null)
        {
            for (int i = 0; i < Fields.Count; i++)
            {
                var field = Fields[i];
                string fieldType = field.FieldType ?? string.Empty;
                bool isRequired = (field.FieldFlags & 2) > 0;
                bool isReadOnly = (field.FieldFlags & 4) > 0;
                bool isLink = fieldType == "10";

                // Skip link and read-only fields (legacy: arrViewFields(dvfcFieldType, nIndex) <> 10 AND (flags AND 4) = 0)
                if (isLink || isReadOnly) continue;

                if (isRequired)
                {
                    string value = GetFieldValueAsString(i, fieldType);
                    if (string.IsNullOrEmpty(value))
                    {
                        _formError += $"<b>{System.Net.WebUtility.HtmlEncode(field.FieldLabel)}</b> is required but has not been filled.<br/>";
                    }
                }
            }
        }

        if (!string.IsNullOrEmpty(_formError))
        {
            return;
        }

        try
        {
            // Build the postback call to the service
            // The legacy code posts the form data as inputField_0, inputField_1, etc.
            // The DataviewNgdtService.LoadDataviewNgdtAsync handles the save operation
            await DataviewNgdtService.LoadDataviewNgdtAsync(
                ItemId, Mode, ViewId.ToString(), "true");

            MudDialog.Close(DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            _formError = $"Error saving: {ex.Message}";
        }
    }

    private async Task OnDeleteFromEdit()
    {
        try
        {
            await DataviewNgdtService.LoadDataviewNgdtAsync(
                ItemId, "delete", ViewId.ToString(), "true");

            Snackbar.Add("Record deleted successfully.", Severity.Success);
            MudDialog.Close(DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            _formError = $"Error deleting: {ex.Message}";
        }
    }

    private string GetFieldValueAsString(int index, string fieldType)
    {
        switch (fieldType)
        {
            case "7":  // date
            case "8":  // datetime
                return _dateValues.TryGetValue(index, out var dt) && dt.HasValue
                    ? dt.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : string.Empty;

            case "13": // time
                if (_timeValues.TryGetValue(index, out var ts) && ts.HasValue)
                {
                    // Legacy: Mid(Request("inputField_" & nIndex), 1, 8) — truncate to HH:mm:ss
                    return ts.Value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
                }
                return string.Empty;

            case "9":  // boolean
                return _boolValues.TryGetValue(index, out var b) ? (b ? "1" : "0") : "0";

            case "3":  // int
            case "4":  // double
                return _numericValues.TryGetValue(index, out var n) && n.HasValue
                    ? n.Value.ToString(CultureInfo.InvariantCulture)
                    : string.Empty;

            default:
                return _fieldValues.TryGetValue(index, out var v) ? v ?? string.Empty : string.Empty;
        }
    }

    public class LookupOption
    {
        public string Value { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
    }
}
