using StudentManagement.DTOs;

namespace StudentManagement.Services;

public interface IChatbotService
{
    Task<ChatbotMessageResponse> ProcessMessageAsync(string message, int? userId = null);
    List<ChatbotQuickAction> GetSuggestedPrompts();
    Task<ChatbotConfigDto> GetConfigAsync();
    Task<ChatbotConfigDto> UpdateConfigAsync(UpdateChatbotConfigDto updateDto);
    Task<bool> TestGeminiConnectionAsync();
}
