using StudentManagement.DTOs;

namespace StudentManagement.Services;

public interface IUserService
{
    List<UserDto> GetAll();
    void UpdateRole(int userId, string role);
    void AssignDoctor(int userId, int? doctorId);
    UserDto CreateUser(CreateUserDto dto);
    UserDto UpdateUser(int id, UpdateUserDto dto);
    void DeleteUser(int id);
    void SetLocked(int id, bool isLocked);
    void ResetPassword(int id, string newPassword);
}
