using StudentManagement.DTOs;
using StudentManagement.Models;
using StudentManagement.Repositories;

namespace StudentManagement.Services;

public class UserService : IUserService
{
    private readonly IUserRepository userRepository;

    public UserService(IUserRepository userRepository)
    {
        this.userRepository = userRepository;
    }

    public List<UserDto> GetAll() => userRepository.GetAll().Select(ToDto).ToList();

    public void UpdateRole(int userId, string role)
    {
        var allowedRoles = new[] { "User", "Doctor", "Receptionist", "Support", "Admin" };
        if (!allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Role không hợp lệ.");

        var user = userRepository.GetById(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");

        user.Role = allowedRoles.First(item => item.Equals(role, StringComparison.OrdinalIgnoreCase));
        if (user.Role != "Doctor")
            user.DoctorId = null;
        userRepository.SaveChanges();
    }

    public void AssignDoctor(int userId, int? doctorId)
    {
        var user = userRepository.GetById(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");

        if (user.Role != "Doctor")
            throw new InvalidOperationException("Chỉ tài khoản có vai trò Bác sĩ mới được gắn hồ sơ bác sĩ.");

        if (doctorId.HasValue)
        {
            if (!userRepository.DoctorExists(doctorId.Value))
                throw new KeyNotFoundException("Không tìm thấy hồ sơ bác sĩ.");
            if (userRepository.DoctorAssignedToAnotherUser(doctorId.Value, userId))
                throw new InvalidOperationException("Hồ sơ bác sĩ này đã được gắn với một tài khoản khác.");
        }

        user.DoctorId = doctorId;
        userRepository.SaveChanges();
    }

    public UserDto CreateUser(CreateUserDto dto)
    {
        var username = dto.Username.Trim();
        if (userRepository.UsernameExists(username))
            throw new InvalidOperationException("Tên đăng nhập đã tồn tại.");

        var allowedRoles = new[] { "User", "Doctor", "Receptionist", "Support", "Admin" };
        var role = allowedRoles.FirstOrDefault(item =>
            item.Equals(dto.Role, StringComparison.OrdinalIgnoreCase)) ?? "User";

        var user = new User
        {
            FullName = dto.FullName.Trim(),
            Username = username,
            Email = dto.Email.Trim(),
            Role = role,
            PasswordHash = PasswordHashing.Hash(string.IsNullOrWhiteSpace(dto.Password) ? "123456" : dto.Password),
            IsLocked = false,
            CreatedAt = DateTime.Now
        };

        userRepository.Add(user);
        userRepository.SaveChanges();
        return ToDto(user);
    }

    public UserDto UpdateUser(int id, UpdateUserDto dto)
    {
        var user = userRepository.GetById(id)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");

        user.FullName = dto.FullName.Trim();
        user.Email = dto.Email.Trim();

        var allowedRoles = new[] { "User", "Doctor", "Receptionist", "Support", "Admin" };
        if (allowedRoles.Any(item => item.Equals(dto.Role, StringComparison.OrdinalIgnoreCase)))
        {
            user.Role = allowedRoles.First(item => item.Equals(dto.Role, StringComparison.OrdinalIgnoreCase));
            if (user.Role != "Doctor")
                user.DoctorId = null;
        }

        userRepository.SaveChanges();
        return ToDto(user);
    }

    public void DeleteUser(int id)
    {
        var user = userRepository.GetById(id)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");

        if (user.Role == "Admin" && user.Username == "admin")
            throw new InvalidOperationException("Không thể xóa tài khoản Quản trị viên gốc.");

        userRepository.Remove(user);
        userRepository.SaveChanges();
    }

    public void SetLocked(int id, bool isLocked)
    {
        var user = userRepository.GetById(id)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");

        if (user.Role == "Admin" && user.Username == "admin")
            throw new InvalidOperationException("Không thể khóa tài khoản Quản trị viên gốc.");

        user.IsLocked = isLocked;
        userRepository.SaveChanges();
    }

    public void ResetPassword(int id, string newPassword)
    {
        var user = userRepository.GetById(id)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");

        user.PasswordHash = PasswordHashing.Hash(string.IsNullOrWhiteSpace(newPassword) ? "123456" : newPassword);
        userRepository.SaveChanges();
    }

    private static UserDto ToDto(User user) => new()
    {
        Id = user.Id,
        FullName = user.FullName,
        Username = user.Username,
        Role = user.Role,
        Email = user.Email,
        IsLocked = user.IsLocked,
        DoctorId = user.DoctorId,
        CreatedAt = user.CreatedAt
    };
}
