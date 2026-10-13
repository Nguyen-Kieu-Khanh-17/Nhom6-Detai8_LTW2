using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService dashboardService;
    private readonly IDoctorService doctorService;

    public DashboardController(IDashboardService dashboardService, IDoctorService doctorService)
    {
        this.dashboardService = dashboardService;
        this.doctorService = doctorService;
    }

    /// <summary>
    /// Dashboard tổng quan cho Admin.
    /// Trả về: tổng số bệnh nhân/bác sĩ/user, thống kê lịch hẹn hôm nay,
    /// lịch sử 7 ngày, top bác sĩ, support tickets chưa xử lý, đơn nghỉ chờ duyệt.
    /// </summary>
    [HttpGet]
    [HttpGet("admin")]
    [Authorize(Roles = "Admin,Receptionist")]
    public ActionResult<AdminDashboardDto> GetAdminDashboard()
    {
        return Ok(dashboardService.GetAdminDashboard());
    }

    /// <summary>
    /// Dashboard cho Bác sĩ.
    /// Trả về: danh sách bệnh nhân hôm nay (có số thứ tự, trạng thái),
    /// thống kê hôm nay và 7 ngày gần nhất.
    /// Admin có thể xem dashboard của bất kỳ bác sĩ nào bằng cách truyền ?doctorId=.
    /// </summary>
    [HttpGet("doctor")]
    [Authorize(Roles = "Doctor,Admin")]
    public ActionResult<DoctorDashboardDto> GetDoctorDashboard([FromQuery] int? doctorId = null)
    {
        int resolvedDoctorId;

        if (User.IsInRole("Admin") && doctorId.HasValue)
        {
            // Admin xem dashboard của bác sĩ cụ thể
            resolvedDoctorId = doctorId.Value;
        }
        else if (User.IsInRole("Doctor"))
        {
            // Bác sĩ xem dashboard của chính mình
            if (doctorId.HasValue)
            {
                resolvedDoctorId = doctorId.Value;
            }
            else
            {
                return BadRequest(new { message = "Vui lòng truyền doctorId của bạn." });
            }
        }
        else
        {
            return Forbid();
        }

        // Kiểm tra bác sĩ tồn tại
        try { doctorService.GetById(resolvedDoctorId); }
        catch (KeyNotFoundException) { return NotFound(new { message = $"Không tìm thấy bác sĩ có mã {resolvedDoctorId}." }); }

        return Ok(dashboardService.GetDoctorDashboard(resolvedDoctorId));
    }
}
