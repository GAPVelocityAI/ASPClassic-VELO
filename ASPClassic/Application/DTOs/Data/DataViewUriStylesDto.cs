#nullable disable
using System;
using System.Collections.Generic;

namespace ASPClassic.Application.DTOs.Data;

/// <summary>DataViewUriStyles data transfer object (domain: Data).</summary>
public class DataViewUriStylesDto
{
    public string StyleValue { get; set; } = string.Empty;
    public string StyleLabel { get; set; } = string.Empty;
    public string StyleClass { get; set; } = string.Empty;
    public string StyleGlyph { get; set; } = string.Empty;
    public string StyleDefault { get; set; } = string.Empty;
}
