using System.ComponentModel.DataAnnotations;
using StudentManagement.Models;

namespace StudentManagement.DTOs;

public class SupportMessageDto
{
    [Required, StringLength(150)] public string Subject { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string Content { get; set; } = string.Empty;
}

public class SupportReplyDto
{
    [Required, StringLength(2000)] public string Reply { get; set; } = string.Empty;
    public SupportMessageStatus Status { get; set; } = SupportMessageStatus.Resolved;
}
