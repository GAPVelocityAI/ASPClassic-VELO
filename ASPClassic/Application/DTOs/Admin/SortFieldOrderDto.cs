namespace ASPClassic.Application.DTOs.Admin;

/// <summary>
/// DTO for a single field reorder entry used by sortFields mode.
/// </summary>
public class SortFieldOrderDto
{
    public int FieldID { get; set; }
    public int NewOrder { get; set; }
}
