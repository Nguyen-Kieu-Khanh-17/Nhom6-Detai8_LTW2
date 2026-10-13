using System.ComponentModel.DataAnnotations;

namespace StudentManagement.DTOs;

public class PrescriptionDto
{
    [Range(1, int.MaxValue)] public int MedicalRecordId { get; set; }
    [Required, StringLength(2000)] public string Medicines { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Instructions { get; set; } = string.Empty;
}
