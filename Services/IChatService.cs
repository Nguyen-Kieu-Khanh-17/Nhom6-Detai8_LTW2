using StudentManagement.DTOs;

namespace StudentManagement.Services;

public interface IChatService
{
    Task<ChatRoomDto> GetOrCreateRoomAsync(int patientUserId);
    Task<List<ChatRoomDto>> GetAllRoomsAsync();
    Task<List<ChatMessageDto>> GetMessagesAsync(int roomId);
    Task<ChatMessageDto> SendMessageAsync(int roomId, int senderId, string content, bool isFromSupport);
    Task CloseRoomAsync(int roomId);
}
