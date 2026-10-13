using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.DTOs;
using StudentManagement.Models;

namespace StudentManagement.Services;

public class ReviewService : IReviewService
{
    private readonly AppDbContext context;
    private readonly IPatientManager patientManager;
    private readonly IDoctorService doctorService;
    private readonly INotificationService notificationService;

    public ReviewService(
        AppDbContext context,
        IPatientManager patientManager,
        IDoctorService doctorService,
        INotificationService notificationService)
    {
        this.context = context;
        this.patientManager = patientManager;
        this.doctorService = doctorService;
        this.notificationService = notificationService;
    }

    public async Task<ReviewResponseDto> CreateReviewAsync(int userId, CreateReviewDto dto)
    {
        var doctor = doctorService.GetById(dto.DoctorId);

        var patient = patientManager.FindByUserId(userId);
        if (patient == null)
        {
            throw new InvalidOperationException("Bạn cần có hồ sơ bệnh nhân để thực hiện đánh giá.");
        }

        if (dto.AppointmentId.HasValue)
        {
            var appointment = await context.Appointments.FirstOrDefaultAsync(a => a.Id == dto.AppointmentId.Value)
                ?? throw new KeyNotFoundException($"Không tìm thấy lịch hẹn #{dto.AppointmentId.Value}.");

            if (appointment.PatientId != patient.Id && appointment.UserId != userId)
            {
                throw new UnauthorizedAccessException("Lịch hẹn này không thuộc về bạn.");
            }

            if (appointment.DoctorId != dto.DoctorId)
            {
                throw new ArgumentException("Bác sĩ được đánh giá không khớp với bác sĩ của lịch hẹn.");
            }

            if (appointment.Status != AppointmentStatus.Completed)
            {
                throw new InvalidOperationException("Chỉ có thể đánh giá sau khi buổi khám đã hoàn thành.");
            }

            var alreadyReviewed = await context.Reviews.AnyAsync(r => r.AppointmentId == dto.AppointmentId.Value);
            if (alreadyReviewed)
            {
                throw new InvalidOperationException("Lịch hẹn này đã được đánh giá trước đó.");
            }
        }
        else
        {
            // Nếu không truyền AppointmentId, kiểm tra xem bệnh nhân đã từng khám hoàn thành với bác sĩ này chưa
            var hasCompleted = await context.Appointments.AnyAsync(a =>
                (a.PatientId == patient.Id || a.UserId == userId) &&
                a.DoctorId == dto.DoctorId &&
                a.Status == AppointmentStatus.Completed);

            if (!hasCompleted)
            {
                throw new InvalidOperationException("Bạn cần hoàn thành ít nhất một buổi khám với bác sĩ này trước khi gửi đánh giá.");
            }
        }

        var review = new Review
        {
            DoctorId = dto.DoctorId,
            PatientId = patient.Id,
            UserId = userId,
            AppointmentId = dto.AppointmentId,
            Rating = dto.Rating,
            Comment = dto.Comment.Trim(),
            IsHidden = false,
            CreatedAt = DateTime.Now
        };

        context.Reviews.Add(review);
        await context.SaveChangesAsync();

        try
        {
            await notificationService.NotifyNewReviewAsync(review, patient.FullName, doctor.FullName);
        }
        catch { /* Không làm gián đoạn việc lưu review nếu gửi thông báo lỗi */ }

        return new ReviewResponseDto
        {
            Id = review.Id,
            DoctorId = review.DoctorId,
            DoctorName = doctor.FullName,
            DoctorSpecialty = doctor.Specialty,
            PatientId = review.PatientId,
            PatientName = patient.FullName,
            AppointmentId = review.AppointmentId,
            Rating = review.Rating,
            Comment = review.Comment,
            IsHidden = review.IsHidden,
            CreatedAt = review.CreatedAt
        };
    }

