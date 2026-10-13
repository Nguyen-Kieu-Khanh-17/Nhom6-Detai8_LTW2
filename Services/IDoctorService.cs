using StudentManagement.Models;

namespace StudentManagement.Services;

public interface IDoctorService
{
    List<Doctor> GetAll(string? specialty = null);
    Doctor GetById(int id);
    Doctor Add(Doctor doctor);
    void Update(int id, Doctor doctor);
    void Remove(int id);
}
