#nullable disable
using System;
using System.Collections.Generic;

namespace ASPClassic.Application.DTOs.Data;

/// <summary>DataViewPagingTypes data transfer object (domain: Data).</summary>
public class DataViewPagingTypesDto
{
    public string StyleValue { get; set; } = string.Empty;
    public string StyleLabel { get; set; } = string.Empty;
    public string StyleDefault { get; set; } = string.Empty;
}
