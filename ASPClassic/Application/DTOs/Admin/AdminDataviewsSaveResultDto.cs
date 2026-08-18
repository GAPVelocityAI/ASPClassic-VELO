using ASPClassic.Domain.Entities.Data;
namespace ASPClassic.Application.DTOs.Admin;

/// <summary>
/// Result of admin_dataviews.asp save/load/delete operations.
/// </summary>
public class AdminDataviewsSaveResultDto
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string RedirectUrl { get; set; } = string.Empty;
    public int? NewViewID { get; set; }
    public DataViewEditDto? DataView { get; set; }
}

/// <summary>
/// Editable DTO for the admin DataView form — carries all fields the user can modify.
/// </summary>
public class DataViewEditDto
{
    public int ViewID { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DataSource { get; set; } = "Default";
    public string MainTable { get; set; } = string.Empty;
    public string Primarykey { get; set; } = string.Empty;
    public string ModificationProcedure { get; set; } = string.Empty;
    public string ViewProcedure { get; set; } = string.Empty;
    public string DeleteProcedure { get; set; } = string.Empty;
    public string ViewDescription { get; set; } = string.Empty;
    public string OrderBy { get; set; } = string.Empty;
    public string RowReorderColumn { get; set; } = string.Empty;
    public bool Published { get; set; }
    public int Flags { get; set; }
    public short DataTableModifierButtonStyle { get; set; }
    public int DataTableFlags { get; set; }
    public int DataTableDefaultPageSize { get; set; } = 10;
    public string DataTablePagingStyle { get; set; } = string.Empty;
}
