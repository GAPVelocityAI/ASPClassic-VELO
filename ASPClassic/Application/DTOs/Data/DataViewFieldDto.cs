#nullable disable
using System;
using System.Collections.Generic;

namespace ASPClassic.Application.DTOs.Data;

/// <summary>DataViewField data transfer object (domain: Data).</summary>
public class DataViewFieldDto
{
    public int ViewID { get; set; }
    public int FieldID { get; set; }
    public string FieldLabel { get; set; } = string.Empty;
    public string? FieldSource { get; set; }
    public string FieldType { get; set; } = string.Empty;
    public int FieldFlags { get; set; }
    public int FieldOrder { get; set; }
    public string? DefaultValue { get; set; }
    public int? MaxLength { get; set; }
    public string? UriPath { get; set; }
    public int? UriStyle { get; set; }
    public string? LinkedTable { get; set; }
    public string? LinkedTableValueField { get; set; }
    public string? LinkedTableTitleField { get; set; }
    public string? LinkedTableGroupField { get; set; }
    public string? LinkedTableGlyphField { get; set; }
    public string? LinkedTableTooltipField { get; set; }
    public string? LinkedTableAddition { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? FieldDescription { get; set; }
    public string? FormatPattern { get; set; }
    public string? FieldTooltip { get; set; }
    public string FieldIdentifier { get; set; } = string.Empty;
}
