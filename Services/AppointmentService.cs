using StudentManagement.Data;
using StudentManagement.DTOs;
using StudentManagement.Models;

namespace StudentManagement.Services;

public class AppointmentService : IAppointmentService
{
    private readonly AppDbContext context;
    private readonly IPatientManager patientManager;
    private readonly IDoctorService doctorService;

    public AppointmentService(
        AppDbContext context,
        IPatientManager patientManager,
        IDoctorService doctorService)
    {
        this.context = context;
        this.patientManager = patientManager;
        this.doctorService = doctorService;
    }

    public List<Appointment> GetAll(DateTime? date = null, AppointmentStatus? status = null)
    {
        var query = context.Appointments.AsQueryable();
        if (date.HasValue)
            query = query.Where(item => item.AppointmentDate.Date == date.Value.Date);
        if (status.HasValue)
            query = query.Where(item => item.Status == status.Value);

        return query.OrderBy(item => item.AppointmentDate).ToList();
    }

    public List<Appointment> GetForUser(int userId)
    {
        return context.Appointments
            .Where(item => item.UserId == userId ||
                context.Patients.Any(patient => patient.UserId == userId && patient.Id == item.PatientId))
            .OrderByDescending(item => item.AppointmentDate)
            .ToList();
    }

    public List<Appointment> GetForDoctor(int doctorId, DateTime? date = null, AppointmentStatus? status = null)
    {
        var query = context.Appointments.Where(item => item.DoctorId == doctorId);
        if (date.HasValue)
            query = query.Where(item => item.AppointmentDate.Date == date.Value.Date);
        if (status.HasValue)
            query = query.Where(item => item.Status == status.Value);
        return query.OrderBy(item => item.AppointmentDate).ToList();
    }

    public List<Appointment> GetForPatientByDoctor(int patientId, int doctorId) =>
        context.Appointments
            .Where(item => item.PatientId == patientId && item.DoctorId == doctorId)
            .OrderByDescending(item => item.AppointmentDate)
            .ToList();

    public Appointment GetById(int id)
    {
        return context.Appointments.FirstOrDefault(item => item.Id == id)
            ?? throw new KeyNotFoundException($"Không tìm thấy lịch hẹn có mã {id}.");
    }

    public Appointment Add(Appointment appointment)
    {
        if (appointment.AppointmentDate <= DateTime.Now)
            throw new ArgumentException("Thời gian khám phải sau thời điểm hiện tại.");
        if (string.IsNullOrWhiteSpace(appointment.Reason))
            throw new ArgumentException("Vui lòng nhập lý do khám.");
        ValidateReferences(appointment);
        EnsureDoctorIsAvailable(appointment);

        context.Appointments.Add(appointment);
        context.SaveChanges();
        return appointment;
    }

    public void Update(int id, Appointment appointment)
    {
        ValidateReferences(appointment);
        EnsureDoctorIsAvailable(appointment, id);

        var existingAppointment = GetById(id);
        existingAppointment.PatientId = appointment.PatientId;
        existingAppointment.DoctorId = appointment.DoctorId;
        existingAppointment.AppointmentDate = appointment.AppointmentDate;
        existingAppointment.Reason = appointment.Reason;
        context.SaveChanges();
    }

    public void Remove(int id)
    {
        var appointment = GetById(id);
        context.Appointments.Remove(appointment);
        context.SaveChanges();
    }

    public void Cancel(int id, int userId)
    {
        var appointment = GetById(id);
        if (!GetForUser(userId).Any(item => item.Id == appointment.Id))
            throw new UnauthorizedAccessException("Bạn không có quyền thao tác lịch hẹn này.");
        if (appointment.Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed or AppointmentStatus.NeedReschedule))
            throw new InvalidOperationException("Chỉ lịch hẹn đang chờ hoặc đã được duyệt mới có thể hủy.");

