using System.ComponentModel.DataAnnotations;

namespace StudentManagement.DTOs;

public class ForgotPasswordDto
{
    [Required]
    public string UsernameOrEmail { get; set; } = string.Empty;
}