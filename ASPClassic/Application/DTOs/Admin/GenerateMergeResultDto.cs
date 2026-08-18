namespace ASPClassic.Application.DTOs.Admin;

/// <summary>
/// Result of the usp_Generate_Merge_For_Table stored procedure call.
/// </summary>
public class GenerateMergeResultDto
{
    public bool Success { get; set; }
    public string GeneratedSql { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
