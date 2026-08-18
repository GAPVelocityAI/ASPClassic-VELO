using System.Collections.Generic;
using ASPClassic.Domain.Entities.Core;

namespace ASPClassic.Infrastructure.Navigation;

/// <summary>
/// Hierarchical navigation node DTO used by <see cref="NavigationTreeBuilder"/>
/// to represent the recursive navigation tree.
/// </summary>
public class NavigationNodeDto
{
    public int NavId { get; set; }
    public string NavLabel { get; set; } = string.Empty;
    public int? NavParentId { get; set; }
    public int NavOrder { get; set; }
    public string NavUri { get; set; } = string.Empty;
    public string NavGlyph { get; set; } = string.Empty;
    public string NavTooltip { get; set; } = string.Empty;
    public int? ViewID { get; set; }
    public bool OpenUriInIFRAME { get; set; }
    public List<NavigationNodeDto> Children { get; set; } = new();
}
