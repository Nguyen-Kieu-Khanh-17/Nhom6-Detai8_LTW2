using StudentManagement.DTOs;
using StudentManagement.Models;

namespace StudentManagement.Services;

public interface IDoctorLeaveService
{
    Task<DoctorLeaveRequest> CreateRequestAsync(int doctorId, DateTime leaveDate, string shift, string reason);
    Task<List<DoctorLeaveResponseDto>> GetAllAsync(LeaveRequestStatus? status = null);
    Task<List<DoctorLeaveResponseDto>> GetByDoctorIdAsync(int doctorId);
    Task<DoctorLeaveResponseDto> ApproveAsync(int requestId, int adminUserId);
    Task<DoctorLeaveResponseDto> RejectAsync(int requestId, int adminUserId);
}
