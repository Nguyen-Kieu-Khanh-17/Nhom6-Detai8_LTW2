namespace StudentManagement.Services;

public class AdminDashboardDto
{
    // Tổng quan
    public int TotalPatients { get; set; }
    public int TotalDoctors { get; set; }
    public int TotalUsers { get; set; }

    // Lịch hẹn hôm nay
    public int TodayAppointments { get; set; }
    public int TodayPending { get; set; }
    public int TodayConfirmed { get; set; }
    public int TodayCheckedIn { get; set; }
    public int TodayCompleted { get; set; }
    public int TodayCancelled { get; set; }

    // Tổng lịch hẹn toàn thời gian
    public int TotalAppointments { get; set; }
    public int TotalCompleted { get; set; }
    public int TotalCancelled { get; set; }
    public int TotalNeedReschedule { get; set; }

    // Hồ sơ bệnh án
    public int TotalMedicalRecords { get; set; }

    // Support
    public int OpenSupportTickets { get; set; }

    // Đơn xin nghỉ chờ duyệt
    public int PendingLeaveRequests { get; set; }

    // Lịch hẹn 7 ngày gần nhất (theo ngày)
    public List<DailyAppointmentStat> Last7DaysStats { get; set; } = [];

    // Top 5 bác sĩ có nhiều lịch hẹn hoàn thành nhất
    public List<DoctorStat> TopDoctors { get; set; } = [];
}

public class DailyAppointmentStat
{
    public DateOnly Date { get; set; }
    public int Total { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
}

public class DoctorStat
{
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public int CompletedCount { get; set; }
}

public class DoctorDashboardDto
{
    // Hôm nay
    public int TodayTotal { get; set; }
    public int TodayWaiting { get; set; }   // CheckedIn
    public int TodayCompleted { get; set; }
    public int TodayCancelled { get; set; }
    public int TodayPending { get; set; }

    // Danh sách bệnh nhân hôm nay (chi tiết)
    public List<TodayPatientInfo> TodayPatients { get; set; } = [];

    // Tổng cộng của bác sĩ này
    public int TotalHandled { get; set; }   // tổng đã hoàn thành
    public int TotalMedicalRecords { get; set; }

    // Lịch hẹn 7 ngày gần nhất
    public List<DailyAppointmentStat> Last7DaysStats { get; set; } = [];
}

public class TodayPatientInfo
{
    public int AppointmentId { get; set; }
    public int? QueueNumber { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime AppointmentDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public interface IDashboardService
{
    AdminDashboardDto GetAdminDashboard();
    DoctorDashboardDto GetDoctorDashboard(int doctorId);
}

