using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ASPClassic.Domain.Entities.Data;

namespace ASPClassic.Infrastructure.Audit;

/// <summary>
/// Centralized audit logging service that records user actions, DataView modifications,
/// and admin operations to an in-memory audit trail store.
/// <para>Legacy source: New abstraction — the original ASP Classic app did not have centralized
/// audit logging. This provides a foundation for compliance and traceability.</para>
/// </summary>
public class AuditLogger
{
    private readonly ILogger<AuditLogger> _logger;
    private readonly ConcurrentQueue<AuditEntryDto> _auditStore = new();
    private long _idCounter;
    private const int MaxStoreSize = 10000;

    public AuditLogger(ILogger<AuditLogger> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Records an audit log entry with the specified details.
    /// Entries are stored in-memory in a bounded queue (most recent 10,000 entries).
    /// Also logs to Serilog at Information level for persistence via configured sinks.
    /// </summary>
    public Task LogAsync(string userName, string action, string entityName, string entityId, string details)
    {
        var id = System.Threading.Interlocked.Increment(ref _idCounter);

        var entry = new AuditEntryDto
        {
            Id = id,
            UserName = userName ?? string.Empty,
            Action = action ?? string.Empty,
            EntityName = entityName ?? string.Empty,
            EntityId = entityId ?? string.Empty,
            Details = details ?? string.Empty,
            Timestamp = DateTime.UtcNow
        };

        _auditStore.Enqueue(entry);

        // Trim the queue if it exceeds the maximum size
        while (_auditStore.Count > MaxStoreSize)
        {
            _auditStore.TryDequeue(out _);
        }

        _logger.LogInformation(
            "AUDIT: User={UserName}, Action={Action}, Entity={EntityName}, EntityId={EntityId}, Details={Details}",
            entry.UserName, entry.Action, entry.EntityName, entry.EntityId, entry.Details);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns the most recent audit log entries, up to the specified count.
    /// </summary>
    public Task<IEnumerable<AuditEntryDto>> GetRecentLogsAsync(int count = 100)
    {
        var recentEntries = _auditStore
            .Reverse()
            .Take(count)
            .OrderByDescending(e => e.Timestamp)
            .AsEnumerable();

        return Task.FromResult(recentEntries);
    }
}
