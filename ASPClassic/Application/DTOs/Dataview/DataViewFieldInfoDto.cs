namespace ASPClassic.Application.DTOs.Dataview;

/// <summary>
/// Extended field information DTO including decoded flag bits and computed search/visibility properties.
/// Port of the per-field state built by <c>InitDataViewFields</c> in <c>inc_crudeconstants.asp</c>.
/// </summary>
public class DataViewFieldInfoDto
{
    public int FieldID { get; set; }
    public int ViewID { get; set; }
    public string FieldLabel { get; set; } = string.Empty;
    public string FieldSource { get; set; } = string.Empty;
    public string FieldType { get; set; } = string.Empty;
    public int FieldTypeNumeric { get; set; }
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

    // Decoded from FieldFlags bitmask
    // The three flags portal.DataViewFieldFlags declares that were missing here: 1 Show in Form,
    // 2 Required, 4 Read Only. Every edit form reads them, so without them the form cannot decide
    // which fields to render, which are mandatory, and which are locked.
    public bool ShowInForm { get; set; }
    public bool IsRequired { get; set; }
    public bool IsReadOnly { get; set; }

    /// <summary>Flag 8 in portal.DataViewFieldFlags — "Show in Items List".</summary>
    public bool ShowInList { get; set; }

    /// <summary>Presentation of a URI column, resolved from portal.DataViewUriStyles.</summary>
    public string? UriStyleClass { get; set; }
    public string? UriStyleGlyph { get; set; }

    public bool IsVisible { get; set; }
    public bool IsExportable { get; set; }
    public bool IsSearchable { get; set; }
    public bool IsSearchableDropdown { get; set; }
    public bool IsSearchableText { get; set; }
}
