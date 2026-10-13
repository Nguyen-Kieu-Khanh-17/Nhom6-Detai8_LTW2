using System.ComponentModel.DataAnnotations;

namespace StudentManagement.DTOs;

public class RegisterDto
{
    [Required, StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(40, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required] public DateOnly DateOfBirth { get; set; }
    [Required, Phone] public string Phone { get; set; } = string.Empty;
    [Required, StringLength(250)] public string Address { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;
}
