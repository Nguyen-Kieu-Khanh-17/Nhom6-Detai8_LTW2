using StudentManagement.Models;

namespace StudentManagement.Services;

public interface IScheduleService
{
    List<DoctorSchedule> Get(int doctorId);
    DoctorSchedule Add(int doctorId, DoctorSchedule schedule);
    void Remove(int id);
}