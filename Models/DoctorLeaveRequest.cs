namespace StudentManagement.Models;

public enum LeaveRequestStatus
{
    Pending,
    Approved,
    Rejected
}

public class DoctorLeaveRequest
{
    public int Id { get; set; }
    public int DoctorId { get; set; }
    public DateTime LeaveDate { get; set; }
    public string Shift { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
