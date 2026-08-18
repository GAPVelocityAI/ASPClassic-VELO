#nullable disable
using System;
using System.Collections.Generic;

namespace ASPClassic.Application.DTOs.Data;

/// <summary>DataView data transfer object (domain: Data).</summary>
public class DataViewDto
{
    public int ViewID { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DataSource { get; set; } = string.Empty;
    public string MainTable { get; set; } = string.Empty;
    public string Primarykey { get; set; } = string.Empty;
    public string ModificationProcedure { get; set; } = string.Empty;
    public string ViewProcedure { get; set; } = string.Empty;
    public string DeleteProcedure { get; set; } = string.Empty;
    public string ViewDescription { get; set; } = string.Empty;
    public string OrderBy { get; set; } = string.Empty;
    public int Flags { get; set; }
    public short DataTableModifierButtonStyle { get; set; }
    public int DataTableFlags { get; set; }
    public int DataTableDefaultPageSize { get; set; }
    public string DataTablePagingStyle { get; set; } = string.Empty;
    public bool Published { get; set; }
    public string RowReorderColumn { get; set; } = string.Empty;
    public bool IsSystemObject { get; set; }
    public string CSSTable { get; set; } = string.Empty;
}
