using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentManagement.DTOs;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService authService;
    private readonly IPatientManager patientManager;
    private readonly IJwtTokenService jwtTokenService;

    public AuthController(IAuthService authService, IPatientManager patientManager, IJwtTokenService jwtTokenService)
    {
        this.authService = authService;
        this.patientManager = patientManager;
        this.jwtTokenService = jwtTokenService;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public ActionResult<UserDto> Register(RegisterDto register)
    {
        try
        {
            var user = authService.Register(register);
            patientManager.Add(new StudentManagement.Models.Patient
            {
                UserId = user.Id,
                FullName = register.FullName.Trim(),
                DateOfBirth = register.DateOfBirth,
                Phone = register.Phone.Trim(),
                Address = register.Address.Trim(),
                MedicalHistory = "Chưa cập nhật"
            });
            return Created("/api/auth/me", user);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [Authorize]
    [HttpPost("change-password")]
    public ActionResult<UserDto> ChangePassword(ChangePasswordDto password)
    {
        try
        {
            var user = authService.ChangePassword(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), password.CurrentPassword, password.NewPassword);
            return Ok(user);
        }
        catch (UnauthorizedAccessException exception) { return Unauthorized(new { message = exception.Message }); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public IActionResult ForgotPassword(ForgotPasswordDto request)
    {
        // Do not reveal whether an account exists. Email delivery can be wired in later.
        _ = authService.UserExists(request.UsernameOrEmail);
        return Ok(new { message = "Nếu tài khoản tồn tại, hướng dẫn đặt lại mật khẩu sẽ được gửi qua email." });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<UserDto>> Login(LoginDto login)
    {
        var user = authService.Login(login);
        if (user is null)
            return Unauthorized(new { message = "Tên đăng nhập hoặc mật khẩu không đúng." });

        user.AccessToken = jwtTokenService.CreateToken(user);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.GivenName, user.FullName),
            new(ClaimTypes.Role, user.Role)
        };
        if (user.DoctorId.HasValue)
            claims.Add(new Claim("doctor_id", user.DoctorId.Value.ToString()));

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });

        return Ok(user);
    }

    [Authorize]
    [HttpGet("me")]
    public ActionResult<object> Me()
    {
        return Ok(new
        {
            username = User.Identity?.Name,
            fullName = User.FindFirstValue(ClaimTypes.GivenName),
            role = User.FindFirstValue(ClaimTypes.Role),
            doctorId = int.TryParse(User.FindFirstValue("doctor_id"), out var doctorId) ? doctorId : (int?)null
        });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}