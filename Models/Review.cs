using System.Text.Json.Serialization;

namespace StudentManagement.Models;

public class Review
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public int PatientId { get; set; }
    public int? UserId { get; set; }
    public int? AppointmentId { get; set; }
    public int Rating { get; set; } // 1 - 5 sao
    public string Comment { get; set; } = string.Empty;
    public bool IsHidden { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public Doctor? Doctor { get; set; }

    [JsonIgnore]
    public Patient? Patient { get; set; }

    [JsonIgnore]
    public User? User { get; set; }

    [JsonIgnore]
    public Appointment? Appointment { get; set; }
}

