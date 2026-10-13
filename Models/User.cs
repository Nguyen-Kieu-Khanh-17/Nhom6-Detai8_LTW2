namespace StudentManagement.Models;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "Receptionist";
    public bool IsLocked { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
