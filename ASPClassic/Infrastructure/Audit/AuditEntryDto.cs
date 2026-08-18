using System;

namespace ASPClassic.Infrastructure.Audit;

/// <summary>
/// DTO representing a single audit log entry.
/// </summary>
public class AuditEntryDto
{
    public long Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
