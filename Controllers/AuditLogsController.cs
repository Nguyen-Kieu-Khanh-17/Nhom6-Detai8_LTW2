using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = "Admin")]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService auditLogService;

    public AuditLogsController(IAuditLogService auditLogService)
    {
        this.auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<ActionResult<List<AuditLog>>> GetLogs([FromQuery] int limit = 100)
    {
        var logs = await auditLogService.GetLogsAsync(limit);
        return Ok(logs);
    }
}

