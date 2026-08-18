using ASPClassic.Domain.Entities.Data;
namespace ASPClassic.Application.DTOs.Browse;

/// <summary>
/// Carries the fully-resolved DataView metadata along with all decoded flag booleans
/// needed by the Browse page to render toolbar buttons, export options, and mode switches.
/// Port of the flag-decoding logic in <c>browse.asp</c>.
/// </summary>
public class BrowseResultDto
{
    // Core view info
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
    public string CSSTable { get; set; } = string.Empty;
    public string DataTablePagingStyle { get; set; } = string.Empty;
    public short DataTableModifierButtonStyle { get; set; }
    public int DataTableDefaultPageSize { get; set; }
    public bool Published { get; set; }
    public bool IsSystemObject { get; set; }

    // Decoded from Flags (nViewFlags)
    public bool AllowUpdate { get; set; }
    public bool AllowInsert { get; set; }
    public bool AllowDelete { get; set; }
    public bool AllowClone { get; set; }
    public bool ShowForm { get; set; }
    public bool ShowList { get; set; }
    public bool ShowCharts { get; set; }
    public bool ShowCustomActions { get; set; }
    public bool BrowseMode { get; set; }

    // Decoded from DataTableFlags (nDtFlags)
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

    // Computed from multiple flags
    public bool ShowRowActions { get; set; }
    public bool AllowExport { get; set; }
    public bool AllowExportAll { get; set; }

    // Page metadata
    public string PageTitle { get; set; } = string.Empty;
    public string ViewQueryString { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public bool RedirectTo404 { get; set; }
    public bool RedirectToDataview { get; set; }
    public string RedirectUrl { get; set; } = string.Empty;

    // Modifier button style index
    public int DtModBtnStyleIndex { get; set; }
}
