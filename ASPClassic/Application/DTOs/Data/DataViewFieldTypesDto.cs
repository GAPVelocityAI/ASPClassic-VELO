#nullable disable
using System;
using System.Collections.Generic;

namespace ASPClassic.Application.DTOs.Data;

/// <summary>DataViewFieldTypes data transfer object (domain: Data).</summary>
public class DataViewFieldTypesDto
{
    public string TypeValue { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string TypeWrappers { get; set; } = string.Empty;
    public string TypeIdentifier { get; set; } = string.Empty;
    public string TypeGroup { get; set; } = string.Empty;
}
