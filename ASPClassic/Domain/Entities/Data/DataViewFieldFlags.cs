#nullable disable
using System;
using System.Collections.Generic;

namespace ASPClassic.Domain.Entities.Data;

/// <summary>Port of <c>ASPClassic:portal.DataViewFieldFlags</c>. Deterministically generated from the plan's schema.</summary>
public class DataViewFieldFlags
{
    public string FlagValue { get; set; } = string.Empty;
    public string FlagLabel { get; set; } = string.Empty;
    public string FlagGlyph { get; set; } = string.Empty;
    public string FlagDefault { get; set; } = string.Empty;
}
