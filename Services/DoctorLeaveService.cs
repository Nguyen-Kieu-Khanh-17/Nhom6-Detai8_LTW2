using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.DTOs;
using StudentManagement.Hubs;
using StudentManagement.Models;

namespace StudentManagement.Services;

public class DoctorLeaveService : IDoctorLeaveService
{
    private readonly AppDbContext context;
    private readonly IHubContext<ClinicHub> hubContext;
    private readonly IDoctorService doctorService;

    public DoctorLeaveService(
        AppDbContext context,
        IHubContext<ClinicHub> hubContext,
        IDoctorService doctorService)
    {
        this.context = context;
        this.hubContext = hubContext;
        this.doctorService = doctorService;
    }

    public async Task<DoctorLeaveRequest> CreateRequestAsync(int doctorId, DateTime leaveDate, string shift, string reason)
    {
        var doctor = doctorService.GetById(doctorId);

        var request = new DoctorLeaveRequest
        {
            DoctorId = doctorId,
            LeaveDate = leaveDate.Date,
            Shift = string.IsNullOrWhiteSpace(shift) ? "AllDay" : shift.Trim(),
            Reason = reason.Trim(),
            Status = LeaveRequestStatus.Pending,
            CreatedAt = DateTime.Now
        };

        context.DoctorLeaveRequests.Add(request);
        await context.SaveChangesAsync();

        // Bắn thông báo Realtime tới Admin
        var adminTitle = "Yêu cầu xin nghỉ ca mới";
        var adminContent = $"Bác sĩ {doctor.FullName} gửi yêu cầu xin nghỉ ca {GetShiftDisplayName(request.Shift)} ngày {request.LeaveDate:dd/MM/yyyy}. Lý do: {request.Reason}";

        var adminUsers = await context.Users
            .Where(u => u.Role == "Admin")
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var adminId in adminUsers)
        {
            context.Notifications.Add(new Notification
            {
                UserId = adminId,
                Title = adminTitle,
                Content = adminContent,
                Type = "NEW_LEAVE_REQUEST",
                IsRead = false,
                LinkAction = "/admin.html",
                CreatedAt = DateTime.Now
            });
        }
        await context.SaveChangesAsync();

        await hubContext.Clients.Group("Admin").SendAsync("NEW_LEAVE_REQUEST", new
        {
            requestId = request.Id,
            doctorId,
            doctorName = doctor.FullName,
            leaveDate = request.LeaveDate,
            shift = request.Shift,
            reason = request.Reason,
            title = adminTitle,
            content = adminContent,
            type = "NEW_LEAVE_REQUEST"
        });

