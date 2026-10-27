namespace StudentManagement.DTOs;

public class ChatbotMessageRequest
{
    public string Message { get; set; } = string.Empty;
    public string? SessionId { get; set; }
}

public class ChatbotQuickAction
{
    public string Label { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty; // "book", "specialty", "info", "support"
    public string? Value { get; set; }
}

public class ChatbotDoctorInfo
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}

public class ChatbotMessageResponse
{
    public string Reply { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
    public string Source { get; set; } = "Medika AI"; // "Google Gemini AI" hoặc "Medika AI"
    public bool ShouldRefreshUi { get; set; }
    public int? CreatedAppointmentId { get; set; }
    public List<ChatbotDoctorInfo> Doctors { get; set; } = [];
    public List<ChatbotQuickAction> QuickActions { get; set; } = [];
}

public class ChatbotConfigDto
{
    public string ApiKeyMasked { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-2.5-flash";
    public string Provider { get; set; } = "Gemini";
    public string AccountId { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool HasKey { get; set; }
    public List<string> AvailableModels { get; set; } = [];
}

public class UpdateChatbotConfigDto
{
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
    public string? Provider { get; set; }
    public string? AccountId { get; set; }
    public bool? IsEnabled { get; set; }
}

public class TestConnectionRequestDto
{
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
    public string? Provider { get; set; }
    public string? AccountId { get; set; }
}

public class TestConnectionResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? StatusCode { get; set; }
    public string? ErrorDetails { get; set; }
    public List<string> AvailableModels { get; set; } = [];
}