    public async Task<DoctorReviewSummaryDto> GetDoctorReviewsAsync(int doctorId, bool includeHidden = false)
    {
        var doctor = doctorService.GetById(doctorId);

        var query = context.Reviews
            .AsNoTracking()
            .Where(r => r.DoctorId == doctorId);

        if (!includeHidden)
            query = query.Where(r => !r.IsHidden);

        var reviews = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var patientIds = reviews.Select(r => r.PatientId).Distinct().ToList();
        var patients = await context.Patients
            .AsNoTracking()
            .Where(p => patientIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.FullName);

        var total = reviews.Count;
        var avg = total > 0 ? Math.Round(reviews.Average(r => r.Rating), 1) : 5.0;

        return new DoctorReviewSummaryDto
        {
            DoctorId = doctorId,
            DoctorName = doctor.FullName,
            Specialty = doctor.Specialty,
            AverageRating = avg,
            TotalReviews = total,
            Reviews = reviews.Select(r => new ReviewResponseDto
            {
                Id = r.Id,
                DoctorId = r.DoctorId,
                DoctorName = doctor.FullName,
                DoctorSpecialty = doctor.Specialty,
                PatientId = r.PatientId,
                PatientName = patients.TryGetValue(r.PatientId, out var pName) ? pName : "Bệnh nhân",
                AppointmentId = r.AppointmentId,
                Rating = r.Rating,
                Comment = r.Comment,
                IsHidden = r.IsHidden,
                CreatedAt = r.CreatedAt
            }).ToList()
        };
    }

    public async Task<List<ReviewResponseDto>> GetAllReviewsAsync(int? doctorId = null, bool includeHidden = true)
    {
        var query = context.Reviews.AsNoTracking().AsQueryable();

        if (doctorId.HasValue)
            query = query.Where(r => r.DoctorId == doctorId.Value);

        if (!includeHidden)
            query = query.Where(r => !r.IsHidden);

        var reviews = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();

        var doctorIds = reviews.Select(r => r.DoctorId).Distinct().ToList();
        var doctors = await context.Doctors
            .AsNoTracking()
            .Where(d => doctorIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => (d.FullName, d.Specialty));

        var patientIds = reviews.Select(r => r.PatientId).Distinct().ToList();
        var patients = await context.Patients
            .AsNoTracking()
            .Where(p => patientIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.FullName);

        return reviews.Select(r => new ReviewResponseDto
        {
            Id = r.Id,
            DoctorId = r.DoctorId,
            DoctorName = doctors.TryGetValue(r.DoctorId, out var d) ? d.FullName : $"Bác sĩ #{r.DoctorId}",
            DoctorSpecialty = doctors.TryGetValue(r.DoctorId, out var dInfo) ? dInfo.Specialty : string.Empty,
            PatientId = r.PatientId,
            PatientName = patients.TryGetValue(r.PatientId, out var pName) ? pName : $"Bệnh nhân #{r.PatientId}",
            AppointmentId = r.AppointmentId,
            Rating = r.Rating,
            Comment = r.Comment,
            IsHidden = r.IsHidden,
            CreatedAt = r.CreatedAt
        }).ToList();
    }

    public async Task<ReviewResponseDto> ToggleHideReviewAsync(int reviewId, bool isHidden)
    {
        var review = await context.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId)
            ?? throw new KeyNotFoundException($"Không tìm thấy đánh giá #{reviewId}.");

        review.IsHidden = isHidden;
        await context.SaveChangesAsync();

        var doctor = doctorService.GetById(review.DoctorId);
        var patient = patientManager.FindById(review.PatientId);

        return new ReviewResponseDto
        {
            Id = review.Id,
            DoctorId = review.DoctorId,
            DoctorName = doctor.FullName,
            DoctorSpecialty = doctor.Specialty,
            PatientId = review.PatientId,
            PatientName = patient.FullName,
            AppointmentId = review.AppointmentId,
            Rating = review.Rating,
            Comment = review.Comment,
            IsHidden = review.IsHidden,
            CreatedAt = review.CreatedAt
        };
    }

    public async Task DeleteReviewAsync(int reviewId)
    {
        var review = await context.Reviews.FirstOrDefaultAsync(r => r.Id == reviewId)
            ?? throw new KeyNotFoundException($"Không tìm thấy đánh giá #{reviewId}.");

        context.Reviews.Remove(review);
        await context.SaveChangesAsync();
    }
}

