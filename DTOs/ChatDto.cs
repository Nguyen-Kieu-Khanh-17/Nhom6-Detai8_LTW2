namespace StudentManagement.DTOs;

public class CreateChatRoomDto
{
    public int? PatientUserId { get; set; }
    public string? Reason { get; set; }
}

public class SendChatMessageDto
{
    public int RoomId { get; set; }
    public string Content { get; set; } = string.Empty;
}

public class ChatRoomDto
{
    public int Id { get; set; }
    public int PatientUserId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public int? SupportUserId { get; set; }
    public string? SupportName { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? LastMessage { get; set; }
}

public class ChatMessageDto
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public int SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsFromSupport { get; set; }
    public DateTime SentAt { get; set; }
}
