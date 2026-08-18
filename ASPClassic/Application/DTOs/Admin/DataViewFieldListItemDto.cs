using ASPClassic.Domain.Entities.Data;
namespace ASPClassic.Application.DTOs.Admin;

/// <summary>
/// List-item DTO for the DataViewField grid display.
/// </summary>
public class DataViewFieldListItemDto
{
    public int FieldID { get; set; }
    public int ViewID { get; set; }
    public string FieldLabel { get; set; } = string.Empty;
    public string FieldSource { get; set; } = string.Empty;
    public string FieldType { get; set; } = string.Empty;
    public int FieldFlags { get; set; }
    public int FieldOrder { get; set; }
    public string DefaultValue { get; set; } = string.Empty;
    public int MaxLength { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string FieldDescription { get; set; } = string.Empty;
}
