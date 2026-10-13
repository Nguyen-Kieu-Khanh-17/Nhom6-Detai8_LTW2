using StudentManagement.DTOs;
using StudentManagement.Models;

namespace StudentManagement.Services;

public interface IReviewService
{
    Task<ReviewResponseDto> CreateReviewAsync(int userId, CreateReviewDto dto);
    Task<DoctorReviewSummaryDto> GetDoctorReviewsAsync(int doctorId, bool includeHidden = false);
    Task<List<ReviewResponseDto>> GetAllReviewsAsync(int? doctorId = null, bool includeHidden = true);
    Task<ReviewResponseDto> ToggleHideReviewAsync(int reviewId, bool isHidden);
    Task DeleteReviewAsync(int reviewId);
}

