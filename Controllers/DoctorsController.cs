using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using StudentManagement.DTOs;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "User,Doctor,Receptionist,Admin")]
public class DoctorsController : ControllerBase
{
    private readonly IDoctorService doctorService;

    public DoctorsController(IDoctorService doctorService)
    {
        this.doctorService = doctorService;
    }

    [HttpGet]
    [AllowAnonymous]
    public ActionResult<List<Doctor>> GetAll([FromQuery] string? specialty = null)
    {
        return Ok(doctorService.GetAll(specialty));
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public ActionResult<Doctor> GetById(int id)
    {
        try { return Ok(doctorService.GetById(id)); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public ActionResult<Doctor> Create(DoctorDto dto)
    {
        try
        {
            var doctor = doctorService.Add(new Doctor
            {
                Id = doctorService.GetAll().Select(item => item.Id).DefaultIfEmpty(0).Max() + 1,
                FullName = dto.FullName.Trim(),
                Specialty = dto.Specialty.Trim(),
                Phone = dto.Phone.Trim(),
                Room = dto.Room.Trim(),
                Degree = string.IsNullOrWhiteSpace(dto.Degree) ? "Bác sĩ CKI" : dto.Degree.Trim(),
                ExperienceYears = dto.ExperienceYears > 0 ? dto.ExperienceYears : 5,
                IsActive = dto.IsActive
            });
            return CreatedAtAction(nameof(GetById), new { id = doctor.Id }, doctor);
        }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public IActionResult Update(int id, DoctorDto dto)
    {
        try
        {
            doctorService.Update(id, new Doctor
            {
                FullName = dto.FullName.Trim(),
                Specialty = dto.Specialty.Trim(),
                Phone = dto.Phone.Trim(),
                Room = dto.Room.Trim(),
                Degree = dto.Degree,
                ExperienceYears = dto.ExperienceYears,
                IsActive = dto.IsActive
            });
            return NoContent();
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    public class ToggleDoctorStatusRequest
    {
        public bool IsActive { get; set; }
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = "Admin")]
    public IActionResult ToggleStatus(int id, [FromBody] ToggleDoctorStatusRequest req)
    {
        try
        {
            var doc = doctorService.GetById(id);
            doc.IsActive = req.IsActive;
            doctorService.Update(id, doc);
            return Ok(new { message = $"Đã cập nhật trạng thái bác sĩ: {(req.IsActive ? "Đang làm việc" : "Tạm nghỉ công tác")}" });
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public IActionResult Delete(int id)
    {
        try { doctorService.Remove(id); return NoContent(); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }
}
