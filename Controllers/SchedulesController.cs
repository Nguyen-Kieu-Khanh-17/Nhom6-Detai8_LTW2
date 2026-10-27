using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentManagement.DTOs;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/doctors/{doctorId:int}/schedule")]
[Authorize(Roles = "Doctor,Admin")]
public class SchedulesController : ControllerBase
{
    private readonly IScheduleService service;
    public SchedulesController(IScheduleService service) { this.service = service; }

    [HttpGet]
    [AllowAnonymous]
    public ActionResult<List<DoctorSchedule>> Get(int doctorId) => Ok(service.Get(doctorId));

    [HttpPost]
    public ActionResult<DoctorSchedule> Add(int doctorId, DoctorScheduleDto dto)
    {
        if (User.IsInRole("Doctor") &&
            (!int.TryParse(User.FindFirst("doctor_id")?.Value, out var assignedDoctorId) || assignedDoctorId != doctorId))
            return Forbid();
        try { return Ok(service.Add(doctorId, new DoctorSchedule { DayOfWeek = dto.DayOfWeek, StartTime = dto.StartTime, EndTime = dto.EndTime, IsDayOff = dto.IsDayOff })); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int doctorId, int id)
    {
        int? assignedDoctorId = null;
        if (User.IsInRole("Doctor"))
        {
            if (!int.TryParse(User.FindFirst("doctor_id")?.Value, out var resolvedDoctorId) || resolvedDoctorId != doctorId)
                return Forbid();
            assignedDoctorId = resolvedDoctorId;
        }
        try { service.Remove(id, assignedDoctorId); return NoContent(); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
