using StudentManagement.Data;
using StudentManagement.Models;

namespace StudentManagement.Services;

public class SupportService : ISupportService
{
    private readonly AppDbContext context;

    public SupportService(AppDbContext context)
    {
        this.context = context;
    }

    public List<SupportMessage> GetAll(int? userId = null)
    {
        var query = context.SupportMessages.AsQueryable();
        if (userId.HasValue) query = query.Where(item => item.UserId == userId.Value);
        return query.OrderByDescending(item => item.CreatedAt).ToList();
    }

    public SupportMessage GetById(int id)
    {
        return context.SupportMessages.FirstOrDefault(item => item.Id == id)
            ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu hỗ trợ.");
    }

    public SupportMessage Add(int userId, SupportMessage message)
    {
        message.UserId = userId;
        context.SupportMessages.Add(message);
        context.SaveChanges();
        return message;
    }

    public void Reply(int id, string reply, SupportMessageStatus status)
    {
        var message = context.SupportMessages.FirstOrDefault(item => item.Id == id)
            ?? throw new KeyNotFoundException("Không tìm thấy yêu cầu hỗ trợ.");

        message.Reply = reply.Trim();
        message.Status = status;
        context.SaveChanges();
    }
}