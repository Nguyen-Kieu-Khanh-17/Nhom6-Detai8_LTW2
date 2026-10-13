using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Admin,Receptionist")]
public class ReportsController : ControllerBase
{
    private readonly AppDbContext context;

    public ReportsController(AppDbContext context)
    {
        this.context = context;
    }

    [HttpGet("export-appointments")]
    public async Task<IActionResult> ExportAppointments()
    {
        var appointments = await context.Appointments
            .OrderByDescending(a => a.AppointmentDate)
            .ToListAsync();

        var doctors = await context.Doctors.ToListAsync();
        var patients = await context.Patients.ToListAsync();

        var sb = new StringBuilder();
        // UTF-8 BOM for Excel opening correctly in Vietnamese
        sb.Append('\uFEFF');
        sb.AppendLine("Mã Lịch,Bệnh Nhân,Bác Sĩ,Chuyên Khoa,Ngày Khám,Lý Do,Trạng Thái,Số Thứ Tự");

        foreach (var a in appointments)
        {
            var p = patients.FirstOrDefault(x => x.Id == a.PatientId);
            var d = doctors.FirstOrDefault(x => x.Id == a.DoctorId);
            var pName = (p?.FullName ?? $"#{a.PatientId}").Replace(",", " ");
            var dName = (d?.FullName ?? $"#{a.DoctorId}").Replace(",", " ");
            var spec = (d?.Specialty ?? "N/A").Replace(",", " ");
            var reason = (a.Reason ?? "").Replace(",", " ").Replace("\n", " ");

            sb.AppendLine($"{a.Id},{pName},{dName},{spec},{a.AppointmentDate:yyyy-MM-dd HH:mm},{reason},{a.Status},{a.QueueNumber}");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv; charset=utf-8", $"BaoCao_LichKham_{DateTime.Now:yyyyMMdd_HHmm}.csv");
    }

    [HttpGet("export-patients")]
    public async Task<IActionResult> ExportPatients()
    {
        var patients = await context.Patients.OrderBy(p => p.Id).ToListAsync();

        var sb = new StringBuilder();
        sb.Append('\uFEFF');
        sb.AppendLine("Mã BN,Họ Và Tên,Ngày Sinh,Số Điện Thoại,BHYT,Địa Chỉ,Tiền Sử Bệnh");

        foreach (var p in patients)
        {
            var name = p.FullName.Replace(",", " ");
            var addr = p.Address.Replace(",", " ");
            var hist = p.MedicalHistory.Replace(",", " ").Replace("\n", " ");
            sb.AppendLine($"{p.Id},{name},{p.DateOfBirth:yyyy-MM-dd},{p.Phone},{p.InsuranceNumber},{addr},{hist}");
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv; charset=utf-8", $"DanhSach_BenhNhan_{DateTime.Now:yyyyMMdd_HHmm}.csv");
    }
}

