namespace ASPClassic.Application.DTOs.Admin;

/// <summary>
/// Result DTO returned by AdminDataviewfieldsService methods,
/// carrying page state, field list, current edit data, and messages.
/// </summary>
public class AdminDataviewfieldsResultDto
{
    public int ViewID { get; set; }
    public string DataViewTitle { get; set; } = string.Empty;
    public string PageTitle { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public int? EditFieldID { get; set; }
    public DataViewFieldEditDto? CurrentField { get; set; }
    public List<DataViewFieldListItemDto> Fields { get; set; } = new();
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Something the save should be told about without being refused.
    /// </summary>
    /// <remarks>
    /// A field naming a column that does not exist yet is the case this was added for: legitimate
    /// when the column is about to be created, and a silent failure when it is not. Refusing it
    /// blocks a real workflow; saying nothing is how it goes unnoticed until an insert fails on
    /// another screen entirely.
    /// </remarks>
    public string? WarningMessage { get; set; }
    public bool RedirectToList { get; set; }
    public string? RedirectUrl { get; set; }
}
