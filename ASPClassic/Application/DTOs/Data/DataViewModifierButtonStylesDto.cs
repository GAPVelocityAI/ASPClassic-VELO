#nullable disable
using System;
using System.Collections.Generic;

namespace ASPClassic.Application.DTOs.Data;

/// <summary>DataViewModifierButtonStyles data transfer object (domain: Data).</summary>
public class DataViewModifierButtonStylesDto
{
    public string StyleValue { get; set; } = string.Empty;
    public string StyleLabel { get; set; } = string.Empty;
    public string StyleClass { get; set; } = string.Empty;
    public string ShowText { get; set; } = string.Empty;
    public string ShowGlyph { get; set; } = string.Empty;
    public string StyleDefault { get; set; } = string.Empty;
}
