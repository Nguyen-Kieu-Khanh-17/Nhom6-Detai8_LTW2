using System.Text.Json.Serialization;

namespace StudentManagement.Models;

public class Invoice
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public int PatientId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash"; // Cash, Transfer
    public string Status { get; set; } = "Paid"; // Paid, Unpaid
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public Appointment? Appointment { get; set; }

    [JsonIgnore]
    public Patient? Patient { get; set; }
}

