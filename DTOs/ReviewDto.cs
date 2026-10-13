using System.ComponentModel.DataAnnotations;

namespace StudentManagement.DTOs;

public class CreateReviewDto
{
    [Range(1, int.MaxValue, ErrorMessage = "DoctorId không hợp lệ.")]
    public int DoctorId { get; set; }

    public int? AppointmentId { get; set; }

    [Range(1, 5, ErrorMessage = "Đánh giá sao phải từ 1 đến 5.")]
    public int Rating { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập nhận xét.")]
    [StringLength(1000, ErrorMessage = "Nhận xét tối đa 1000 ký tự.")]
    public string Comment { get; set; } = string.Empty;
}

public class ReviewResponseDto
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string DoctorSpecialty { get; set; } = string.Empty;
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public int? AppointmentId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public bool IsHidden { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DoctorReviewSummaryDto
{
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public List<ReviewResponseDto> Reviews { get; set; } = [];
}

public class ToggleReviewHideDto
{
    public bool IsHidden { get; set; }
}

