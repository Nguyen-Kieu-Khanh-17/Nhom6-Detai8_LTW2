using StudentManagement.Data;
using StudentManagement.Models;

namespace StudentManagement.Services;

public class PatientManager : IPatientManager
{
    private readonly AppDbContext context;

    public PatientManager(AppDbContext context)
    {
        this.context = context;
    }

    public List<Patient> GetAll()
    {
        return context.Patients.OrderBy(patient => patient.Id).ToList();
    }

    public List<Patient> GetForDoctor(int doctorId)
    {
        return context.Patients
            .Where(patient => context.Appointments.Any(appointment =>
                appointment.PatientId == patient.Id && appointment.DoctorId == doctorId))
            .OrderBy(patient => patient.Id)
            .ToList();
    }

    public Patient FindById(int id)
    {
        return context.Patients.FirstOrDefault(patient => patient.Id == id)
            ?? throw new KeyNotFoundException($"Không tìm thấy bệnh nhân có mã {id}.");
    }

    public Patient FindByUserId(int userId)
    {
        return context.Patients.FirstOrDefault(patient => patient.UserId == userId)
            ?? throw new KeyNotFoundException("Tài khoản chưa có hồ sơ bệnh nhân.");
    }

    public void Add(Patient patient)
    {
        if (patient.Id > 0 && context.Patients.Any(item => item.Id == patient.Id))
            throw new InvalidOperationException($"Mã bệnh nhân {patient.Id} đã tồn tại.");

        context.Patients.Add(patient);
        context.SaveChanges();
    }

    public void Update(Patient patient)
    {
        var existingPatient = FindById(patient.Id);
        existingPatient.FullName = patient.FullName;
        existingPatient.DateOfBirth = patient.DateOfBirth;
        existingPatient.Phone = patient.Phone;
        existingPatient.Address = patient.Address;
        existingPatient.MedicalHistory = patient.MedicalHistory;
        existingPatient.InsuranceNumber = patient.InsuranceNumber;
        context.SaveChanges();
    }

    public void Remove(int id)
    {
        var patient = FindById(id);
        context.Patients.Remove(patient);
        context.SaveChanges();
    }
}