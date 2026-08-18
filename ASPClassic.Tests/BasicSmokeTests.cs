using ASPClassic.Infrastructure.Helpers;
using ASPClassic.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ASPClassic.Tests;

/// <summary>
/// Smoke tests that verify key infrastructure helpers compile and initialize correctly.
/// </summary>
public class BasicSmokeTests
{
    [Fact]
    public void JsonValueFormatter_SanitizeForJson_EscapesQuotes()
    {
        var formatter = new JsonValueFormatter(NullLogger<JsonValueFormatter>.Instance);
        var result    = formatter.SanitizeForJson("Hello \"World\"");
        Assert.Contains("\\\"", result);
    }

    [Fact]
    public void JsonValueFormatter_SanitizeForJson_EscapesBackslash()
    {
        var formatter = new JsonValueFormatter(NullLogger<JsonValueFormatter>.Instance);
        var result    = formatter.SanitizeForJson(@"path\to\file");
        Assert.Contains("\\\\", result);
    }

    [Fact]
    public void DataViewSecurityHelper_FilterAccessibleViews_ReturnsAllForKnownUser()
    {
        var helper  = new DataViewSecurityHelper(NullLogger<DataViewSecurityHelper>.Instance);
        var viewIds = new[] { 1, 2, 3 };
        var result  = helper.FilterAccessibleViews(viewIds, "admin").ToList();
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void DataViewSecurityHelper_CanUserAccessView_ReturnsTrueByDefault()
    {
        var helper = new DataViewSecurityHelper(NullLogger<DataViewSecurityHelper>.Instance);
        Assert.True(helper.CanUserAccessView(1, "admin"));
    }
}
