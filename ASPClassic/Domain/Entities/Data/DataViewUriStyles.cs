#nullable disable
using System;
using System.Collections.Generic;

namespace ASPClassic.Domain.Entities.Data;

/// <summary>Port of <c>ASPClassic:portal.DataViewUriStyles</c>. Deterministically generated from the plan's schema.</summary>
public class DataViewUriStyles
{
    public string StyleValue { get; set; } = string.Empty;
    public string StyleLabel { get; set; } = string.Empty;
    public string StyleClass { get; set; } = string.Empty;
    public string StyleGlyph { get; set; } = string.Empty;
    public string StyleDefault { get; set; } = string.Empty;
}
