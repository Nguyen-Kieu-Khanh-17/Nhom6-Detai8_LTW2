namespace StudentManagement.DTOs;

public class CompleteExamDto
{
    public string Symptoms { get; set; } = string.Empty;
    public string Diagnosis { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string Medicines { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;

    // Clinical Vitals & Testing
    public string BloodPressure { get; set; } = string.Empty;
    public int? HeartRate { get; set; }
    public double? Temperature { get; set; }
    public double? Weight { get; set; }
    public string TreatmentPlan { get; set; } = string.Empty;
    public DateTime? FollowUpDate { get; set; }
    public string LabTests { get; set; } = string.Empty;
}
