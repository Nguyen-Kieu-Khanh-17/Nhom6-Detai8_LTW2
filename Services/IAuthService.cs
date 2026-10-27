using StudentManagement.DTOs;

namespace StudentManagement.Services;

public interface IAuthService
{
    UserDto? Login(LoginDto login);
    UserDto Register(RegisterDto register);
    UserDto ChangePassword(int userId, string currentPassword, string newPassword);
    bool UserExists(string usernameOrEmail);
}
