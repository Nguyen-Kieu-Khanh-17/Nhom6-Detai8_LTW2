using StudentManagement.Data;
using StudentManagement.Models;

namespace StudentManagement.Services;

public class MedicalRecordService : IMedicalRecordService
{
    private readonly AppDbContext context;
    private readonly IPatientManager patientManager;
    private readonly IDoctorService doctorService;

    public MedicalRecordService(AppDbContext context, IPatientManager patientManager, IDoctorService doctorService)
    {
        this.context = context;
        this.patientManager = patientManager;
        this.doctorService = doctorService;
    }

    public List<MedicalRecord> GetAll(int? patientId = null)
    {
        var query = context.MedicalRecords.AsQueryable();
        if (patientId.HasValue) query = query.Where(item => item.PatientId == patientId.Value);
        return query.OrderByDescending(item => item.ExaminedAt).ToList();
    }

    public List<MedicalRecord> GetForDoctor(int doctorId) =>
        context.MedicalRecords
            .Where(record => record.DoctorId == doctorId)
            .OrderByDescending(record => record.ExaminedAt)
            .ToList();

    public MedicalRecord GetById(int id)
    {
        return context.MedicalRecords.FirstOrDefault(item => item.Id == id)
            ?? throw new KeyNotFoundException("Không tìm thấy hồ sơ bệnh án.");
    }

    public MedicalRecord Add(MedicalRecord record)
    {
        patientManager.FindById(record.PatientId);
        doctorService.GetById(record.DoctorId);
        record.ExaminedAt = DateTime.Now;
        context.MedicalRecords.Add(record);
        context.SaveChanges();
        return record;
    }

    public List<Prescription> GetPrescriptions(int recordId)
    {
        GetById(recordId);
        return context.Prescriptions.Where(item => item.MedicalRecordId == recordId).ToList();
    }

    public Prescription AddPrescription(Prescription prescription)
    {
        GetById(prescription.MedicalRecordId);
        prescription.CreatedAt = DateTime.Now;
        context.Prescriptions.Add(prescription);
        context.SaveChanges();
        return prescription;
    }
}