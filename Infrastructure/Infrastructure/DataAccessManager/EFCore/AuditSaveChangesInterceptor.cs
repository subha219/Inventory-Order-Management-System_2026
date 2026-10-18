using System.Text.Json;
using Domain.Common;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.DataAccessManager.EFCore;

/// <summary>
/// Writes an AuditLog row for every created, updated or (soft) deleted business record, inside the same
/// transaction as the change itself. Failures here never block the original save.
/// </summary>
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly HashSet<string> Skipped = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id", "CreatedAtUtc", "CreatedById", "UpdatedAtUtc", "UpdatedById"
    };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return new ValueTask<InterceptionResult<int>>(result);
    }

    private static string? Text(object? value)
    {
        if (value == null) return null;
        var s = value is DateTime dt ? dt.ToString("yyyy-MM-dd HH:mm:ss") : value.ToString();
        return s != null && s.Length > 120 ? s.Substring(0, 120) + "..." : s;
    }

    private static void Capture(DbContext? context)
    {
        if (context == null) return;
        try
        {
            var entries = context.ChangeTracker.Entries<BaseEntity>()
                .Where(e => e.Entity is not AuditLog &&
                            (e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted))
                .ToList();
            if (entries.Count == 0) return;

            var now = DateTime.UtcNow;
            foreach (var entry in entries)
            {
                var entity = entry.Entity;
                var action = entry.State == EntityState.Added ? "Created" : entry.State == EntityState.Deleted ? "Deleted" : "Updated";
                var changes = new Dictionary<string, object?>();

                if (entry.State == EntityState.Modified)
                {
                    foreach (var p in entry.Properties.Where(p => p.IsModified && !Skipped.Contains(p.Metadata.Name)))
                    {
                        var oldV = Text(p.OriginalValue);
                        var newV = Text(p.CurrentValue);
                        if (oldV == newV) continue;
                        changes[p.Metadata.Name] = new { old = oldV, @new = newV };
                    }
                    if (changes.Count == 0) continue;
                    if (changes.ContainsKey("IsDeleted") && entity.IsDeleted) action = "Deleted";
                }
                else
                {
                    foreach (var p in entry.Properties.Where(p => !Skipped.Contains(p.Metadata.Name) && !p.Metadata.IsForeignKey()))
                    {
                        var v = Text(p.CurrentValue);
                        if (string.IsNullOrEmpty(v) || v == "False" || v == "0") continue;
                        changes[p.Metadata.Name] = v;
                        if (changes.Count >= 8) break;
                    }
                }

                var label = Text(entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Number")?.CurrentValue)
                            ?? Text(entry.Properties.FirstOrDefault(p => p.Metadata.Name == "Name")?.CurrentValue);
                var json = JsonSerializer.Serialize(changes);
                if (json.Length > 3900) json = json.Substring(0, 3900) + "...";

                context.Set<AuditLog>().Add(new AuditLog
                {
                    UserId = entity.UpdatedById ?? entity.CreatedById,
                    Action = action,
                    EntityName = entry.Metadata.ClrType.Name,
                    EntityId = entity.Id,
                    EntityLabel = label,
                    Changes = json,
                    CreatedAtUtc = now
                });
            }
        }
        catch
        {
            // Auditing must never break the business operation.
        }
    }
}
