using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.Hubs;
using StudentManagement.Models;

namespace StudentManagement.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext context;
    private readonly IHubContext<ClinicHub> hubContext;

    public NotificationService(AppDbContext context, IHubContext<ClinicHub> hubContext)
    {
        this.context = context;
        this.hubContext = hubContext;
    }

    public async Task<List<Notification>> GetUserNotificationsAsync(int userId, int limit = 30)
    {
        return await context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        return await context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task MarkAsReadAsync(int notificationId, int userId)
    {
        var item = await context.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);
        if (item != null)
        {
            item.IsRead = true;
            await context.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(int userId)
    {
        var unreadItems = await context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var item in unreadItems)
        {
            item.IsRead = true;
        }

        await context.SaveChangesAsync();
    }

    public async Task<Notification> CreateNotificationAsync(int userId, string title, string content, string type, string? linkAction = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Content = content,
            Type = type,
            IsRead = false,
            LinkAction = linkAction,
            CreatedAt = DateTime.Now
        };

        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        // Gửi realtime qua SignalR tới user tương ứng
        await hubContext.Clients.Group($"user_{userId}").SendAsync("NOTIFICATION_RECEIVED", notification);

        return notification;
    }

    // Event 1: APPOINTMENT_CREATED -> Gửi NEW_BOOKING tới Lễ tân
    public async Task NotifyAppointmentCreatedAsync(Appointment appointment, string patientName)
    {
        var title = "Lịch hẹn mới chờ duyệt";
        var content = $"Bệnh nhân {patientName} vừa đặt lịch khám lúc {appointment.AppointmentDate:dd/MM/yyyy HH:mm}. Lý do: {appointment.Reason}";
        var payload = new
        {
            appointmentId = appointment.Id,
            patientId = appointment.PatientId,
            patientName,
            doctorId = appointment.DoctorId,
            appointmentDate = appointment.AppointmentDate,
            reason = appointment.Reason,
            status = appointment.Status.ToString(),
            title,
            content,
            type = "NEW_BOOKING",
            createdAt = DateTime.Now
        };

        // 1. Lưu thông báo vào DB cho các nhân viên Lễ tân
        var receptionists = await context.Users
            .Where(u => u.Role == "Receptionist" || u.Role == "Admin")
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var recId in receptionists)
        {
            context.Notifications.Add(new Notification
            {
                UserId = recId,
                Title = title,
                Content = content,
                Type = "NEW_BOOKING",
                IsRead = false,
                LinkAction = $"/index.html?appointmentId={appointment.Id}",
                CreatedAt = DateTime.Now
            });
        }
        await context.SaveChangesAsync();

        // 2. Bắn Realtime Event NEW_BOOKING tới toàn bộ Lễ tân và Admin
        await hubContext.Clients.Group("Receptionist").SendAsync("NEW_BOOKING", payload);
        await hubContext.Clients.Group("Admin").SendAsync("NEW_BOOKING", payload);
    }

    // Event 2: APPOINTMENT_CONFIRMED -> Gửi BOOKING_SUCCESS tới Patient
    public async Task NotifyAppointmentConfirmedAsync(Appointment appointment, string patientName, string doctorName)
    {
        var targetUserId = appointment.UserId;
        if (!targetUserId.HasValue)
        {
            targetUserId = await context.Patients
                .Where(p => p.Id == appointment.PatientId)
                .Select(p => p.UserId)
                .FirstOrDefaultAsync();
        }

        var title = "Lịch khám đã được duyệt thành công";
        var content = $"Lịch khám của bạn với Bác sĩ {doctorName} lúc {appointment.AppointmentDate:dd/MM/yyyy HH:mm} đã được Lễ tân xác nhận.";
        var payload = new
        {
            appointmentId = appointment.Id,
            patientName,
            doctorName,
            appointmentDate = appointment.AppointmentDate,
            title,
            content,
            type = "BOOKING_SUCCESS",
            createdAt = DateTime.Now
        };

        if (targetUserId.HasValue && targetUserId.Value > 0)
        {
            // Lưu vào DB
            context.Notifications.Add(new Notification
            {
                UserId = targetUserId.Value,
                Title = title,
                Content = content,
                Type = "BOOKING_SUCCESS",
                IsRead = false,
                LinkAction = "/user.html",
                CreatedAt = DateTime.Now
            });
            await context.SaveChangesAsync();

            // Bắn Realtime tới riêng Patient đó
            await hubContext.Clients.Group($"user_{targetUserId.Value}").SendAsync("BOOKING_SUCCESS", payload);
        }
    }

    // Event 3: PATIENT_CHECKED_IN -> Gửi PATIENT_WAITING tới Bác sĩ chỉ định
    public async Task NotifyPatientCheckedInAsync(Appointment appointment, string patientName)
    {
        var title = "Bệnh nhân đã check-in";
        var content = $"Bệnh nhân {patientName} (STT khám: #{appointment.QueueNumber}) đã check-in tại quầy và đang chờ khám.";
        var payload = new
        {
            appointmentId = appointment.Id,
            patientId = appointment.PatientId,
            patientName,
            doctorId = appointment.DoctorId,
            queueNumber = appointment.QueueNumber,
            reason = appointment.Reason,
            title,
            content,
            type = "PATIENT_WAITING",
            createdAt = DateTime.Now
        };

        // Lưu thông báo cho các tài khoản Doctor
        var doctorUsers = await context.Users
            .Where(u => u.Role == "Doctor")
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var docUserId in doctorUsers)
        {
            context.Notifications.Add(new Notification
            {
                UserId = docUserId,
                Title = title,
                Content = content,
                Type = "PATIENT_WAITING",
                IsRead = false,
                LinkAction = "/doctor.html",
                CreatedAt = DateTime.Now
            });
        }
        await context.SaveChangesAsync();

        // Bắn Realtime tới phòng của Bác sĩ chỉ định và nhóm Bác sĩ
        await hubContext.Clients.Group($"doctor_{appointment.DoctorId}").SendAsync("PATIENT_WAITING", payload);
        await hubContext.Clients.Group("Doctor").SendAsync("PATIENT_WAITING", payload);
    }

    // Event 4: EXAM_COMPLETED -> Gửi RECORD_READY tới Patient và PAYMENT_PENDING tới Lễ tân
    public async Task NotifyExamCompletedAsync(Appointment appointment, string patientName, string doctorName)
    {
        var targetUserId = appointment.UserId;
        if (!targetUserId.HasValue)
        {
            targetUserId = await context.Patients
                .Where(p => p.Id == appointment.PatientId)
                .Select(p => p.UserId)
                .FirstOrDefaultAsync();
        }

        // 1. Thông báo cho Patient: RECORD_READY
        var patientTitle = "Kết quả khám & Đơn thuốc đã sẵn sàng";
        var patientContent = $"Bác sĩ {doctorName} đã hoàn thành ca khám cho bạn. Kết quả và đơn thuốc đã được cập nhật vào hồ sơ.";
        var patientPayload = new
        {
            appointmentId = appointment.Id,
            doctorName,
            patientName,
            title = patientTitle,
            content = patientContent,
            type = "RECORD_READY",
            createdAt = DateTime.Now
        };

        if (targetUserId.HasValue && targetUserId.Value > 0)
        {
            context.Notifications.Add(new Notification
            {
                UserId = targetUserId.Value,
                Title = patientTitle,
                Content = patientContent,
                Type = "RECORD_READY",
                IsRead = false,
                LinkAction = "/user.html",
                CreatedAt = DateTime.Now
            });

            await hubContext.Clients.Group($"user_{targetUserId.Value}").SendAsync("RECORD_READY", patientPayload);
        }

        // 2. Thông báo cho Lễ tân / Thu ngân: PAYMENT_PENDING
        var recTitle = "Bệnh nhân hoàn thành khám - Cần thanh toán";
        var recContent = $"Bệnh nhân {patientName} (STT #{appointment.QueueNumber}) đã khám xong với Bác sĩ {doctorName}. Chờ xử lý thanh toán / xuất đơn.";
        var recPayload = new
        {
            appointmentId = appointment.Id,
            patientName,
            doctorName,
            queueNumber = appointment.QueueNumber,
            title = recTitle,
            content = recContent,
            type = "PAYMENT_PENDING",
            createdAt = DateTime.Now
        };

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
                Type = "PAYMENT_PENDING",
                IsRead = false,
                LinkAction = "/index.html",
                CreatedAt = DateTime.Now
            });
        }

        await context.SaveChangesAsync();

        await hubContext.Clients.Group("Receptionist").SendAsync("PAYMENT_PENDING", recPayload);
        await hubContext.Clients.Group("Admin").SendAsync("PAYMENT_PENDING", recPayload);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Event 5: APPOINTMENT_CANCELLED → notify Receptionist/Admin
    // ─────────────────────────────────────────────────────────────────────
    public async Task NotifyAppointmentCancelledAsync(Appointment appointment, string patientName, string cancelledByName)
    {
        var title = "Lịch khám đã bị hủy";
        var content = $"Bệnh nhân {patientName} đã hủy lịch khám lúc {appointment.AppointmentDate:dd/MM/yyyy HH:mm}. Người hủy: {cancelledByName}.";
        var payload = new
        {
            appointmentId = appointment.Id,
            patientId = appointment.PatientId,
            patientName,
            cancelledByName,
            doctorId = appointment.DoctorId,
            appointmentDate = appointment.AppointmentDate,
            title,
            content,
            type = "APPOINTMENT_CANCELLED",
            createdAt = DateTime.Now
        };

        var targets = await context.Users
            .Where(u => u.Role == "Receptionist" || u.Role == "Admin")
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var uid in targets)
        {
            context.Notifications.Add(new Notification
            {
                UserId = uid,
                Title = title,
                Content = content,
                Type = "APPOINTMENT_CANCELLED",
                IsRead = false,
                LinkAction = $"/index.html?appointmentId={appointment.Id}",
                CreatedAt = DateTime.Now
            });
        }
        await context.SaveChangesAsync();

        await hubContext.Clients.Group("Receptionist").SendAsync("APPOINTMENT_CANCELLED", payload);
        await hubContext.Clients.Group("Admin").SendAsync("APPOINTMENT_CANCELLED", payload);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Event 6: APPOINTMENT_RESCHEDULED → notify Patient
    // ─────────────────────────────────────────────────────────────────────
    public async Task NotifyAppointmentRescheduledAsync(Appointment appointment, string patientName, string doctorName)
    {
        var targetUserId = appointment.UserId;
        if (!targetUserId.HasValue)
        {
            targetUserId = await context.Patients
                .Where(p => p.Id == appointment.PatientId)
                .Select(p => p.UserId)
                .FirstOrDefaultAsync();
        }

        var title = "Lịch khám đã được đặt lại thành công";
        var content = $"Lịch khám của bạn với Bác sĩ {doctorName} đã được dời sang {appointment.AppointmentDate:dd/MM/yyyy HH:mm}.";
        var payload = new
        {
            appointmentId = appointment.Id,
            patientName,
            doctorName,
            newDate = appointment.AppointmentDate,
            title,
            content,
            type = "APPOINTMENT_RESCHEDULED",
            createdAt = DateTime.Now
        };

        if (targetUserId.HasValue && targetUserId.Value > 0)
        {
            context.Notifications.Add(new Notification
            {
                UserId = targetUserId.Value,
                Title = title,
                Content = content,
                Type = "APPOINTMENT_RESCHEDULED",
                IsRead = false,
                LinkAction = "/user.html",
                CreatedAt = DateTime.Now
            });
            await context.SaveChangesAsync();
            await hubContext.Clients.Group($"user_{targetUserId.Value}").SendAsync("APPOINTMENT_RESCHEDULED", payload);
        }

        await hubContext.Clients.Group("Receptionist").SendAsync("APPOINTMENT_RESCHEDULED", payload);
        await hubContext.Clients.Group("Admin").SendAsync("APPOINTMENT_RESCHEDULED", payload);
        await hubContext.Clients.Group($"doctor_{appointment.DoctorId}").SendAsync("APPOINTMENT_RESCHEDULED", payload);
    }


    // ─────────────────────────────────────────────────────────────────────
    // Event 7: NEW_SUPPORT_TICKET → notify Support/Admin
    // ─────────────────────────────────────────────────────────────────────
    public async Task NotifyNewSupportTicketAsync(SupportMessage ticket, string senderName)
    {
        var title = "Yêu cầu hỗ trợ mới";
        var content = $"{senderName} gửi yêu cầu hỗ trợ: \"{ticket.Subject}\".";
        var payload = new
        {
            ticketId = ticket.Id,
            senderName,
            subject = ticket.Subject,
            title,
            content,
            type = "NEW_CHAT_MESSAGE", // dùng type NEW_CHAT_MESSAGE theo yêu cầu
            createdAt = DateTime.Now
        };

        var targets = await context.Users
            .Where(u => u.Role == "Support" || u.Role == "Admin")
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var uid in targets)
        {
            context.Notifications.Add(new Notification
            {
                UserId = uid,
                Title = title,
                Content = content,
                Type = "NEW_SUPPORT_TICKET",
                IsRead = false,
                LinkAction = "/support.html",
                CreatedAt = DateTime.Now
            });
        }
        await context.SaveChangesAsync();

        await hubContext.Clients.Group("Support").SendAsync("NEW_SUPPORT_TICKET", payload);
        await hubContext.Clients.Group("Admin").SendAsync("NEW_SUPPORT_TICKET", payload);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Event 8: SUPPORT_REPLIED → notify Patient (User)
    // ─────────────────────────────────────────────────────────────────────
    public async Task NotifySupportRepliedAsync(SupportMessage ticket, string replierName)
    {
        var title = "Yêu cầu hỗ trợ đã được trả lời";
        var content = $"Nhân viên {replierName} đã trả lời yêu cầu \"{ticket.Subject}\" của bạn.";
        var payload = new
        {
            ticketId = ticket.Id,
            replierName,
            subject = ticket.Subject,
            reply = ticket.Reply,
            title,
            content,
            type = "SUPPORT_REPLIED",
            createdAt = DateTime.Now
        };

        context.Notifications.Add(new Notification
        {
            UserId = ticket.UserId,
            Title = title,
            Content = content,
            Type = "SUPPORT_REPLIED",
            IsRead = false,
            LinkAction = "/user.html",
            CreatedAt = DateTime.Now
        });
        await context.SaveChangesAsync();

        await hubContext.Clients.Group($"user_{ticket.UserId}").SendAsync("SUPPORT_REPLIED", payload);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Event 9: DOCTOR_LEAVE_APPROVED / DOCTOR_LEAVE_REJECTED → notify Doctor
    // ─────────────────────────────────────────────────────────────────────
    public async Task NotifyDoctorLeaveStatusAsync(DoctorLeaveRequest leaveRequest, string doctorName, bool isApproved, string? adminNote = null)
    {
        var shiftText = leaveRequest.Shift.ToLowerInvariant() switch
        {
            "morning"   => "Sáng",
            "afternoon" => "Chiều",
            "evening"   => "Tối",
            _           => "Cả ngày"
        };

        var eventType = isApproved ? "DOCTOR_LEAVE_APPROVED" : "DOCTOR_LEAVE_REJECTED";
        var title = isApproved
            ? "Đơn xin nghỉ ca đã được duyệt"
            : "Đơn xin nghỉ ca bị từ chối";
        var content = isApproved
            ? $"Đơn xin nghỉ ca {shiftText} ngày {leaveRequest.LeaveDate:dd/MM/yyyy} của bạn đã được Admin phê duyệt."
            : $"Đơn xin nghỉ ca {shiftText} ngày {leaveRequest.LeaveDate:dd/MM/yyyy} của bạn đã bị từ chối.{(adminNote != null ? $" Ghi chú: {adminNote}" : "")}";

        var payload = new
        {
            requestId = leaveRequest.Id,
            doctorName,
            leaveDate = leaveRequest.LeaveDate,
            shift = shiftText,
            isApproved,
            adminNote,
            title,
            content,
            type = eventType,
            createdAt = DateTime.Now
        };

        // Tìm tài khoản User có role Doctor tương ứng với doctorId
        // (Doctor model và User tách rời nhau, tìm theo FullName hoặc mapping)
        // Broadcast tới group doctor_{doctorId} vì Hub cho phép bác sĩ join group này
        var doctorUsers = await context.Users
            .Where(u => u.Role == "Doctor")
            .Select(u => u.Id)
            .ToListAsync();

        // Lưu thông báo cho tất cả tài khoản Doctor (cách an toàn vì chưa có mapping doctor↔user)
        // TODO: khi có mapping DoctorUserId thì chỉ gửi cho đúng bác sĩ
        foreach (var uid in doctorUsers)
        {
            context.Notifications.Add(new Notification
            {
                UserId = uid,
                Title = title,
                Content = content,
                Type = eventType,
                IsRead = false,
                LinkAction = "/doctor.html",
                CreatedAt = DateTime.Now
            });
        }
        await context.SaveChangesAsync();

        await hubContext.Clients.Group($"doctor_{leaveRequest.DoctorId}").SendAsync(eventType, payload);
        await hubContext.Clients.Group("Doctor").SendAsync(eventType, payload);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Event 10: NEW_REVIEW → notify Doctor and Admin
    // ─────────────────────────────────────────────────────────────────────
    public async Task NotifyNewReviewAsync(Review review, string patientName, string doctorName)
    {
        var title = "Đánh giá mới từ bệnh nhân";
        var content = $"Bệnh nhân {patientName} vừa gửi đánh giá {review.Rating}⭐ cho Bác sĩ {doctorName}: \"{review.Comment}\".";
        var payload = new
        {
            reviewId = review.Id,
            doctorId = review.DoctorId,
            doctorName,
            patientId = review.PatientId,
            patientName,
            rating = review.Rating,
            comment = review.Comment,
            title,
            content,
            type = "NEW_REVIEW",
            createdAt = DateTime.Now
        };

        // Lưu thông báo cho các tài khoản Doctor
        var doctorUsers = await context.Users
            .Where(u => u.Role == "Doctor")
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var docUserId in doctorUsers)
        {
            context.Notifications.Add(new Notification
            {
                UserId = docUserId,
                Title = title,
                Content = content,
                Type = "NEW_REVIEW",
                IsRead = false,
                LinkAction = "/doctor.html",
                CreatedAt = DateTime.Now
            });
        }

        // Lưu thông báo cho các tài khoản Admin
        var adminUsers = await context.Users
            .Where(u => u.Role == "Admin")
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var adminId in adminUsers)
        {
            context.Notifications.Add(new Notification
            {
                UserId = adminId,
                Title = title,
                Content = content,
                Type = "NEW_REVIEW",
                IsRead = false,
                LinkAction = "/admin.html",
                CreatedAt = DateTime.Now
            });
        }

        await context.SaveChangesAsync();

        // Gửi realtime qua SignalR
        await hubContext.Clients.Group($"doctor_{review.DoctorId}").SendAsync("NEW_REVIEW", payload);
        await hubContext.Clients.Group("Doctor").SendAsync("NEW_REVIEW", payload);
        await hubContext.Clients.Group("Admin").SendAsync("NEW_REVIEW", payload);
    }
}