        return request;
    }

    public async Task<List<DoctorLeaveResponseDto>> GetAllAsync(LeaveRequestStatus? status = null)
    {
        var query = context.DoctorLeaveRequests.AsNoTracking().AsQueryable();
        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var requests = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
        var doctors = await context.Doctors.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.FullName);

        return requests.Select(r => new DoctorLeaveResponseDto
        {
            Id = r.Id,
            DoctorId = r.DoctorId,
            DoctorName = doctors.TryGetValue(r.DoctorId, out var name) ? name : $"Bác sĩ #{r.DoctorId}",
            LeaveDate = r.LeaveDate,
            Shift = r.Shift,
            Reason = r.Reason,
            Status = r.Status,
            CreatedAt = r.CreatedAt
        }).ToList();
    }

    public async Task<List<DoctorLeaveResponseDto>> GetByDoctorIdAsync(int doctorId)
    {
        var requests = await context.DoctorLeaveRequests
            .AsNoTracking()
            .Where(r => r.DoctorId == doctorId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var doc = doctorService.GetById(doctorId);

        return requests.Select(r => new DoctorLeaveResponseDto
        {
            Id = r.Id,
            DoctorId = r.DoctorId,
            DoctorName = doc.FullName,
            LeaveDate = r.LeaveDate,
            Shift = r.Shift,
            Reason = r.Reason,
            Status = r.Status,
            CreatedAt = r.CreatedAt
        }).ToList();
    }

    public async Task<DoctorLeaveResponseDto> ApproveAsync(int requestId, int adminUserId)
    {
        var leaveRequest = await context.DoctorLeaveRequests.FirstOrDefaultAsync(r => r.Id == requestId)
            ?? throw new KeyNotFoundException($"Không tìm thấy yêu cầu xin nghỉ #{requestId}.");

        if (leaveRequest.Status != LeaveRequestStatus.Pending)
            throw new InvalidOperationException("Yêu cầu này đã được xử lý trước đó.");

        leaveRequest.Status = LeaveRequestStatus.Approved;

        var doctor = doctorService.GetById(leaveRequest.DoctorId);
        var targetDate = leaveRequest.LeaveDate.Date;
        var shift = leaveRequest.Shift.Trim().ToLowerInvariant();
        var shiftText = GetShiftDisplayName(leaveRequest.Shift);

        // 1. Quét tìm tất cả các lịch hẹn bị ảnh hưởng trong ca đó
        var candidateAppointments = await context.Appointments
            .Where(a => a.DoctorId == leaveRequest.DoctorId
                     && a.AppointmentDate.Date == targetDate
                     && (a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed))
            .ToListAsync();

        var affectedAppointments = candidateAppointments
            .Where(a => IsInShift(a.AppointmentDate, shift))
            .ToList();

        // 2. Chuyển trạng thái lịch hẹn thành NeedReschedule
        foreach (var appt in affectedAppointments)
        {
            appt.Status = AppointmentStatus.NeedReschedule;
        }

        await context.SaveChangesAsync();

        // 3. Bắn thông báo cho từng Patient
        var patientTitle = "Bác sĩ có ca đột xuất - Cần chọn lại thời gian";
        var patientContent = $"Bác sĩ {doctor.FullName} có ca đột xuất vào ca {shiftText} ngày {leaveRequest.LeaveDate:dd/MM/yyyy}, vui lòng chọn lại thời gian hoặc bác sĩ khác.";

        foreach (var appt in affectedAppointments)
        {
            var targetUserId = appt.UserId;
            if (!targetUserId.HasValue)
            {
                targetUserId = await context.Patients
                    .Where(p => p.Id == appt.PatientId)
                    .Select(p => p.UserId)
                    .FirstOrDefaultAsync();
            }

            if (targetUserId.HasValue && targetUserId.Value > 0)
            {
                context.Notifications.Add(new Notification
                {
                    UserId = targetUserId.Value,
                    Title = patientTitle,
                    Content = patientContent,
                    Type = "RESCHEDULE_REQUIRED",
                    IsRead = false,
                    LinkAction = "/user.html",
                    CreatedAt = DateTime.Now
                });

                await hubContext.Clients.Group($"user_{targetUserId.Value}").SendAsync("RESCHEDULE_REQUIRED", new
                {
                    appointmentId = appt.Id,
                    doctorId = leaveRequest.DoctorId,
                    doctorName = doctor.FullName,
                    leaveDate = leaveRequest.LeaveDate,
                    shift = shiftText,
                    title = patientTitle,
                    content = patientContent,
                    type = "RESCHEDULE_REQUIRED"
                });
            }
        }

        // 4. Bắn thông báo cho Lễ tân
        var recTitle = $"Điều phối lại lịch khám Bác sĩ {doctor.FullName}";
        var recContent = $"Cần điều phối lại {affectedAppointments.Count} bệnh nhân của Bác sĩ {doctor.FullName} (Ca {shiftText} ngày {leaveRequest.LeaveDate:dd/MM/yyyy}).";

        var receptionists = await context.Users
            .Where(u => u.Role == "Receptionist" || u.Role == "Admin")
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var recId in receptionists)
        {
            context.Notifications.Add(new Notification
            {
                UserId = recId,
                Title = recTitle,
                Content = recContent,
                Type = "LEAVE_APPROVED_COORDINATE",
                IsRead = false,
                LinkAction = "/index.html",
                CreatedAt = DateTime.Now
            });
        }

        await context.SaveChangesAsync();

        var recPayload = new
        {
            doctorId = leaveRequest.DoctorId,
            doctorName = doctor.FullName,
            leaveDate = leaveRequest.LeaveDate,
            shift = shiftText,
            affectedCount = affectedAppointments.Count,
            title = recTitle,
            content = recContent,
            type = "LEAVE_APPROVED_COORDINATE"
        };

        await hubContext.Clients.Group("Receptionist").SendAsync("LEAVE_APPROVED_COORDINATE", recPayload);
        await hubContext.Clients.Group("Admin").SendAsync("LEAVE_APPROVED_COORDINATE", recPayload);

        return new DoctorLeaveResponseDto
        {
            Id = leaveRequest.Id,
            DoctorId = leaveRequest.DoctorId,
            DoctorName = doctor.FullName,
            LeaveDate = leaveRequest.LeaveDate,
            Shift = leaveRequest.Shift,
            Reason = leaveRequest.Reason,
            Status = leaveRequest.Status,
            CreatedAt = leaveRequest.CreatedAt,
            AffectedAppointmentsCount = affectedAppointments.Count
        };
    }

    public async Task<DoctorLeaveResponseDto> RejectAsync(int requestId, int adminUserId)
    {
        var leaveRequest = await context.DoctorLeaveRequests.FirstOrDefaultAsync(r => r.Id == requestId)
            ?? throw new KeyNotFoundException($"Không tìm thấy yêu cầu xin nghỉ #{requestId}.");

        if (leaveRequest.Status != LeaveRequestStatus.Pending)
            throw new InvalidOperationException("Yêu cầu này đã được xử lý trước đó.");

        leaveRequest.Status = LeaveRequestStatus.Rejected;
        await context.SaveChangesAsync();

        var doctor = doctorService.GetById(leaveRequest.DoctorId);

        return new DoctorLeaveResponseDto
        {
            Id = leaveRequest.Id,
            DoctorId = leaveRequest.DoctorId,
            DoctorName = doctor.FullName,
            LeaveDate = leaveRequest.LeaveDate,
            Shift = leaveRequest.Shift,
            Reason = leaveRequest.Reason,
            Status = leaveRequest.Status,
            CreatedAt = leaveRequest.CreatedAt
        };
    }

    private static bool IsInShift(DateTime dt, string shift)
    {
        var hour = dt.Hour;
        return shift switch
        {
            "morning" => hour >= 6 && hour < 12,
            "afternoon" => hour >= 12 && hour < 17,
            "evening" => hour >= 17 && hour < 22,
            _ => true // "allday" hoặc toàn bộ ca
        };
    }

    private static string GetShiftDisplayName(string shift)
    {
        return shift.ToLowerInvariant() switch
        {
            "morning" => "Sáng (07:00 - 12:00)",
            "afternoon" => "Chiều (13:00 - 17:00)",
            "evening" => "Tối (17:30 - 21:00)",
            _ => "Cả ngày"
        };
    }
}