        appointment.Status = AppointmentStatus.Cancelled;
        context.SaveChanges();
    }

    public Appointment Confirm(int id, int? receptionistUserId = null)
    {
        var appointment = GetById(id);
        if (appointment.Status != AppointmentStatus.Pending)
            throw new InvalidOperationException("Chỉ lịch hẹn đang chờ mới được duyệt.");
        EnsureDoctorIsAvailable(appointment, id);
        appointment.Status = AppointmentStatus.Confirmed;
        if (receptionistUserId.HasValue)
            appointment.ReceptionistId = receptionistUserId.Value;
        context.SaveChanges();
        return appointment;
    }

    public Appointment CheckIn(int id, int? receptionistUserId = null)
    {
        var appointment = GetById(id);
        if (appointment.Status != AppointmentStatus.Confirmed && appointment.Status != AppointmentStatus.Pending)
            throw new InvalidOperationException("Chỉ lịch hẹn đang chờ hoặc đã được duyệt mới được check-in.");
        appointment.Status = AppointmentStatus.CheckedIn;
        if (receptionistUserId.HasValue)
            appointment.ReceptionistId = receptionistUserId.Value;

        if (!appointment.QueueNumber.HasValue)
        {
            var maxQueue = context.Appointments
                .Where(a => a.AppointmentDate.Date == appointment.AppointmentDate.Date && a.QueueNumber.HasValue)
                .Max(a => (int?)a.QueueNumber) ?? 0;
            appointment.QueueNumber = maxQueue + 1;
        }

        context.SaveChanges();
        return appointment;
    }

    public MedicalRecord CompleteExam(int id, CompleteExamDto dto, int? doctorId = null)
    {
        var appointment = GetById(id);
        if (doctorId.HasValue && appointment.DoctorId != doctorId.Value)
            throw new UnauthorizedAccessException("Ca khám này được phân công cho bác sĩ khác.");
        if (appointment.Status != AppointmentStatus.CheckedIn)
            throw new InvalidOperationException("Chỉ ca khám đã check-in mới được hoàn tất.");
        if (context.MedicalRecords.Any(record => record.AppointmentId == appointment.Id))
            throw new InvalidOperationException("Ca khám này đã có hồ sơ bệnh án.");
        appointment.Status = AppointmentStatus.Completed;

        var record = new MedicalRecord
        {
            PatientId = appointment.PatientId,
            DoctorId = appointment.DoctorId,
            AppointmentId = appointment.Id,
            ExaminedAt = DateTime.Now,
            Symptoms = (dto.Symptoms ?? "").Trim(),
            Diagnosis = (dto.Diagnosis ?? "").Trim(),
            Notes = (dto.Notes ?? "").Trim(),
            BloodPressure = (dto.BloodPressure ?? "").Trim(),
            HeartRate = dto.HeartRate,
            Temperature = dto.Temperature,
            Weight = dto.Weight,
            TreatmentPlan = (dto.TreatmentPlan ?? "").Trim(),
            FollowUpDate = dto.FollowUpDate,
            LabTests = (dto.LabTests ?? "").Trim()
        };
        context.MedicalRecords.Add(record);
        context.SaveChanges();

        if (!string.IsNullOrWhiteSpace(dto.Medicines))
        {
            var prescription = new Prescription
            {
                MedicalRecordId = record.Id,
                CreatedAt = DateTime.Now,
                Medicines = dto.Medicines.Trim(),
                Instructions = (dto.Instructions ?? "").Trim()
            };
            context.Prescriptions.Add(prescription);
            context.SaveChanges();
        }

        return record;
    }

    public Appointment Reschedule(int id, DateTime newDate, int? newDoctorId = null, string? reason = null, int? userId = null, string? role = null)
    {
        var appointment = GetById(id);

        if (role == "User" && userId.HasValue && appointment.UserId != userId.Value)
        {
            var patient = patientManager.FindByUserId(userId.Value);
            if (patient == null || patient.Id != appointment.PatientId)
                throw new UnauthorizedAccessException("Bạn không có quyền đổi lịch hẹn này.");
        }

        if (appointment.Status == AppointmentStatus.Completed || appointment.Status == AppointmentStatus.Cancelled)
        {
            throw new InvalidOperationException("Không thể đổi lịch hẹn đã hoàn thành hoặc đã bị hủy.");
        }

        if (newDate <= DateTime.Now)
        {
            throw new ArgumentException("Thời gian khám mới phải sau thời điểm hiện tại.");
        }

        var targetDoctorId = newDoctorId ?? appointment.DoctorId;
        doctorService.GetById(targetDoctorId);

        // Kiểm tra lịch nghỉ phép được duyệt của bác sĩ
        var doctorLeaves = context.DoctorLeaveRequests
            .Where(l => l.DoctorId == targetDoctorId && l.Status == LeaveRequestStatus.Approved && l.LeaveDate.Date == newDate.Date)
            .ToList();

        foreach (var leave in doctorLeaves)
        {
            if (IsInShift(newDate, leave.Shift.ToLowerInvariant()))
            {
                throw new InvalidOperationException($"Bác sĩ đã có lịch nghỉ phép ({leave.Shift}) vào ca này.");
            }
        }

        // Kiểm tra trùng lịch hẹn với bác sĩ đó
        var hasConflict = context.Appointments.Any(item =>
            item.Id != id &&
            item.DoctorId == targetDoctorId &&
            (item.Status == AppointmentStatus.Confirmed || item.Status == AppointmentStatus.Scheduled) &&
            item.AppointmentDate == newDate);

        if (hasConflict)
            throw new InvalidOperationException("Bác sĩ đã có lịch hẹn trùng thời gian này.");

        appointment.AppointmentDate = newDate;
        appointment.DoctorId = targetDoctorId;
        if (!string.IsNullOrWhiteSpace(reason))
            appointment.Reason = reason.Trim();

        // Nếu trước đó đang chờ đổi lịch (NeedReschedule), chuyển về Confirmed
        if (appointment.Status == AppointmentStatus.NeedReschedule)
            appointment.Status = AppointmentStatus.Confirmed;

        appointment.QueueNumber = null;
        context.SaveChanges();
        return appointment;
    }

    private void ValidateReferences(Appointment appointment)
    {
        patientManager.FindById(appointment.PatientId);
        doctorService.GetById(appointment.DoctorId);
    }

    private void EnsureDoctorIsAvailable(Appointment appointment, int? ignoredId = null)
    {
        var hasConflict = context.Appointments.Any(item =>
            item.Id != ignoredId &&
            item.DoctorId == appointment.DoctorId &&
            (item.Status == AppointmentStatus.Confirmed ||
             item.Status == AppointmentStatus.CheckedIn ||
             item.Status == AppointmentStatus.Scheduled) &&
            item.AppointmentDate == appointment.AppointmentDate);

        if (hasConflict)
            throw new InvalidOperationException("Bác sĩ đã có lịch hẹn trùng thời gian này.");

        var doctorLeaves = context.DoctorLeaveRequests
            .Where(l => l.DoctorId == appointment.DoctorId && l.Status == LeaveRequestStatus.Approved && l.LeaveDate.Date == appointment.AppointmentDate.Date)
            .ToList();

        foreach (var leave in doctorLeaves)
        {
            if (IsInShift(appointment.AppointmentDate, leave.Shift.ToLowerInvariant()))
            {
                throw new InvalidOperationException($"Bác sĩ đã có lịch nghỉ phép ({leave.Shift}) vào ca này.");
            }
        }
    }

    private static bool IsInShift(DateTime dt, string shift)
    {
        var hour = dt.Hour;
        return shift switch
        {
            "morning" => hour >= 6 && hour < 12,
            "afternoon" => hour >= 12 && hour < 17,
            "evening" => hour >= 17 && hour < 22,
            _ => true // "allday"
        };
    }
}
