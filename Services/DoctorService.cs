using StudentManagement.Data;
using StudentManagement.Models;

namespace StudentManagement.Services;

public class DoctorService : IDoctorService
{
    private readonly AppDbContext context;

    public DoctorService(AppDbContext context)
    {
        this.context = context;
        SeedDoctors();
    }

    private void SeedDoctors()
    {
        if (context.Doctors.Any()) return;

        var sampleDoctors = new List<Doctor>
        {
            new() { FullName = "BS. CKII Nguyễn Văn Hùng", Specialty = "Tim mạch", Room = "P.101", Phone = "0912345601" },
            new() { FullName = "ThS. BS Trần Thị Mai", Specialty = "Sản phụ khoa", Room = "P.201", Phone = "0912345602" },
            new() { FullName = "BS. CKI Lê Hoàng Nam", Specialty = "Nhi khoa", Room = "P.105", Phone = "0912345603" },
            new() { FullName = "BS. CKII Phạm Quốc Bảo", Specialty = "Tiêu hóa - Gan mật", Room = "P.102", Phone = "0912345604" },
            new() { FullName = "ThS. BS Đặng Thu Thảo", Specialty = "Da liễu", Room = "P.204", Phone = "0912345605" },
            new() { FullName = "BS. CKI Vũ Đình Trọng", Specialty = "Cơ xương khớp", Room = "P.108", Phone = "0912345606" },
            new() { FullName = "BS. CKII Bùi Quang Minh", Specialty = "Tai Mũi Họng", Room = "P.202", Phone = "0912345607" },
            new() { FullName = "ThS. BS Ngô Minh Tâm", Specialty = "Mắt", Room = "P.203", Phone = "0912345608" },
            new() { FullName = "BS. CKI Hoàng Đức Long", Specialty = "Nội tổng quát", Room = "P.103", Phone = "0912345609" }
        };

        context.Doctors.AddRange(sampleDoctors);
        context.SaveChanges();

        // Seed lịch làm việc từ Thứ 2 đến Thứ 7 cho từng bác sĩ
        var schedules = new List<DoctorSchedule>();
        foreach (var doc in sampleDoctors)
        {
            for (var day = 1; day <= 6; day++) // 1: Thứ 2 -> 6: Thứ 7
            {
                schedules.Add(new DoctorSchedule
                {
                    DoctorId = doc.Id,
                    DayOfWeek = (DayOfWeek)day,
                    StartTime = new TimeOnly(8, 0),
                    EndTime = new TimeOnly(17, 0),
                    IsDayOff = false
                });
            }
        }
        context.DoctorSchedules.AddRange(schedules);
        context.SaveChanges();
    }


    public List<Doctor> GetAll(string? specialty = null)
    {
        var query = context.Doctors.AsQueryable();
        if (!string.IsNullOrWhiteSpace(specialty))
            query = query.Where(doctor => doctor.Specialty.ToLower().Contains(specialty.Trim().ToLower()));

        return query.OrderBy(doctor => doctor.FullName).ToList();
    }

    public Doctor GetById(int id)
    {
        return context.Doctors.FirstOrDefault(doctor => doctor.Id == id)
            ?? throw new KeyNotFoundException($"Không tìm thấy bác sĩ có mã {id}.");
    }

    public Doctor Add(Doctor doctor)
    {
        if (doctor.Id > 0 && context.Doctors.Any(item => item.Id == doctor.Id))
            throw new InvalidOperationException($"Mã bác sĩ {doctor.Id} đã tồn tại.");

        context.Doctors.Add(doctor);
        context.SaveChanges();
        return doctor;
    }

    public void Update(int id, Doctor doctor)
    {
        var existingDoctor = GetById(id);
        existingDoctor.FullName = doctor.FullName;
        existingDoctor.Specialty = doctor.Specialty;
        existingDoctor.Phone = doctor.Phone;
        existingDoctor.Room = doctor.Room;
        existingDoctor.Degree = doctor.Degree;
        existingDoctor.ExperienceYears = doctor.ExperienceYears;
        existingDoctor.IsActive = doctor.IsActive;
        context.SaveChanges();
    }

    public void Remove(int id)
    {
        var doctor = GetById(id);
        context.Doctors.Remove(doctor);
        context.SaveChanges();
    }
}
