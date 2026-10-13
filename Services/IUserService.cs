using StudentManagement.DTOs;

namespace StudentManagement.Services;

public interface IUserService
{
    List<UserDto> GetAll();
    UserDto? Login(LoginDto login);
    UserDto Register(RegisterDto register);
    UserDto ChangePassword(int userId, string currentPassword, string newPassword);
    bool UserExists(string usernameOrEmail);
    void UpdateRole(int userId, string role);
    UserDto CreateUser(CreateUserDto dto);
    UserDto UpdateUser(int id, UpdateUserDto dto);
    void DeleteUser(int id);
    void SetLocked(int id, bool isLocked);
    void ResetPassword(int id, string newPassword);
}
