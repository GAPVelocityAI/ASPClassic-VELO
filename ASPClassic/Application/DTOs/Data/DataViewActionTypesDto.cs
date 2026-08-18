#nullable disable
using System;
using System.Collections.Generic;

namespace ASPClassic.Application.DTOs.Data;

/// <summary>DataViewActionTypes data transfer object (domain: Data).</summary>
public class DataViewActionTypesDto
{
    public string TypeValue { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string TypeDefault { get; set; } = string.Empty;
}
