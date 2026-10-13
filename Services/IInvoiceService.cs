using StudentManagement.Models;

namespace StudentManagement.Services;

public interface IInvoiceService
{
    Task<Invoice> CreateInvoiceAsync(int appointmentId, decimal amount, string paymentMethod, string notes);
    Task<List<InvoiceDto>> GetInvoicesAsync();
    Task<InvoiceDto?> GetInvoiceByAppointmentIdAsync(int appointmentId);
    Task<bool> ConfirmPaymentAsync(int invoiceId, string paymentMethod);
}

public class InvoiceDto
{
    public int Id { get; set; }
    public int AppointmentId { get; set; }
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string Status { get; set; } = "Paid";
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

