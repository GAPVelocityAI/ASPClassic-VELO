namespace ASPClassic.Application.DTOs.Dataview;

/// <summary>
/// Extended action information DTO.
/// Port of the per-action state built by <c>InitDataViewActions</c> in <c>inc_crudeconstants.asp</c>.
/// </summary>
public class DataViewActionInfoDto
{
    public int ActionID { get; set; }
    public int ViewID { get; set; }
    public string ActionLabel { get; set; } = string.Empty;
    public int? ParentActionID { get; set; }
    public string ActionTooltip { get; set; } = string.Empty;
    public string ActionDescription { get; set; } = string.Empty;
    public int ActionOrder { get; set; }
    public bool RequireConfirmation { get; set; }
    public bool OpenURLInNewWindow { get; set; }
    public string ActionExpression { get; set; } = string.Empty;
    public string GlyphIcon { get; set; } = string.Empty;
    public bool IsPerRow { get; set; }
    public string CSSButton { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string DataViewTitle { get; set; } = string.Empty;
}
