using StudentManagement.DTOs;

namespace StudentManagement.Services;

public interface IJwtTokenService
{
    string CreateToken(UserDto user);
}
