namespace StudentManagement.Models;

public class Doctor
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public string Degree { get; set; } = "Bác sĩ CKI";
    public int ExperienceYears { get; set; } = 5;
    public bool IsActive { get; set; } = true;
}
