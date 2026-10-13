using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.Models;

namespace StudentManagement.Services;

public class AuditLogService : IAuditLogService
{
    private readonly AppDbContext context;

    public AuditLogService(AppDbContext context)
    {
        this.context = context;
    }

    public async Task LogAsync(string action, string performedBy, string role, string details)
    {
        try
        {
            var log = new AuditLog
            {
                Action = action,
                PerformedBy = performedBy,
                Role = role,
                Details = details,
                CreatedAt = DateTime.Now
            };
            context.AuditLogs.Add(log);
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AuditLogError] {ex.Message}");
        }
    }

    public async Task<List<AuditLog>> GetLogsAsync(int limit = 100)
    {
        return await context.AuditLogs
            .OrderByDescending(l => l.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }
}

