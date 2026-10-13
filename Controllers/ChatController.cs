using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentManagement.DTOs;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService chatService;

    public ChatController(IChatService chatService)
    {
        this.chatService = chatService;
    }

    /// <summary>
    /// Bệnh nhân lấy hoặc mở phòng chat tư vấn với CSKH.
    /// </summary>
    [HttpGet("my-room")]
    public async Task<ActionResult<ChatRoomDto>> GetMyRoom()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var room = await chatService.GetOrCreateRoomAsync(userId);
        return Ok(room);
    }

    /// <summary>
    /// Nhân viên CSKH hoặc Admin xem tất cả các phòng chat đang chờ hoặc đang hoạt động.
    /// </summary>
    [HttpGet("rooms")]
    [Authorize(Roles = "Support,Admin")]
    public async Task<ActionResult<List<ChatRoomDto>>> GetAllRooms()
    {
        var rooms = await chatService.GetAllRoomsAsync();
        return Ok(rooms);
    }

    /// <summary>
    /// Xem lịch sử tin nhắn trong phòng chat.
    /// </summary>
    [HttpGet("rooms/{roomId:int}/messages")]
    public async Task<ActionResult<List<ChatMessageDto>>> GetMessages(int roomId)
    {
        var messages = await chatService.GetMessagesAsync(roomId);
        return Ok(messages);
    }

    /// <summary>
    /// Gửi tin nhắn trong phòng chat.
    /// </summary>
    [HttpPost("messages")]
    public async Task<ActionResult<ChatMessageDto>> SendMessage([FromBody] SendChatMessageDto dto)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isSupport = User.IsInRole("Support") || User.IsInRole("Admin");

        try
        {
            var result = await chatService.SendMessageAsync(dto.RoomId, userId, dto.Content, isSupport);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Đóng phòng chat khi đã giải quyết xong.
    /// </summary>
    [HttpPost("rooms/{roomId:int}/close")]
    [Authorize(Roles = "Support,Admin")]
    public async Task<IActionResult> CloseRoom(int roomId)
    {
        try
        {
            await chatService.CloseRoomAsync(roomId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
