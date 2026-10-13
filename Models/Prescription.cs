namespace StudentManagement.Models;

public class Prescription
{
    public int Id { get; set; }
    public int MedicalRecordId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Medicines { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
}
