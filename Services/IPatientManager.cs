using StudentManagement.Models;

namespace StudentManagement.Services;

public interface IPatientManager
{
    List<Patient> GetAll();
    List<Patient> GetForDoctor(int doctorId);
    Patient FindById(int id);
    Patient FindByUserId(int userId);
    void Add(Patient patient);
    void Update(Patient patient);
    void Remove(int id);
}