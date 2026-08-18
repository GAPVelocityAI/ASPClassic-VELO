#nullable disable
using System;
using System.Collections.Generic;

namespace ASPClassic.Application.DTOs.Data;

/// <summary>DataViewChartTypes data transfer object (domain: Data).</summary>
public class DataViewChartTypesDto
{
    public string TypeValue { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string TypeCode { get; set; } = string.Empty;
}
