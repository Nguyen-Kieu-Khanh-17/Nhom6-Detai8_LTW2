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
        try { return Ok(service.Add(doctorId, new DoctorSchedule { DayOfWeek = dto.DayOfWeek, StartTime = dto.StartTime, EndTime = dto.EndTime, IsDayOff = dto.IsDayOff })); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        try { service.Remove(id); return NoContent(); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }
}
