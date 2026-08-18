#nullable disable
namespace ASPClassic.Application.DTOs.Core;

public class Page404ResultDto
{
    /// <summary>Where the visitor asked to go, and where the site root is, so the page can offer a way back.</summary>
    public string RequestedPath { get; set; } = string.Empty;
    public string BasePath { get; set; } = string.Empty;
    public string? RedirectUrl { get; set; }
    public string PageTitle { get; set; } = string.Empty;
}
