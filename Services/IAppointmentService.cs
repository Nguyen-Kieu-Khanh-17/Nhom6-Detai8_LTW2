using StudentManagement.DTOs;
using StudentManagement.Models;

namespace StudentManagement.Services;

public interface IAppointmentService
{
    List<Appointment> GetAll(DateTime? date = null, AppointmentStatus? status = null);
    List<Appointment> GetForUser(int userId);
    Appointment GetById(int id);
    Appointment Add(Appointment appointment);
    void Update(int id, Appointment appointment);
    void Remove(int id);
    void Cancel(int id, int userId);
    Appointment Confirm(int id, int? receptionistUserId = null);
    Appointment CheckIn(int id, int? receptionistUserId = null);
    Appointment Reschedule(int id, DateTime newDate, int? newDoctorId = null, string? reason = null, int? userId = null, string? role = null);
    MedicalRecord CompleteExam(int id, CompleteExamDto dto);
}
