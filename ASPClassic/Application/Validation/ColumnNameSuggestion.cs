// ═══════════════════════════════════════════════════════════════
// Property of Growth Acceleration Partners
// Author: Jose Arroyo
// ═══════════════════════════════════════════════════════════════
namespace ASPClassic.Application.Validation;

/// <summary>
/// Finds the column somebody probably meant.
/// </summary>
/// <remarks>
/// A rejection that only says "no such column" and lists ten names makes the reader do the matching.
/// Most misses are one of three things — a different separator, a shortened name, or a small typo —
/// and naming the likely column turns a puzzle into a correction. Observed live: "tax" for a column
/// called "Taxes", and "Item_Name" for "ItemName".
/// </remarks>
public static class ColumnNameSuggestion
{
    /// <summary>The column <paramref name="typed"/> most likely meant, or null when nothing is close.</summary>
    public static string? Suggest(string? typed, IEnumerable<string> columns)
    {
        if (string.IsNullOrWhiteSpace(typed)) return null;

        var candidates = columns.Where(c => !string.IsNullOrWhiteSpace(c)).ToList();
        if (candidates.Count == 0) return null;

        static string Normalise(string value) =>
            value.Replace("_", "").Replace(" ", "").Replace("-", "").ToLowerInvariant();

        var target = Normalise(typed);

        // 1. Same name, written with different separators or casing.
        var exact = candidates.FirstOrDefault(c => Normalise(c) == target);
        if (exact is not null) return exact;

        // 2. A shortened or extended form — "tax" for "Taxes", "description" for "desc". The
        //    shortest match wins, being the least of a leap from what was typed.
        // Two characters minimum: one letter matches half the table and suggests nothing useful.
        var prefix = target.Length < 2
            ? null
            : candidates
                .Where(c => Normalise(c).StartsWith(target, StringComparison.Ordinal)
                         || target.StartsWith(Normalise(c), StringComparison.Ordinal))
                .OrderBy(c => c.Length)
                .FirstOrDefault();

        if (prefix is not null) return prefix;

        // 3. A small typo. The threshold scales with length so that short names are not matched to
        //    each other on one shared letter — "SKU" and "Sum" are not the same idea.
        var best = candidates
            .Select(c => (Column: c, Distance: Distance(Normalise(c), target)))
            .OrderBy(x => x.Distance)
            .First();

        var allowed = Math.Max(1, target.Length / 4);

        return best.Distance <= allowed ? best.Column : null;
    }

    /// <summary>Levenshtein distance, iterative with two rows.</summary>
    private static int Distance(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++) previous[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;

            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;

                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }
}
