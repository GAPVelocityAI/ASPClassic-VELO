using ASPClassic.Domain.Entities.Data;
using ASPClassic.Application.DTOs.Dataview;
using ASPClassic.Application.DTOs.Data;
namespace ASPClassic.Application.DTOs.Dataview;

/// <summary>
/// Result DTO returned by <c>LoadDataviewNgdtAsync</c>.
/// Contains the fully-populated page state for the DataView NGDT page.
/// Port of the page-level state built by <c>dataview_ngdt.asp</c>.
/// </summary>
public class DataviewNgdtLoadResultDto
{
    public int ViewID { get; set; }
    public string? Error { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DataSource { get; set; } = string.Empty;
    public string ViewDescription { get; set; } = string.Empty;
    public string ViewProcedure { get; set; } = string.Empty;
    public string ModificationProcedure { get; set; } = string.Empty;
    public string DeleteProcedure { get; set; } = string.Empty;
    public string MainTable { get; set; } = string.Empty;
    public string OrderBy { get; set; } = string.Empty;
    public string Primarykey { get; set; } = string.Empty;
    public string CSSTable { get; set; } = string.Empty;
    public short DataTableModifierButtonStyle { get; set; }
    public int DataTableDefaultPageSize { get; set; }
    public string DataTablePagingStyle { get; set; } = string.Empty;

    // View flags
    public bool AllowUpdate { get; set; }
    public bool AllowInsert { get; set; }
    public bool AllowDelete { get; set; }
    public bool AllowClone { get; set; }
    public bool ShowRowActions { get; set; }
    public bool ShowForm { get; set; }
    public bool ShowList { get; set; }
    public bool AllowSearch { get; set; }
    public bool RTEEnabled { get; set; }
    public bool ShowCharts { get; set; }

    // DataTable flags
    public bool DtInfo { get; set; }
    public bool DtColumnFooter { get; set; }
    public bool DtQuickSearch { get; set; }
    public bool DtSort { get; set; }
    public bool DtPagination { get; set; }
    public bool DtPageSizeSelection { get; set; }
    public bool DtStateSave { get; set; }

    // Modifier button style
    public string ModifierButtonStyleClass { get; set; } = string.Empty;
    public bool ModifierShowText { get; set; }
    public bool ModifierShowGlyph { get; set; }

    // Fields
    public List<DataViewFieldInfoDto> Fields { get; set; } = new();

    // View query string
    public string ViewQueryString { get; set; } = string.Empty;

    // List column count
    public int ListColumnCount { get; set; }

    // Field defaults map (index → default value)
    public Dictionary<int, string> FieldDefaults { get; set; } = new();

    // Redirect URL after successful operation
    public string? RedirectUrl { get; set; }

    // Success message key
    public string? SuccessMessage { get; set; }

    // Current mode
    public string Mode { get; set; } = "none";

    // Current item ID (for edit mode)
    public int? ItemID { get; set; }

    // Edit-mode row data (field index → value from DB)
    public Dictionary<int, string?> EditRowData { get; set; } = new();

    // Linked table lookup data (field index → list of key-value pairs)
    public Dictionary<int, List<LookupItemDto>> LookupData { get; set; } = new();
}
