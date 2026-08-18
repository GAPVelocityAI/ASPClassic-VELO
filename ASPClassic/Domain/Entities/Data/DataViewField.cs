#nullable disable
using System;
using System.Collections.Generic;

namespace ASPClassic.Domain.Entities.Data;

/// <summary>Port of <c>ASPClassic:portal.DataViewField</c>. Deterministically generated from the plan's schema.</summary>
public class DataViewField
{
    public int ViewID { get; set; }
    public int FieldID { get; set; }
    public string FieldLabel { get; set; } = string.Empty;
    public string FieldSource { get; set; } = string.Empty;
    public string FieldType { get; set; } = string.Empty;
    public int FieldFlags { get; set; }
    public int FieldOrder { get; set; }
    public string DefaultValue { get; set; } = string.Empty;
    public int? MaxLength { get; set; }
    public string UriPath { get; set; } = string.Empty;
    public int? UriStyle { get; set; }
    public string LinkedTable { get; set; } = string.Empty;
    public string LinkedTableValueField { get; set; } = string.Empty;
    public string LinkedTableTitleField { get; set; } = string.Empty;
    public string LinkedTableGroupField { get; set; } = string.Empty;
    public string LinkedTableGlyphField { get; set; } = string.Empty;
    public string LinkedTableTooltipField { get; set; } = string.Empty;
    public string LinkedTableAddition { get; set; } = string.Empty;
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string FieldDescription { get; set; } = string.Empty;
    public string FormatPattern { get; set; } = string.Empty;
    public string FieldTooltip { get; set; } = string.Empty;
    public string FieldIdentifier { get; set; } = string.Empty;

    public virtual ASPClassic.Domain.Entities.Data.DataView View { get; set; }
}
