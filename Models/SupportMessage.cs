namespace StudentManagement.Models;

public enum SupportMessageStatus
{
    Open,
    InProgress,
    Resolved
}

public class SupportMessage
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Reply { get; set; }
    public SupportMessageStatus Status { get; set; } = SupportMessageStatus.Open;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
