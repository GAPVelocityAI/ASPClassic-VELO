#nullable disable
using System;
using System.Collections.Generic;

namespace ASPClassic.Application.DTOs.Inc;

/// <summary>IncFunctionsState data transfer object (domain: Inc).</summary>
public class IncFunctionsStateDto
{
    public bool DictLang { get; set; }
    public string Sanitizer { get; set; } = string.Empty;
}
