using ASPClassic.Domain.Entities.Data;
namespace ASPClassic.Application.DTOs.Inc;

/// <summary>Port of legacy JS column object built by <c>InitDataViewFieldsJS</c> — one entry per DataViewField.</summary>
public class DataViewFieldJsColumnDto
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
    public string UriPath { get; set; } = string.Empty;
    public int UriStyle { get; set; }
    public string LinkedTable { get; set; } = string.Empty;
    public string LinkedTableValueField { get; set; } = string.Empty;
    public string LinkedTableTitleField { get; set; } = string.Empty;
    public string LinkedTableGroupField { get; set; } = string.Empty;
    public string LinkedTableGlyphField { get; set; } = string.Empty;
    public string LinkedTableTooltipField { get; set; } = string.Empty;
    public string LinkedTableAddition { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string FieldDescription { get; set; } = string.Empty;
    public string FormatPattern { get; set; } = string.Empty;
    public string FieldTooltip { get; set; } = string.Empty;
    public string FieldIdentifier { get; set; } = string.Empty;
}
