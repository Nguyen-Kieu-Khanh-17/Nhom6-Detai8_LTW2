namespace StudentManagement.Models;

public class MedicalRecord
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public int? AppointmentId { get; set; }
    public DateTime ExaminedAt { get; set; }
    public string Symptoms { get; set; } = string.Empty;
    public string Diagnosis { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    
    // Clinical Vitals & Treatment Plan
    public string BloodPressure { get; set; } = string.Empty;
    public int? HeartRate { get; set; }
    public double? Temperature { get; set; }
    public double? Weight { get; set; }
    public string TreatmentPlan { get; set; } = string.Empty;
    public DateTime? FollowUpDate { get; set; }
    public string LabTests { get; set; } = string.Empty;
}
