using System.Collections.Generic;
using ASPClassic.Application.DTOs.Data;
using ASPClassic.Domain.Entities.Data;

namespace ASPClassic.Infrastructure.Caching;

/// <summary>
/// Composite DTO holding all metadata for a single DataView: header info, fields, actions, and charts.
/// Used by <see cref="DataViewCacheService"/> to cache the full metadata bundle.
/// </summary>
public class DataViewInfoDto
{
    public DataViewDto? DataView { get; set; }
    public List<DataViewFieldDto> Fields { get; set; } = new();
    public List<DataViewActionDto> Actions { get; set; } = new();
    public List<DataViewChartDto> Charts { get; set; } = new();
}
