using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.DTOs;
using StudentManagement.Models;

namespace StudentManagement.Services;

public class UserService : IUserService
{
    private readonly AppDbContext context;
    private readonly PasswordHasher<User> passwordHasher = new();

    public UserService(AppDbContext context, IConfiguration configuration)
    {
        this.context = context;
        SeedSpecialAccount(configuration, "AdminPassword", "MEDIKA_ADMIN_PASSWORD", "admin", "Quản trị viên", "Admin", "admin@medika.local", "admin123");
        SeedSpecialAccount(configuration, "DoctorPassword", "MEDIKA_DOCTOR_PASSWORD", "doctor", "Bác sĩ trực", "Doctor", "doctor@medika.local", "doctor123");
        SeedSpecialAccount(configuration, "ReceptionistPassword", "MEDIKA_RECEPTIONIST_PASSWORD", "letu", "Lê Thu", "Receptionist", "receptionist@medika.local", "receptionist123");
        SeedSpecialAccount(configuration, "SupportPassword", "MEDIKA_SUPPORT_PASSWORD", "support", "Nhân viên CSKH", "Support", "support@medika.local", "support123");
        SeedSpecialAccount(configuration, "UserPassword", "MEDIKA_USER_PASSWORD", "user", "Người dùng demo", "User", "user@medika.local", "user123");
    }

