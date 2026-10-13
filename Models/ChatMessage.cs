namespace StudentManagement.Models;

public class ChatMessage
{
    public int Id { get; set; }
    public int? RoomId { get; set; }
    public int SenderId { get; set; }
    public int ReceiverId { get; set; }
    public int RoomPatientId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsFromSupport { get; set; } = false;
    public bool IsRead { get; set; } = false;
    public DateTime SentAt { get; set; } = DateTime.Now;
}
