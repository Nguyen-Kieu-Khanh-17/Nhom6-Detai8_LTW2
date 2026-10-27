using System.Text.Json.Serialization;

namespace StudentManagement.DTOs;

public class UserDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int? DoctorId { get; set; }
    public string Email { get; set; } = string.Empty;
    public bool IsLocked { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AccessToken { get; set; }
}

public class CreateUserDto
{
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
}

public class UpdateUserDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
}

public class AssignDoctorDto
{
    public int? DoctorId { get; set; }
}
