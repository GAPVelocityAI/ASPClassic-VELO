#nullable disable
using System;
using System.Collections.Generic;

namespace ASPClassic.Domain.Entities.Data;

/// <summary>Port of <c>ASPClassic:portal.DataViewChart table (reference)</c>. Deterministically generated from the plan's schema.</summary>
public class DataViewChart
{
    public int ViewID { get; set; }
    public int ChartID { get; set; }
    public int ChartType { get; set; }
    public int? ChartOrder { get; set; }
    public int ChartGridWidth { get; set; }
    public string ChartProperties { get; set; } = string.Empty;
    public string XField { get; set; } = string.Empty;
    public string YField { get; set; } = string.Empty;
    public string ZField { get; set; } = string.Empty;

    public virtual ASPClassic.Domain.Entities.Data.DataView View { get; set; }
}
