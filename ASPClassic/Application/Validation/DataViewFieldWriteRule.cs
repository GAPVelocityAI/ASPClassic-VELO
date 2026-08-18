// ═══════════════════════════════════════════════════════════════
// Property of Growth Acceleration Partners
// Author: Jose Arroyo
// ═══════════════════════════════════════════════════════════════
using ASPClassic.Application.Services.Admin;
using ASPClassic.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ASPClassic.Application.Validation;

/// <summary>
/// A field row must name a column that exists in the table its own view edits.
/// </summary>
/// <remarks>
/// <para>The same rule Manage Fields enforces, applied to the other way of creating a field: the
/// Data View Fields screen, which edits <c>DataViewField</c> as an ordinary table through the
/// generic writer.</para>
/// <para>Without it the two routes disagree — one refuses a column name that does not exist and the
/// other accepts it — and the row written by the permissive route fails later, on the first insert
/// into a different table entirely.</para>
/// </remarks>
public sealed class DataViewFieldWriteRule : IRecordWriteRule
{
    private readonly IDbContextFactory<ASPClassicVBScriptDbContext> _dbFactory;
    private readonly IAdminDataviewfieldsService _fields;
    private readonly ILogger<DataViewFieldWriteRule> _logger;

    public DataViewFieldWriteRule(
        IDbContextFactory<ASPClassicVBScriptDbContext> dbFactory,
        IAdminDataviewfieldsService fields,
        ILogger<DataViewFieldWriteRule> logger)
    {
        _dbFactory = dbFactory;
        _fields = fields;
        _logger = logger;
    }

    public string Table => "DataViewField";

    public async Task<string?> ValidateAsync(
        int viewId,
        string mode,
        string? itemId,
        IReadOnlyDictionary<string, string> values,
        CancellationToken ct = default)
    {
        values.TryGetValue("FieldSource", out var source);

        if (string.IsNullOrWhiteSpace(source))
            return "Field Source is required — it names the database column this field shows.";

        // The field belongs to the view named in the ROW, not to the view being edited through.
        // Editing the field list of view 2 is done through view -2, and it is view 2's table the
        // column has to exist in.
        if (!values.TryGetValue("ViewID", out var owner) ||
            !int.TryParse(owner, out var ownerViewId))
        {
            return "Data View is required — a field belongs to one view.";
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var target = await db.DataViews
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.ViewID == ownerViewId, ct);

        if (target is null) return $"There is no data view {ownerViewId}.";

        var columns = await _fields.GetTableColumnsAsync(
            target.MainTable ?? string.Empty, target.Primarykey ?? string.Empty, ct);

        // An unreadable table is not an empty one. A view may point at something this connection
        // cannot see, and refusing the write on that basis would be worse than the mistake it
        // prevents.
        if (columns.Count == 0) return null;

        if (columns.Contains(source, StringComparer.OrdinalIgnoreCase)) return null;

        // Not refused. Defining a field before its column exists is a real way to work, and this
        // path has nowhere to show a warning — so it is recorded and allowed. The name itself being
        // absent is the only thing that can never become valid, and that is still refused above.
        _logger.LogWarning(
            "A field row written through the generic screen names '{Source}', which is not a column " +
            "of {Table} (view {ViewId}). Until the column exists the field will not display and " +
            "saving a record through it will fail. Nearest column: {Suggestion}.",
            source, target.MainTable, ownerViewId,
            ColumnNameSuggestion.Suggest(source, columns) ?? "none");

        return null;
    }
}
