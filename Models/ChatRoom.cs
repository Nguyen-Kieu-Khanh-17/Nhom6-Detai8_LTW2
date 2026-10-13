namespace StudentManagement.Models;

public class ChatRoom
{
    public int Id { get; set; }
    public int PatientUserId { get; set; }
    public int? SupportUserId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, Active, Closed
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; } = DateTime.Now;
}
