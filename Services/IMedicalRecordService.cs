using StudentManagement.Models;

namespace StudentManagement.Services;

public interface IMedicalRecordService
{
    List<MedicalRecord> GetAll(int? patientId = null);
    MedicalRecord GetById(int id);
    MedicalRecord Add(MedicalRecord record);
    List<Prescription> GetPrescriptions(int recordId);
    Prescription AddPrescription(Prescription prescription);
}