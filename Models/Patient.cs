namespace StudentManagement.Models;

public class Patient
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public DateOnly DateOfBirth { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string MedicalHistory { get; set; } = string.Empty;
    public string InsuranceNumber { get; set; } = string.Empty;
}