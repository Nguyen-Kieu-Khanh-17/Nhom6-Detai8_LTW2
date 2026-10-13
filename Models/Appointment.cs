using System.Text.Json.Serialization;

namespace StudentManagement.Models;

public enum AppointmentStatus
{
    Pending,
    Confirmed,
    CheckedIn,
    Completed,
    Cancelled,
    NeedReschedule,
    Scheduled
}

public class Appointment
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int DoctorId { get; set; }
    public int? UserId { get; set; }
    public int? ReceptionistId { get; set; }
    public int? QueueNumber { get; set; }
    public DateTime AppointmentDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public Patient? Patient { get; set; }

    [JsonIgnore]
    public Doctor? Doctor { get; set; }

    [JsonIgnore]
    public User? Receptionist { get; set; }

    [JsonIgnore]
    public User? User { get; set; }
}

