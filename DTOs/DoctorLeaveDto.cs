using StudentManagement.Models;

namespace StudentManagement.DTOs;

public class CreateDoctorLeaveDto
{
    public int DoctorId { get; set; }
    public DateTime LeaveDate { get; set; }
    public string Shift { get; set; } = "AllDay"; // Morning, Afternoon, Evening, AllDay
    public string Reason { get; set; } = string.Empty;
}

public class DoctorLeaveResponseDto
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public DateTime LeaveDate { get; set; }
    public string Shift { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public LeaveRequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public int AffectedAppointmentsCount { get; set; }
}

/// <summary>Body tuỳ chọn khi Admin từ chối đơn xin nghỉ — có thể kèm ghi chú lý do từ chối.</summary>
public class RejectLeaveDto
{
    public string? Note { get; set; }
}
