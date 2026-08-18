using ASPClassic.Domain.Entities.Data;
namespace ASPClassic.Application.DTOs.Dataview;

/// <summary>
/// Fully-resolved result of loading a DataView page, including decoded flag bitmasks,
/// field definitions, inline/toolbar actions, and computed rendering booleans.
/// Port of the complete state built by <c>dataview.asp</c> top-level script.
/// </summary>
public class DataViewLoadResultDto
{
    // Core identity
    public int ViewID { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DataSource { get; set; } = string.Empty;
    public bool Published { get; set; }
    public string Error { get; set; } = string.Empty;

    // View metadata
    public string ViewDescription { get; set; } = string.Empty;
    public string ViewProcedure { get; set; } = string.Empty;
    public string ModificationProcedure { get; set; } = string.Empty;
    public string DeleteProcedure { get; set; } = string.Empty;
    public string MainTable { get; set; } = string.Empty;
    public string OrderBy { get; set; } = string.Empty;
    public string RowReorderColumn { get; set; } = string.Empty;
    public string RowReorderColumnMasked { get; set; } = string.Empty;
    public string Primarykey { get; set; } = string.Empty;
    public string CSSTable { get; set; } = string.Empty;
    public bool IsSystemObject { get; set; }
    public short DataTableModifierButtonStyle { get; set; }
    public int DataTableDefaultPageSize { get; set; }
    public string DataTablePagingStyle { get; set; } = string.Empty;

    // Decoded View Flags (from Flags bitmask)
    public bool AllowUpdate { get; set; }
    public bool AllowInsert { get; set; }
    public bool AllowDelete { get; set; }
    public bool AllowClone { get; set; }
    public bool ShowForm { get; set; }
    public bool ShowList { get; set; }
    public bool ShowCharts { get; set; }
    public bool ShowCustomActions { get; set; }
    public bool BrowseMode { get; set; }

    // Decoded DataTable Flags (from DataTableFlags bitmask)
    public bool DtInfo { get; set; }
    public bool DtColumnFooter { get; set; }
    public bool DtQuickSearch { get; set; }
    public bool DtSort { get; set; }
    public bool DtPagination { get; set; }
    public bool DtPageSizeSelection { get; set; }
    public bool DtStateSave { get; set; }
    public bool AllowSearch { get; set; }
    public bool AllowColumnsToggle { get; set; }
    public bool AllowRowDetails { get; set; }
    public bool AllowRowSelection { get; set; }
    public bool ExportClipboard { get; set; }
    public bool ExportCSV { get; set; }
    public bool ExportExcel { get; set; }
    public bool ExportPDF { get; set; }
    public bool ExportPrint { get; set; }
    public bool FixedHeaders { get; set; }

    // Computed compound flags
    public bool ShowRowActions { get; set; }
    public bool AllowExport { get; set; }
    public bool AllowExportAll { get; set; }

    // Modifier button style resolved index
    public int DtModBtnStyleIndex { get; set; }

    // View query string fragment
    public string ViewQueryString { get; set; } = string.Empty;

    // Fields and actions
    public List<DataViewFieldInfoDto> Fields { get; set; } = new();
    public List<DataViewActionInfoDto> InlineActions { get; set; } = new();
    public List<DataViewActionInfoDto> ToolbarActions { get; set; } = new();
}
