#nullable disable
using System;
using System.Collections.Generic;

namespace ASPClassic.Application.DTOs.Data;

/// <summary>DataViewChart data transfer object (domain: Data).</summary>
public class DataViewChartDto
{
    public int ViewID { get; set; }
    public int ChartID { get; set; }
    public int ChartType { get; set; }
    public int ChartOrder { get; set; }
    public int ChartGridWidth { get; set; }
    public string ChartProperties { get; set; } = string.Empty;
    public string XField { get; set; } = string.Empty;
    public string YField { get; set; } = string.Empty;
    public string ZField { get; set; } = string.Empty;
}
