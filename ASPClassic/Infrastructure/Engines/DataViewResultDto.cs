using System.Collections.Generic;

namespace ASPClassic.Infrastructure.Engines;

/// <summary>
/// Result DTO returned by <see cref="DataViewQueryEngine"/> containing paged query results
/// in a format compatible with DataTables-style server-side processing.
/// </summary>
public class DataViewResultDto
{
    /// <summary>Draw counter echo for DataTables sequencing.</summary>
    public int Draw { get; set; }

    /// <summary>Total number of records in the dataset (before filtering).</summary>
    public int RecordsTotal { get; set; }

    /// <summary>Total number of records after filtering (search).</summary>
    public int RecordsFiltered { get; set; }

    /// <summary>
    /// The data rows. Each row is a dictionary mapping column name to string value.
    /// </summary>
    public List<Dictionary<string, string>> Data { get; set; } = new();

    /// <summary>Optional error message if the query failed.</summary>
    public string? Error { get; set; }
}
