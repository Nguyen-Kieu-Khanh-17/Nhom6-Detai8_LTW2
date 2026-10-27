using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using StudentManagement.DTOs;

namespace StudentManagement.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public string CreateToken(UserDto user)
    {
        var settings = configuration.GetSection("JwtSettings");
        var secretKey = settings["SecretKey"]
            ?? throw new InvalidOperationException("JwtSettings:SecretKey chưa được cấu hình.");
        var issuer = settings["Issuer"]
            ?? throw new InvalidOperationException("JwtSettings:Issuer chưa được cấu hình.");
        var audience = settings["Audience"]
            ?? throw new InvalidOperationException("JwtSettings:Audience chưa được cấu hình.");
        var expiryMinutes = settings.GetValue<int>("ExpiryMinutes");

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.GivenName, user.FullName),
            new Claim(ClaimTypes.Role, user.Role)
        };
        if (user.DoctorId.HasValue)
            claims.Add(new Claim("doctor_id", user.DoctorId.Value.ToString()));
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
