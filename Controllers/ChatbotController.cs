using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StudentManagement.DTOs;
using StudentManagement.Services;

namespace StudentManagement.Controllers;

[ApiController]
[Route("api/chatbot")]
public class ChatbotController : ControllerBase
{
    private readonly IChatbotService chatbotService;

    public ChatbotController(IChatbotService chatbotService)
    {
        this.chatbotService = chatbotService;
    }

    /// <summary>
    /// Gửi tin nhắn cho Trợ lý ảo AI Medika Assistant (tích hợp Google Gemini).
    /// Hoạt động cho cả khách xem trang và người dùng đã đăng nhập.
    /// </summary>
    [HttpPost("message")]
    [AllowAnonymous]
    public async Task<ActionResult<ChatbotMessageResponse>> SendMessage([FromBody] ChatbotMessageRequest request)
    {
        int? userId = null;
        if (User?.Identity?.IsAuthenticated == true && int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid))
        {
            userId = uid;
        }

        var response = await chatbotService.ProcessMessageAsync(request.Message, userId, request.SessionId);
        return Ok(response);
    }

    /// <summary>
    /// Lấy danh sách câu hỏi / hành động gợi ý nhanh cho chatbot.
    /// </summary>
    [HttpGet("prompts")]
    [AllowAnonymous]
    public ActionResult<List<ChatbotQuickAction>> GetSuggestedPrompts()
    {
        return Ok(chatbotService.GetSuggestedPrompts());
    }

    /// <summary>
    /// Admin xem cấu hình nhà cung cấp AI của Chatbot.
    /// </summary>
    [HttpGet("config")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ChatbotConfigDto>> GetConfig()
    {
        return Ok(await chatbotService.GetConfigAsync());
    }

    /// <summary>
    /// Admin cập nhật nhà cung cấp, API Key hoặc Model của Chatbot AI.
    /// </summary>
    [HttpPost("config")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ChatbotConfigDto>> UpdateConfig([FromBody] UpdateChatbotConfigDto updateDto)
    {
        var config = await chatbotService.UpdateConfigAsync(updateDto);
        return Ok(config);
    }

    /// <summary>
    /// Admin kiểm tra kết nối với nhà cung cấp AI đã chọn.
    /// </summary>
    [HttpPost("test-connection")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TestConnectionResponseDto>> TestConnection([FromBody] TestConnectionRequestDto? request = null)
    {
        var result = await chatbotService.TestConnectionAsync(
            request?.Provider,
            request?.ApiKey,
            request?.Model,
            request?.AccountId);
        return Ok(result);
    }

    /// <summary>
    /// Admin lấy danh sách model được nhà cung cấp hiện tại hỗ trợ.
    /// </summary>
    [HttpGet("available-models")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<List<string>>> GetAvailableModels([FromQuery] string? provider = null, [FromQuery] string? accountId = null)
    {
        var models = await chatbotService.GetAvailableModelsAsync(provider, accountId);
        return Ok(models);
    }
}
