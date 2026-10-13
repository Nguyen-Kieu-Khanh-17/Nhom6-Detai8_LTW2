using System.ComponentModel.DataAnnotations;

namespace StudentManagement.DTOs;

public class DoctorScheduleDto
{
    [Range(0, 6)] public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public bool IsDayOff { get; set; }
}
