using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using StudentManagement.DTOs;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/medical-records")]
[Authorize(Roles = "User,Doctor,Receptionist,Admin")]
public class MedicalRecordsController : ControllerBase
{
    private readonly IMedicalRecordService service;
    public MedicalRecordsController(IMedicalRecordService service) { this.service = service; }

    [HttpGet]
    public ActionResult<List<MedicalRecord>> GetAll([FromQuery] int? patientId = null)
    {
        if (User.IsInRole("User"))
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var patient = HttpContext.RequestServices.GetRequiredService<IPatientManager>().FindByUserId(userId);
            return Ok(service.GetAll(patient.Id));
        }
        return Ok(service.GetAll(patientId));
    }

    [HttpGet("{id:int}")]
    public ActionResult<MedicalRecord> GetById(int id)
    {
        try { return Ok(service.GetById(id)); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPost]
    [Authorize(Roles = "Doctor,Admin")]
    public ActionResult<MedicalRecord> Create(MedicalRecordDto dto)
    {
        try
        {
            var record = service.Add(new MedicalRecord { PatientId = dto.PatientId, DoctorId = dto.DoctorId, AppointmentId = dto.AppointmentId, Symptoms = dto.Symptoms.Trim(), Diagnosis = dto.Diagnosis.Trim(), Notes = dto.Notes.Trim() });
            return CreatedAtAction(nameof(GetById), new { id = record.Id }, record);
        }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpPost("{id:int}/prescriptions")]
    [Authorize(Roles = "Doctor,Admin")]
    public ActionResult<Prescription> AddPrescription(int id, PrescriptionDto dto)
    {
        try { return Ok(service.AddPrescription(new Prescription { MedicalRecordId = id, Medicines = dto.Medicines.Trim(), Instructions = dto.Instructions.Trim() })); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [HttpGet("{id:int}/prescriptions")]
    public ActionResult<List<Prescription>> GetPrescriptions(int id)
    {
        try { return Ok(service.GetPrescriptions(id)); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }
}
