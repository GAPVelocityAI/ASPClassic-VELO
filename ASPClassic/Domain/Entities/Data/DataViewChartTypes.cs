#nullable disable
using System;
using System.Collections.Generic;

namespace ASPClassic.Domain.Entities.Data;

/// <summary>Port of <c>ASPClassic:portal.DataViewChartTypes table (reference)</c>. Deterministically generated from the plan's schema.</summary>
public class DataViewChartTypes
{
    public string TypeValue { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string TypeCode { get; set; } = string.Empty;
}
