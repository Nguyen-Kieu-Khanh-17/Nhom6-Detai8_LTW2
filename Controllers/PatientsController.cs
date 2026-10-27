using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using StudentManagement.DTOs;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PatientsController : ControllerBase
{
    private readonly IPatientManager patientManager;
    private readonly IAppointmentService appointmentService;

    public PatientsController(IPatientManager patientManager, IAppointmentService appointmentService)
    {
        this.patientManager = patientManager;
        this.appointmentService = appointmentService;
    }

    [HttpGet]
    [Authorize(Roles = "Receptionist,Admin,Doctor")]
    public ActionResult<List<Patient>> GetAll()
    {
        if (User.IsInRole("Doctor"))
        {
            if (!int.TryParse(User.FindFirstValue("doctor_id"), out var doctorId))
                return Conflict(new { message = "Tài khoản bác sĩ chưa được quản trị viên gắn với hồ sơ bác sĩ." });
            return Ok(patientManager.GetForDoctor(doctorId));
        }
        return Ok(patientManager.GetAll());
    }

    [HttpGet("me")]
    public ActionResult<Patient> GetMyProfile()
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            return Ok(patientManager.FindByUserId(userId));
        }
        catch (KeyNotFoundException)
        {
            // If patient profile doesn't exist yet, create a draft
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var fullName = User.FindFirstValue(ClaimTypes.Name) ?? "Bệnh nhân";
            var draft = new Patient
            {
                UserId = userId,
                FullName = fullName,
                DateOfBirth = new DateOnly(1995, 1, 1),
                Phone = "",
                Address = "",
                MedicalHistory = "Chưa có tiền sử bệnh",
                InsuranceNumber = ""
            };
            patientManager.Add(draft);
            return Ok(draft);
        }
    }

    [HttpPut("me")]
    public IActionResult UpdateMyProfile([FromBody] PatientDto dto)
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var patient = patientManager.FindByUserId(userId);
            patient.FullName = dto.FullName.Trim();
            patient.DateOfBirth = dto.DateOfBirth;
            patient.Phone = dto.Phone.Trim();
            patient.Address = dto.Address.Trim();
            patient.InsuranceNumber = dto.InsuranceNumber.Trim();
            if (!string.IsNullOrWhiteSpace(dto.MedicalHistory)) patient.MedicalHistory = dto.MedicalHistory.Trim();

            patientManager.Update(patient);
            return Ok(patient);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Receptionist,Admin,Doctor")]
    public ActionResult<Patient> GetById(int id)
    {
        try
        {
            var patient = patientManager.FindById(id);
            if (User.IsInRole("Doctor"))
            {
                if (!int.TryParse(User.FindFirstValue("doctor_id"), out var doctorId))
                    return Conflict(new { message = "Tài khoản bác sĩ chưa được quản trị viên gắn với hồ sơ bác sĩ." });
                if (!IsPatientAssignedToDoctor(id, doctorId))
                    return Forbid();
            }
            return Ok(patient);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    private bool IsPatientAssignedToDoctor(int patientId, int doctorId) =>
        appointmentService.GetForPatientByDoctor(patientId, doctorId).Count > 0;

    [HttpPost]
    [Authorize(Roles = "Receptionist,Admin")]
    public ActionResult<Patient> Create(PatientDto dto)
    {
        var patient = new Patient
        {
            FullName = dto.FullName,
            DateOfBirth = dto.DateOfBirth,
            Phone = dto.Phone,
            Address = dto.Address,
            MedicalHistory = dto.MedicalHistory,
            InsuranceNumber = dto.InsuranceNumber
        };

        try
        {
            patientManager.Add(patient);
            return CreatedAtAction(nameof(GetById), new { id = patient.Id }, patient);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Receptionist,Admin")]
    public IActionResult Update(int id, PatientDto dto)
    {
        var patient = new Patient
        {
            Id = id,
            FullName = dto.FullName,
            DateOfBirth = dto.DateOfBirth,
            Phone = dto.Phone,
            Address = dto.Address,
            MedicalHistory = dto.MedicalHistory,
            InsuranceNumber = dto.InsuranceNumber
        };

        try
        {
            patientManager.Update(patient);
            return NoContent();
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Receptionist,Admin")]
    public IActionResult Delete(int id)
    {
        try
        {
            patientManager.Remove(id);
            return NoContent();
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { message = exception.Message });
        }
    }
}