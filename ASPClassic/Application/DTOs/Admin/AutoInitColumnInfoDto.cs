namespace ASPClassic.Application.DTOs.Admin;

/// <summary>
/// Internal DTO holding column introspection results during AutoInit.
/// </summary>
public class AutoInitColumnInfoDto
{
    public string ColumnName { get; set; } = string.Empty;
    public string FieldType { get; set; } = "1";
    public int FieldFlags { get; set; } = 1;
    public int FieldOrder { get; set; }
    public string FieldDefault { get; set; } = string.Empty;
    public int? MaxLength { get; set; }
    public string LinkedTable { get; set; } = string.Empty;
    public string LinkedColumnValue { get; set; } = string.Empty;
    public string LinkedColumnLabel { get; set; } = string.Empty;
}
