namespace StudentManagement.Models;

public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Type { get; set; } = "General";
    public bool IsRead { get; set; } = false;
    public string? LinkAction { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
