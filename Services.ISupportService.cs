using StudentManagement.Models;

namespace StudentManagement.Services;

public interface ISupportService
{
    List<SupportMessage> GetAll(int? userId = null);
    SupportMessage GetById(int id);
    SupportMessage Add(int userId, SupportMessage message);
    void Reply(int id, string reply, SupportMessageStatus status);
}