#nullable disable
using System;
using System.Collections.Generic;

namespace ASPClassic.Application.DTOs.Core;

/// <summary>Navigation data transfer object (domain: Core).</summary>
public class NavigationDto
{
    public int NavId { get; set; }
    public string NavLabel { get; set; } = string.Empty;
    public int NavParentId { get; set; }
    public int NavOrder { get; set; }
    public string NavUri { get; set; } = string.Empty;
    public string NavGlyph { get; set; } = string.Empty;
    public string NavTooltip { get; set; } = string.Empty;
    public int ViewID { get; set; }
    public bool OpenUriInIFRAME { get; set; }
}
