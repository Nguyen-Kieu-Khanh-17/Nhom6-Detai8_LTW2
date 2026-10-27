using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentManagement.DTOs;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/doctor-leaves")]
[Authorize]
public class DoctorLeavesController : ControllerBase
{
    private readonly IDoctorLeaveService leaveService;
    private readonly INotificationService notificationService;
    private readonly IDoctorService doctorService;

    public DoctorLeavesController(
        IDoctorLeaveService leaveService,
        INotificationService notificationService,
        IDoctorService doctorService)
    {
        this.leaveService = leaveService;
        this.notificationService = notificationService;
        this.doctorService = doctorService;
    }

    [HttpGet]
    public async Task<ActionResult<List<DoctorLeaveResponseDto>>> GetAll([FromQuery] int? doctorId = null)
    {
        if (User.IsInRole("Doctor"))
        {
            if (doctorId.HasValue)
                return Ok(await leaveService.GetByDoctorIdAsync(doctorId.Value));
            return Ok(await leaveService.GetAllAsync());
        }

        return Ok(await leaveService.GetAllAsync());
    }

    [HttpPost]
    [Authorize(Roles = "Doctor,Admin")]
    public async Task<ActionResult> Create([FromBody] CreateDoctorLeaveDto dto)
    {
        try
        {
            var result = await leaveService.CreateRequestAsync(dto.DoctorId, dto.LeaveDate, dto.Shift, dto.Reason);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
    }

    public class ReviewLeaveDto
    {
        public string Status { get; set; } = string.Empty;
        public string? Note { get; set; }
    }

    [HttpPatch("{id:int}/review")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DoctorLeaveResponseDto>> Review(int id, [FromBody] ReviewLeaveDto dto)
    {
        if (string.Equals(dto.Status, "Approved", StringComparison.OrdinalIgnoreCase))
            return await Approve(id);
        return await Reject(id, new RejectLeaveDto { Note = dto.Note });
    }

    [HttpPatch("{id:int}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DoctorLeaveResponseDto>> Approve(int id)
    {
        try
        {
            var adminUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await leaveService.ApproveAsync(id, adminUserId);

            try
            {
                var leaveRequest = new StudentManagement.Models.DoctorLeaveRequest
                {
                    Id = result.Id,
                    DoctorId = result.DoctorId,
                    LeaveDate = result.LeaveDate,
                    Shift = result.Shift,
                    Reason = result.Reason,
                    Status = result.Status,
                    CreatedAt = result.CreatedAt
                };
                await notificationService.NotifyDoctorLeaveStatusAsync(leaveRequest, result.DoctorName, isApproved: true);
            }
            catch { /* Notification failure should not break approval */ }

            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPatch("{id:int}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<DoctorLeaveResponseDto>> Reject(int id, [FromBody] RejectLeaveDto? dto = null)
    {
        try
        {
            var adminUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await leaveService.RejectAsync(id, adminUserId);

            try
            {
                var leaveRequest = new StudentManagement.Models.DoctorLeaveRequest
                {
                    Id = result.Id,
                    DoctorId = result.DoctorId,
                    LeaveDate = result.LeaveDate,
                    Shift = result.Shift,
                    Reason = result.Reason,
                    Status = result.Status,
                    CreatedAt = result.CreatedAt
                };
                await notificationService.NotifyDoctorLeaveStatusAsync(leaveRequest, result.DoctorName, isApproved: false, dto?.Note);
            }
            catch { /* Notification failure should not break rejection */ }

            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }
}
