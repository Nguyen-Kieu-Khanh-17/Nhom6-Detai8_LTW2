using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.DTOs;
using StudentManagement.Hubs;
using StudentManagement.Models;

namespace StudentManagement.Services;

public class ChatService : IChatService
{
    private readonly AppDbContext context;
    private readonly IHubContext<ClinicHub> hubContext;

    public ChatService(AppDbContext context, IHubContext<ClinicHub> hubContext)
    {
        this.context = context;
        this.hubContext = hubContext;
    }

    public async Task<ChatRoomDto> GetOrCreateRoomAsync(int patientUserId)
    {
        var existingRoom = await context.ChatRooms
            .FirstOrDefaultAsync(r => r.PatientUserId == patientUserId && r.Status != "Closed");

        if (existingRoom == null)
        {
            var roomCode = $"ROOM_{patientUserId}_{DateTime.Now:yyyyMMddHHmmss}";
            existingRoom = new ChatRoom
            {
                PatientUserId = patientUserId,
                RoomCode = roomCode,
                Status = "Active",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            context.ChatRooms.Add(existingRoom);
            await context.SaveChangesAsync();

            // Thông báo cho Support có phòng chat mới
            await hubContext.Clients.Group("Support").SendAsync("NewChatRoomCreated", new
            {
                roomId = existingRoom.Id,
                patientUserId,
                roomCode = existingRoom.RoomCode
            });
        }

        var patientUser = await context.Users.FindAsync(patientUserId);
        var supportUser = existingRoom.SupportUserId.HasValue ? await context.Users.FindAsync(existingRoom.SupportUserId.Value) : null;

        var lastMsg = await context.ChatMessages
            .Where(m => m.RoomId == existingRoom.Id)
            .OrderByDescending(m => m.SentAt)
            .Select(m => m.Content)
            .FirstOrDefaultAsync();

        return new ChatRoomDto
        {
            Id = existingRoom.Id,
            PatientUserId = existingRoom.PatientUserId,
            PatientName = patientUser?.FullName ?? $"Bệnh nhân #{patientUserId}",
            SupportUserId = existingRoom.SupportUserId,
            SupportName = supportUser?.FullName,
            RoomCode = existingRoom.RoomCode,
            Status = existingRoom.Status,
            CreatedAt = existingRoom.CreatedAt,
            UpdatedAt = existingRoom.UpdatedAt,
            LastMessage = lastMsg
        };
    }

    public async Task<List<ChatRoomDto>> GetAllRoomsAsync()
    {
        var rooms = await context.ChatRooms
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .ToListAsync();

        var users = await context.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.FullName);

        var result = new List<ChatRoomDto>();
        foreach (var r in rooms)
        {
            var lastMsg = await context.ChatMessages
                .Where(m => m.RoomId == r.Id)
                .OrderByDescending(m => m.SentAt)
                .Select(m => m.Content)
                .FirstOrDefaultAsync();

            result.Add(new ChatRoomDto
            {
                Id = r.Id,
                PatientUserId = r.PatientUserId,
                PatientName = users.GetValueOrDefault(r.PatientUserId, $"Bệnh nhân #{r.PatientUserId}"),
                SupportUserId = r.SupportUserId,
                SupportName = r.SupportUserId.HasValue ? users.GetValueOrDefault(r.SupportUserId.Value, "CSKH") : null,
                RoomCode = r.RoomCode,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                UpdatedAt = r.UpdatedAt,
                LastMessage = lastMsg
            });
        }

        return result;
    }

    public async Task<List<ChatMessageDto>> GetMessagesAsync(int roomId)
    {
        var messages = await context.ChatMessages
            .Where(m => m.RoomId == roomId)
            .OrderBy(m => m.SentAt)
            .ToListAsync();

        var senderIds = messages.Select(m => m.SenderId).Distinct().ToList();
        var userNames = await context.Users
            .Where(u => senderIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        return messages.Select(m => new ChatMessageDto
        {
            Id = m.Id,
            RoomId = m.RoomId ?? roomId,
            SenderId = m.SenderId,
            SenderName = userNames.GetValueOrDefault(m.SenderId, m.IsFromSupport ? "Nhân viên hỗ trợ" : "Bệnh nhân"),
            Content = m.Content,
            IsFromSupport = m.IsFromSupport,
            SentAt = m.SentAt
        }).ToList();
    }

    public async Task<ChatMessageDto> SendMessageAsync(int roomId, int senderId, string content, bool isFromSupport)
    {
        var room = await context.ChatRooms.FindAsync(roomId)
            ?? throw new KeyNotFoundException("Không tìm thấy phòng chat.");

        var sender = await context.Users.FindAsync(senderId);

        if (isFromSupport && !room.SupportUserId.HasValue)
        {
            room.SupportUserId = senderId;
        }

        room.UpdatedAt = DateTime.Now;

        var message = new ChatMessage
        {
            RoomId = roomId,
            SenderId = senderId,
            ReceiverId = isFromSupport ? room.PatientUserId : (room.SupportUserId ?? 0),
            RoomPatientId = room.PatientUserId,
            Content = content.Trim(),
            IsFromSupport = isFromSupport,
            SentAt = DateTime.Now
        };

        context.ChatMessages.Add(message);
        await context.SaveChangesAsync();

        var messageDto = new ChatMessageDto
        {
            Id = message.Id,
            RoomId = roomId,
            SenderId = senderId,
            SenderName = sender?.FullName ?? (isFromSupport ? "Nhân viên hỗ trợ" : "Bệnh nhân"),
            Content = message.Content,
            IsFromSupport = isFromSupport,
            SentAt = message.SentAt
        };

        // Broadcast realtime qua SignalR tới nhóm phòng chat và người nhận
        await hubContext.Clients.Group($"chat_room_{roomId}").SendAsync("ReceiveChatMessage", messageDto);
        await hubContext.Clients.Group($"user_{message.ReceiverId}").SendAsync("ReceiveChatMessage", messageDto);

        return messageDto;
    }

    public async Task CloseRoomAsync(int roomId)
    {
        var room = await context.ChatRooms.FindAsync(roomId)
            ?? throw new KeyNotFoundException("Không tìm thấy phòng chat.");

        room.Status = "Closed";
        room.UpdatedAt = DateTime.Now;
        await context.SaveChangesAsync();

        await hubContext.Clients.Group($"chat_room_{roomId}").SendAsync("ChatRoomClosed", new { roomId });
    }
}
