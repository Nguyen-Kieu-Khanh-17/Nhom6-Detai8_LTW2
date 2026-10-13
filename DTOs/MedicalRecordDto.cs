using System.ComponentModel.DataAnnotations;

namespace StudentManagement.DTOs;

public class MedicalRecordDto
{
    [Range(1, int.MaxValue)] public int PatientId { get; set; }
    [Range(1, int.MaxValue)] public int DoctorId { get; set; }
    public int? AppointmentId { get; set; }
    [Required, StringLength(500)] public string Symptoms { get; set; } = string.Empty;
    [Required, StringLength(500)] public string Diagnosis { get; set; } = string.Empty;
    [StringLength(1000)] public string Notes { get; set; } = string.Empty;
}
