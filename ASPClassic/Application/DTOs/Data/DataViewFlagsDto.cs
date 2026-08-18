#nullable disable
using System;
using System.Collections.Generic;

namespace ASPClassic.Application.DTOs.Data;

/// <summary>DataViewFlags data transfer object (domain: Data).</summary>
public class DataViewFlagsDto
{
    public string FlagValue { get; set; } = string.Empty;
    public string FlagLabel { get; set; } = string.Empty;
    public string FlagGlyph { get; set; } = string.Empty;
    public string FlagDefault { get; set; } = string.Empty;
}
