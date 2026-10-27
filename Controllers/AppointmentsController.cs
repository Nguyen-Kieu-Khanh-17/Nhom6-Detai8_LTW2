using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using StudentManagement.DTOs;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "User,Doctor,Receptionist,Admin,Support")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService appointmentService;
    private readonly INotificationService notificationService;
    private readonly IPatientManager patientManager;
    private readonly IDoctorService doctorService;

    public AppointmentsController(
        IAppointmentService appointmentService,
        INotificationService notificationService,
        IPatientManager patientManager,
        IDoctorService doctorService)
    {
        this.appointmentService = appointmentService;
        this.notificationService = notificationService;
        this.patientManager = patientManager;
        this.doctorService = doctorService;
    }

    [HttpGet]
    public ActionResult<List<Appointment>> GetAll([FromQuery] DateTime? date = null, [FromQuery] AppointmentStatus? status = null)
    {
        if (User.IsInRole("User"))
            return Ok(appointmentService.GetForUser(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!)));
        if (User.IsInRole("Doctor"))
        {
            if (!TryGetDoctorId(out var doctorId))
                return Conflict(new { message = "Tài khoản bác sĩ chưa được quản trị viên gắn với hồ sơ bác sĩ." });
            return Ok(appointmentService.GetForDoctor(doctorId, date, status));
        }
        return Ok(appointmentService.GetAll(date, status));
    }

    [HttpGet("{id:int}")]
    public ActionResult<Appointment> GetById(int id)
    {
        try
        {
            var appointment = appointmentService.GetById(id);
            if (User.IsInRole("User") &&
                !appointmentService.GetForUser(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!))
                    .Any(item => item.Id == appointment.Id))
                return Forbid();
            if (User.IsInRole("Doctor") &&
                (!TryGetDoctorId(out var doctorId) || appointment.DoctorId != doctorId))
                return Forbid();
            return Ok(appointment);
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPost]
    [Authorize(Roles = "User,Receptionist,Admin,Support")]
    public async Task<ActionResult<Appointment>> Create(AppointmentDto dto)
    {
        try
        {
            var patientId = dto.PatientId;
            int? apptUserId = null;

            if (User.IsInRole("User"))
            {
                var uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                patientId = patientManager.FindByUserId(uid).Id;
                apptUserId = uid;
            }
            else
            {
                if (patientId <= 0 && dto.UserId.HasValue)
                {
                    try
                    {
                        var p = patientManager.FindByUserId(dto.UserId.Value);
                        if (p != null) patientId = p.Id;
                    }
                    catch { /* fallback to direct patientId */ }
                }
                apptUserId = dto.UserId;
            }

            var appointment = appointmentService.Add(new Appointment
            {
                PatientId = patientId,
                DoctorId = dto.DoctorId,
                UserId = apptUserId,
                AppointmentDate = dto.AppointmentDate,
                Reason = dto.Reason.Trim(),
                Status = AppointmentStatus.Pending
            });

            try
            {
                var patient = patientManager.FindById(appointment.PatientId);
                await notificationService.NotifyAppointmentCreatedAsync(appointment, patient.FullName);
            }
            catch { /* Notification failure should not break appointment creation */ }

            return CreatedAtAction(nameof(GetById), new { id = appointment.Id }, appointment);
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Receptionist,Admin")]
    public IActionResult Update(int id, AppointmentDto dto)
    {
        try
        {
            appointmentService.Update(id, new Appointment
            {
                PatientId = dto.PatientId, DoctorId = dto.DoctorId, AppointmentDate = dto.AppointmentDate,
                Reason = dto.Reason.Trim(), Status = dto.Status
            });
            return NoContent();
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPatch("{id:int}/confirm")]
    [Authorize(Roles = "Receptionist,Admin")]
    public async Task<ActionResult<Appointment>> Confirm(int id)
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var appointment = appointmentService.Confirm(id, userId);

            try
            {
                var patient = patientManager.FindById(appointment.PatientId);
                var doctor = doctorService.GetById(appointment.DoctorId);
                await notificationService.NotifyAppointmentConfirmedAsync(appointment, patient.FullName, doctor.FullName);
            }
            catch { /* Keep transaction clean */ }

            return Ok(appointment);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPatch("{id:int}/check-in")]
    [Authorize(Roles = "Receptionist,Admin")]
    public async Task<ActionResult<Appointment>> CheckIn(int id)
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var appointment = appointmentService.CheckIn(id, userId);

            try
            {
                var patient = patientManager.FindById(appointment.PatientId);
                await notificationService.NotifyPatientCheckedInAsync(appointment, patient.FullName);
            }
            catch { /* Keep transaction clean */ }

            return Ok(appointment);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPost("{id:int}/complete")]
    [Authorize(Roles = "Doctor,Admin")]
    public async Task<ActionResult<MedicalRecord>> Complete(int id, [FromBody] CompleteExamDto dto)
    {
        try
        {
            int? doctorId = null;
            if (User.IsInRole("Doctor"))
            {
                if (!TryGetDoctorId(out var assignedDoctorId))
                    return Conflict(new { message = "Tài khoản bác sĩ chưa được quản trị viên gắn với hồ sơ bác sĩ." });
                doctorId = assignedDoctorId;
            }
            var record = appointmentService.CompleteExam(id, dto, doctorId);
            
            try
            {
                var appointment = appointmentService.GetById(id);
                var patient = patientManager.FindById(appointment.PatientId);
                var doctor = doctorService.GetById(appointment.DoctorId);
                await notificationService.NotifyExamCompletedAsync(appointment, patient.FullName, doctor.FullName);
            }
            catch { /* Keep transaction clean */ }

            return Ok(record);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Receptionist,Admin")]
    public IActionResult Delete(int id)
    {
        try { appointmentService.Remove(id); return NoContent(); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPatch("{id:int}/cancel")]
    [Authorize(Roles = "User")]
    public async Task<IActionResult> Cancel(int id)
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            appointmentService.Cancel(id, userId);

            try
            {
                var appointment = appointmentService.GetById(id);
                var patient = patientManager.FindById(appointment.PatientId);
                var userName = User.FindFirstValue(ClaimTypes.Name) ?? patient.FullName;
                await notificationService.NotifyAppointmentCancelledAsync(appointment, patient.FullName, userName);
            }
            catch { /* Notification failure should not break cancel */ }

            return NoContent();
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
        catch (UnauthorizedAccessException exception) { return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message }); }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }

    [HttpPatch("{id:int}/reschedule")]
    [Authorize(Roles = "User,Doctor,Receptionist,Admin")]
    public async Task<ActionResult<Appointment>> Reschedule(int id, [FromBody] RescheduleAppointmentDto dto)
    {
        try
        {
            if (User.IsInRole("Doctor") &&
                (!TryGetDoctorId(out var doctorId) || appointmentService.GetById(id).DoctorId != doctorId))
                return Forbid();
            int? userId = null;
            if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid))
            {
                userId = uid;
            }
            var role = User.FindFirstValue(ClaimTypes.Role);

            var appointment = appointmentService.Reschedule(
                id,
                dto.NewAppointmentDate,
                dto.DoctorId,
                dto.Reason,
                userId,
                role);

            try
            {
                var patient = patientManager.FindById(appointment.PatientId);
                var doctor = doctorService.GetById(appointment.DoctorId);
                await notificationService.NotifyAppointmentRescheduledAsync(appointment, patient.FullName, doctor.FullName);
            }
            catch { /* Notification failure should not break reschedule */ }

            return Ok(appointment);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    private bool TryGetDoctorId(out int doctorId) =>
        int.TryParse(User.FindFirstValue("doctor_id"), out doctorId);
}