    public List<UserDto> GetAll()
    {
        return context.Users.AsNoTracking()
            .OrderBy(user => user.Id)
            .Select(user => new UserDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Username = user.Username,
                Role = user.Role,
                Email = user.Email,
                IsLocked = user.IsLocked,
                CreatedAt = user.CreatedAt
            })
            .ToList();
    }

    public UserDto? Login(LoginDto login)
    {
        var normalizedUsername = login.Username.Trim();
        var user = context.Users.FirstOrDefault(item =>
            item.Username.ToLower() == normalizedUsername.ToLower());

        if (user is null)
            return null;

        if (user.IsLocked)
            throw new InvalidOperationException("Tài khoản của bạn đã bị khóa bởi Quản trị viên.");

        var passwordMatches = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, login.Password) != PasswordVerificationResult.Failed;

        return passwordMatches ? ToDto(user) : null;
    }

    public UserDto Register(RegisterDto register)
    {
        var username = register.Username.Trim();
        if (context.Users.Any(item => item.Username.ToLower() == username.ToLower()))
            throw new InvalidOperationException("Tên đăng nhập đã tồn tại.");

        var user = new User
        {
            FullName = register.FullName.Trim(),
            Username = username,
            Email = register.Email.Trim(),
            PasswordHash = passwordHasher.HashPassword(new User(), register.Password),
            Role = "User",
            CreatedAt = DateTime.Now
        };

        context.Users.Add(user);
        context.SaveChanges();
        return ToDto(user);
    }

    public UserDto ChangePassword(int userId, string currentPassword, string newPassword)
    {
        var user = context.Users.FirstOrDefault(item => item.Id == userId)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");

        if (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
            throw new UnauthorizedAccessException("Mật khẩu hiện tại không đúng.");

        user.PasswordHash = passwordHasher.HashPassword(user, newPassword);
        context.SaveChanges();
        return ToDto(user);
    }

    public bool UserExists(string usernameOrEmail)
    {
        var value = usernameOrEmail.Trim();
        var normalized = value.ToLower();
        return context.Users.Any(item => item.Username.ToLower() == normalized ||
            item.Email.ToLower() == normalized);
    }

    public void UpdateRole(int userId, string role)
    {
        var allowedRoles = new[] { "User", "Doctor", "Receptionist", "Support", "Admin" };
        if (!allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Role không hợp lệ.");

        var user = context.Users.FirstOrDefault(item => item.Id == userId)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");

        user.Role = allowedRoles.First(item => item.Equals(role, StringComparison.OrdinalIgnoreCase));
        context.SaveChanges();
    }

    public UserDto CreateUser(CreateUserDto dto)
    {
        var username = dto.Username.Trim();
        if (context.Users.Any(u => u.Username.ToLower() == username.ToLower()))
            throw new InvalidOperationException("Tên đăng nhập đã tồn tại.");

        var allowedRoles = new[] { "User", "Doctor", "Receptionist", "Support", "Admin" };
        var role = allowedRoles.FirstOrDefault(r => r.Equals(dto.Role, StringComparison.OrdinalIgnoreCase)) ?? "User";

        var user = new User
        {
            FullName = dto.FullName.Trim(),
            Username = username,
            Email = dto.Email.Trim(),
            Role = role,
            PasswordHash = passwordHasher.HashPassword(new User(), string.IsNullOrWhiteSpace(dto.Password) ? "123456" : dto.Password),
            IsLocked = false,
            CreatedAt = DateTime.Now
        };

        context.Users.Add(user);
        context.SaveChanges();
        return ToDto(user);
    }

    public UserDto UpdateUser(int id, UpdateUserDto dto)
    {
        var user = context.Users.Find(id)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");

        user.FullName = dto.FullName.Trim();
        user.Email = dto.Email.Trim();

        var allowedRoles = new[] { "User", "Doctor", "Receptionist", "Support", "Admin" };
        if (allowedRoles.Any(r => r.Equals(dto.Role, StringComparison.OrdinalIgnoreCase)))
        {
            user.Role = allowedRoles.First(r => r.Equals(dto.Role, StringComparison.OrdinalIgnoreCase));
        }

        context.SaveChanges();
        return ToDto(user);
    }

    public void DeleteUser(int id)
    {
        var user = context.Users.Find(id)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");

        if (user.Role == "Admin" && user.Username == "admin")
            throw new InvalidOperationException("Không thể xóa tài khoản Quản trị viên gốc.");

        context.Users.Remove(user);
        context.SaveChanges();
    }

    public void SetLocked(int id, bool isLocked)
    {
        var user = context.Users.Find(id)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");

        if (user.Role == "Admin" && user.Username == "admin")
            throw new InvalidOperationException("Không thể khóa tài khoản Quản trị viên gốc.");

        user.IsLocked = isLocked;
        context.SaveChanges();
    }

    public void ResetPassword(int id, string newPassword)
    {
        var user = context.Users.Find(id)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản.");

        user.PasswordHash = passwordHasher.HashPassword(user, string.IsNullOrWhiteSpace(newPassword) ? "123456" : newPassword);
        context.SaveChanges();
    }

    private void SeedSpecialAccount(IConfiguration configuration, string secretKey, string passwordVariable, string username, string fullName, string role, string email, string fallbackPassword)
    {
        var configuredPassword = configuration[secretKey] ?? Environment.GetEnvironmentVariable(passwordVariable);
        var password = string.IsNullOrWhiteSpace(configuredPassword) ? fallbackPassword : configuredPassword;

        var existingUser = context.Users.FirstOrDefault(item => item.Username.ToLower() == username.ToLower());
        if (existingUser is not null)
        {
            var verification = passwordHasher.VerifyHashedPassword(existingUser, existingUser.PasswordHash, password);
            if (verification == PasswordVerificationResult.Failed)
            {
                existingUser.PasswordHash = passwordHasher.HashPassword(existingUser, password);
                context.SaveChanges();
            }
            return;
        }

        var user = new User
        {
            FullName = fullName,
            Username = username,
            Email = email,
            Role = role,
            PasswordHash = passwordHasher.HashPassword(new User(), password),
            CreatedAt = DateTime.Now
        };

        context.Users.Add(user);
        context.SaveChanges();
    }

    private static UserDto ToDto(User user)
    {
        return new UserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Username = user.Username,
            Role = user.Role,
            Email = user.Email,
            IsLocked = user.IsLocked,
            CreatedAt = user.CreatedAt
        };
    }
}
