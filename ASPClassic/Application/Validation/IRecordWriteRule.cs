// ═══════════════════════════════════════════════════════════════
// Property of Growth Acceleration Partners
// Author: Jose Arroyo
// ═══════════════════════════════════════════════════════════════
namespace ASPClassic.Application.Validation;

/// <summary>
/// A rule that must hold before a record of one particular table is written.
/// </summary>
/// <remarks>
/// <para>The generic writer edits whatever table a data view names, and knows nothing about what any
/// of them MEAN. That is the point of the design, and it is also why a row can be written that is
/// structurally valid and semantically impossible — a field row naming a column that does not
/// exist, which fails much later on an unrelated screen.</para>
/// <para>Putting such a check inside the writer would make the generic path stop being generic, one
/// table at a time. Instead the writer asks whether any rule applies to the table it is about to
/// write, and the knowledge of what a particular table means lives in its own rule. Adding a rule
/// requires no change to the writer; removing every rule leaves it exactly as general as before.</para>
/// </remarks>
public interface IRecordWriteRule
{
    /// <summary>The bare table name this rule applies to, matched case-insensitively.</summary>
    string Table { get; }

    /// <summary>
    /// The error to show, or null when the record may be written.
    /// </summary>
    /// <param name="viewId">The data view through which the write is being made.</param>
    /// <param name="mode">"add", "edit" or "clone".</param>
    /// <param name="itemId">Key of the record being edited, absent when adding.</param>
    /// <param name="values">Column name to value, as the form supplied them.</param>
    Task<string?> ValidateAsync(
        int viewId,
        string mode,
        string? itemId,
        IReadOnlyDictionary<string, string> values,
        CancellationToken ct = default);
}
