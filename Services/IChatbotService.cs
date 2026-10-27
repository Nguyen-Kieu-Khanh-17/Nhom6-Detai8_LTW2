using StudentManagement.DTOs;

namespace StudentManagement.Services;

public interface IChatbotService
{
    Task<ChatbotMessageResponse> ProcessMessageAsync(string message, int? userId = null, string? sessionId = null);
    List<ChatbotQuickAction> GetSuggestedPrompts();
    Task<ChatbotConfigDto> GetConfigAsync();
    Task<ChatbotConfigDto> UpdateConfigAsync(UpdateChatbotConfigDto updateDto);
    Task<TestConnectionResponseDto> TestConnectionAsync(string? provider, string? testApiKey, string? testModel, string? accountId);
    Task<List<string>> GetAvailableModelsAsync(string? provider = null, string? accountId = null);
}
