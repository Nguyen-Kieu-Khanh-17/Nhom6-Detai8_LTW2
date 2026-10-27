using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentManagement.Models;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/invoices")]
[Authorize]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService invoiceService;

    public InvoicesController(IInvoiceService invoiceService)
    {
        this.invoiceService = invoiceService;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Receptionist")]
    public async Task<ActionResult<List<InvoiceDto>>> GetInvoices()
    {
        var list = await invoiceService.GetInvoicesAsync();
        return Ok(list);
    }

    [HttpGet("appointment/{appointmentId}")]
    public async Task<ActionResult<InvoiceDto>> GetByAppointmentId(int appointmentId)
    {
        var inv = await invoiceService.GetInvoiceByAppointmentIdAsync(appointmentId);
        if (inv == null) return NotFound(new { message = "Chưa có hóa đơn cho ca khám này." });
        return Ok(inv);
    }

    public class CreateInvoiceRequest
    {
        public int AppointmentId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        public string Notes { get; set; } = string.Empty;
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Receptionist")]
    public async Task<ActionResult<Invoice>> CreateInvoice([FromBody] CreateInvoiceRequest req)
    {
        try
        {
            var invoice = await invoiceService.CreateInvoiceAsync(req.AppointmentId, req.Amount, req.PaymentMethod, req.Notes);
            return Ok(invoice);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    public class ConfirmPaymentRequest
    {
        public string PaymentMethod { get; set; } = "Cash";
    }

    [HttpPatch("{id}/confirm")]
    [Authorize(Roles = "Admin,Receptionist")]
    public async Task<ActionResult> ConfirmPayment(int id, [FromBody] ConfirmPaymentRequest? req = null)
    {
        var method = req?.PaymentMethod ?? "Cash";
        var success = await invoiceService.ConfirmPaymentAsync(id, method);
        if (!success) return NotFound(new { message = "Không tìm thấy hóa đơn." });
        return Ok(new { message = "Xác nhận thanh toán thành công." });
    }
}

