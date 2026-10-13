using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentManagement.DTOs;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/support")]
[Authorize]
public class SupportController : ControllerBase
{
    private readonly ISupportService service;
    private readonly INotificationService notificationService;

    public SupportController(ISupportService service, INotificationService notificationService)
    {
        this.service = service;
        this.notificationService = notificationService;
    }

    [HttpGet]
    public ActionResult<List<SupportMessage>> GetAll()
    {
        var isStaff = User.IsInRole("Support") || User.IsInRole("Admin");
        return Ok(service.GetAll(isStaff ? null : int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)));
    }

    [HttpPost]
    [Authorize(Roles = "User")]
    public async Task<ActionResult<SupportMessage>> Create(SupportMessageDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var senderName = User.FindFirstValue(ClaimTypes.Name) ?? $"User #{userId}";

        var message = service.Add(userId, new SupportMessage
        {
            Subject = dto.Subject.Trim(),
            Content = dto.Content.Trim()
        });

        try
        {
            await notificationService.NotifyNewSupportTicketAsync(message, senderName);
        }
        catch { /* Notification failure should not break ticket creation */ }

        return Created($"/api/support/{message.Id}", message);
    }

    [HttpPatch("{id:int}/reply")]
    [Authorize(Roles = "Support,Admin")]
    public async Task<IActionResult> Reply(int id, SupportReplyDto dto)
    {
        try
        {
            service.Reply(id, dto.Reply, dto.Status);

            try
            {
                var ticket = service.GetById(id);
                var replierName = User.FindFirstValue(ClaimTypes.Name) ?? "Nhân viên hỗ trợ";
                await notificationService.NotifySupportRepliedAsync(ticket, replierName);
            }
            catch { /* Notification failure should not break reply */ }

            return NoContent();
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }
}
