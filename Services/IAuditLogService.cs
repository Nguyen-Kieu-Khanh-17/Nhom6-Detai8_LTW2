using StudentManagement.Models;

namespace StudentManagement.Services;

public interface IAuditLogService
{
    Task LogAsync(string action, string performedBy, string role, string details);
    Task<List<AuditLog>> GetLogsAsync(int limit = 100);
}

