using StudentManagement.Models;

namespace StudentManagement.Services;

public interface INotificationService
{
    Task<List<Notification>> GetUserNotificationsAsync(int userId, int limit = 30);
    Task<int> GetUnreadCountAsync(int userId);
    Task MarkAsReadAsync(int notificationId, int userId);
    Task MarkAllAsReadAsync(int userId);
    Task<Notification> CreateNotificationAsync(int userId, string title, string content, string type, string? linkAction = null);

    // ── Event gốc ─────────────────────────────────────────────────────────
    Task NotifyAppointmentCreatedAsync(Appointment appointment, string patientName);
    Task NotifyAppointmentConfirmedAsync(Appointment appointment, string patientName, string doctorName);
    Task NotifyPatientCheckedInAsync(Appointment appointment, string patientName);
    Task NotifyExamCompletedAsync(Appointment appointment, string patientName, string doctorName);

    // ── Event mở rộng ─────────────────────────────────────────────────────
    /// <summary>User hủy lịch → notify Receptionist/Admin</summary>
    Task NotifyAppointmentCancelledAsync(Appointment appointment, string patientName, string cancelledByName);

    /// <summary>Admin/Doctor đổi lịch (NeedReschedule → Scheduled) → notify Patient</summary>
    Task NotifyAppointmentRescheduledAsync(Appointment appointment, string patientName, string doctorName);

    /// <summary>User tạo ticket hỗ trợ → notify Support/Admin</summary>
    Task NotifyNewSupportTicketAsync(SupportMessage ticket, string senderName);

    /// <summary>Support trả lời ticket → notify User</summary>
    Task NotifySupportRepliedAsync(SupportMessage ticket, string replierName);

    /// <summary>Admin duyệt/từ chối đơn xin nghỉ → notify bác sĩ</summary>
    Task NotifyDoctorLeaveStatusAsync(DoctorLeaveRequest leaveRequest, string doctorName, bool isApproved, string? adminNote = null);

    /// <summary>Bệnh nhân gửi đánh giá bác sĩ → notify Bác sĩ và Admin</summary>
    Task NotifyNewReviewAsync(Review review, string patientName, string doctorName);
}

