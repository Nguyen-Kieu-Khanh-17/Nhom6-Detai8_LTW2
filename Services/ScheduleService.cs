using StudentManagement.Data;
using StudentManagement.Models;

namespace StudentManagement.Services;

public class ScheduleService : IScheduleService
{
    private readonly AppDbContext context;
    private readonly IDoctorService doctorService;

    public ScheduleService(AppDbContext context, IDoctorService doctorService)
    {
        this.context = context;
        this.doctorService = doctorService;
    }

    public List<DoctorSchedule> Get(int doctorId)
    {
        return context.DoctorSchedules.Where(item => item.DoctorId == doctorId).OrderBy(item => item.DayOfWeek).ToList();
    }

    public DoctorSchedule Add(int doctorId, DoctorSchedule schedule)
    {
        doctorService.GetById(doctorId);
        schedule.DoctorId = doctorId;
        context.DoctorSchedules.Add(schedule);
        context.SaveChanges();
        return schedule;
    }

    public void Remove(int id)
    {
        var schedule = context.DoctorSchedules.FirstOrDefault(item => item.Id == id)
            ?? throw new KeyNotFoundException("Không tìm thấy lịch làm việc.");

        context.DoctorSchedules.Remove(schedule);
        context.SaveChanges();
    }
}