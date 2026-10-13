using StudentManagement.Data;
using StudentManagement.Models;

namespace StudentManagement.Services;

public class DashboardService : IDashboardService
{
    private readonly AppDbContext context;

    public DashboardService(AppDbContext context)
    {
        this.context = context;
    }

    public AdminDashboardDto GetAdminDashboard()
    {
        var today = DateTime.Today;

        // ── Tổng quan ─────────────────────────────────────────────────────
        var totalPatients = context.Patients.Count();
        var totalDoctors = context.Doctors.Count();
        var totalUsers = context.Users.Count();

        // ── Lịch hẹn hôm nay ─────────────────────────────────────────────
        var todayAppointments = context.Appointments
            .Where(a => a.AppointmentDate.Date == today)
            .ToList();

        // ── Toàn bộ lịch hẹn ─────────────────────────────────────────────
        var allAppointments = context.Appointments.ToList();

        // ── Hồ sơ bệnh án ─────────────────────────────────────────────────
        var totalMedicalRecords = context.MedicalRecords.Count();

        // ── Support ticket chưa xử lý ─────────────────────────────────────
        var openSupportTickets = context.SupportMessages
            .Count(s => s.Status == SupportMessageStatus.Open || s.Status == SupportMessageStatus.InProgress);

        // ── Đơn xin nghỉ chờ duyệt ───────────────────────────────────────
        var pendingLeaveRequests = context.DoctorLeaveRequests
            .Count(l => l.Status == LeaveRequestStatus.Pending);

        // ── Thống kê 7 ngày gần nhất ──────────────────────────────────────
        var last7Days = Enumerable.Range(0, 7)
            .Select(i => today.AddDays(-6 + i))
            .ToList();

        var recentAppointments = context.Appointments
            .Where(a => a.AppointmentDate.Date >= last7Days.First() && a.AppointmentDate.Date <= today)
            .ToList();

        var last7DaysStats = last7Days.Select(day => new DailyAppointmentStat
        {
            Date = DateOnly.FromDateTime(day),
            Total = recentAppointments.Count(a => a.AppointmentDate.Date == day),
            Completed = recentAppointments.Count(a => a.AppointmentDate.Date == day && a.Status == AppointmentStatus.Completed),
            Cancelled = recentAppointments.Count(a => a.AppointmentDate.Date == day && a.Status == AppointmentStatus.Cancelled)
        }).ToList();

        // ── Top 5 bác sĩ ──────────────────────────────────────────────────
        var doctorIds = allAppointments
            .Where(a => a.Status == AppointmentStatus.Completed)
            .GroupBy(a => a.DoctorId)
            .Select(g => new { DoctorId = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .Take(5)
            .ToList();

        var doctors = context.Doctors.ToList();
        var topDoctors = doctorIds.Select(d =>
        {
            var doc = doctors.FirstOrDefault(x => x.Id == d.DoctorId);
            return new DoctorStat
            {
                DoctorId = d.DoctorId,
                DoctorName = doc?.FullName ?? "N/A",
                Specialty = doc?.Specialty ?? "N/A",
                CompletedCount = d.Count
            };
        }).ToList();

        return new AdminDashboardDto
        {
            TotalPatients = totalPatients,
            TotalDoctors = totalDoctors,
            TotalUsers = totalUsers,

            TodayAppointments = todayAppointments.Count,
            TodayPending = todayAppointments.Count(a => a.Status == AppointmentStatus.Pending),
            TodayConfirmed = todayAppointments.Count(a => a.Status == AppointmentStatus.Confirmed),
            TodayCheckedIn = todayAppointments.Count(a => a.Status == AppointmentStatus.CheckedIn),
            TodayCompleted = todayAppointments.Count(a => a.Status == AppointmentStatus.Completed),
            TodayCancelled = todayAppointments.Count(a => a.Status == AppointmentStatus.Cancelled),

            TotalAppointments = allAppointments.Count,
            TotalCompleted = allAppointments.Count(a => a.Status == AppointmentStatus.Completed),
            TotalCancelled = allAppointments.Count(a => a.Status == AppointmentStatus.Cancelled),
            TotalNeedReschedule = allAppointments.Count(a => a.Status == AppointmentStatus.NeedReschedule),

            TotalMedicalRecords = totalMedicalRecords,
            OpenSupportTickets = openSupportTickets,
            PendingLeaveRequests = pendingLeaveRequests,

            Last7DaysStats = last7DaysStats,
            TopDoctors = topDoctors
        };
    }

    public DoctorDashboardDto GetDoctorDashboard(int doctorId)
    {
        var today = DateTime.Today;

        // ── Lịch hẹn hôm nay của bác sĩ ─────────────────────────────────
        var todayList = context.Appointments
            .Where(a => a.DoctorId == doctorId && a.AppointmentDate.Date == today)
            .OrderBy(a => a.QueueNumber)
            .ThenBy(a => a.AppointmentDate)
            .ToList();

        // Lấy tên bệnh nhân
        var patientIds = todayList.Select(a => a.PatientId).Distinct().ToList();
        var patients = context.Patients
            .Where(p => patientIds.Contains(p.Id))
            .ToList();

        var todayPatients = todayList.Select(a =>
        {
            var patient = patients.FirstOrDefault(p => p.Id == a.PatientId);
            return new TodayPatientInfo
            {
                AppointmentId = a.Id,
                QueueNumber = a.QueueNumber,
                PatientName = patient?.FullName ?? "N/A",
                Reason = a.Reason,
                AppointmentDate = a.AppointmentDate,
                Status = a.Status.ToString()
            };
        }).ToList();

        // ── Tổng của bác sĩ ───────────────────────────────────────────────
        var totalHandled = context.Appointments
            .Count(a => a.DoctorId == doctorId && a.Status == AppointmentStatus.Completed);

        var totalMedicalRecords = context.MedicalRecords
            .Count(m => m.DoctorId == doctorId);

        // ── Thống kê 7 ngày gần nhất ──────────────────────────────────────
        var last7Days = Enumerable.Range(0, 7)
            .Select(i => today.AddDays(-6 + i))
            .ToList();

        var recentAppointments = context.Appointments
            .Where(a => a.DoctorId == doctorId
                     && a.AppointmentDate.Date >= last7Days.First()
                     && a.AppointmentDate.Date <= today)
            .ToList();

        var last7DaysStats = last7Days.Select(day => new DailyAppointmentStat
        {
            Date = DateOnly.FromDateTime(day),
            Total = recentAppointments.Count(a => a.AppointmentDate.Date == day),
            Completed = recentAppointments.Count(a => a.AppointmentDate.Date == day && a.Status == AppointmentStatus.Completed),
            Cancelled = recentAppointments.Count(a => a.AppointmentDate.Date == day && a.Status == AppointmentStatus.Cancelled)
        }).ToList();

        return new DoctorDashboardDto
        {
            TodayTotal = todayList.Count,
            TodayWaiting = todayList.Count(a => a.Status == AppointmentStatus.CheckedIn),
            TodayCompleted = todayList.Count(a => a.Status == AppointmentStatus.Completed),
            TodayCancelled = todayList.Count(a => a.Status == AppointmentStatus.Cancelled),
            TodayPending = todayList.Count(a => a.Status == AppointmentStatus.Pending || a.Status == AppointmentStatus.Confirmed),

            TodayPatients = todayPatients,

            TotalHandled = totalHandled,
            TotalMedicalRecords = totalMedicalRecords,

            Last7DaysStats = last7DaysStats
        };
    }
}
