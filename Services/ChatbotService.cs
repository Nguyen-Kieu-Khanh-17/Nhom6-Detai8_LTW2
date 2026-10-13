using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using StudentManagement.Data;
using StudentManagement.DTOs;
using StudentManagement.Models;

namespace StudentManagement.Services;

public class ChatbotService : IChatbotService
{
    private readonly AppDbContext context;
    private readonly IConfiguration configuration;
    private readonly IHttpClientFactory httpClientFactory;

    // Lưu cấu hình động do Admin cập nhật trong runtime
    private static string? dynamicApiKey;
    private static string dynamicModel = "gemini-1.5-flash";
    private static bool dynamicIsEnabled = true;

    public ChatbotService(AppDbContext context, IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        this.context = context;
        this.configuration = configuration;
        this.httpClientFactory = httpClientFactory;
    }

    private string GetActiveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(dynamicApiKey)) return dynamicApiKey.Trim();
        var configKey = configuration["Gemini:ApiKey"] ?? configuration["GeminiApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        return configKey?.Trim() ?? string.Empty;
    }

    private string GetActiveModel()
    {
        return !string.IsNullOrWhiteSpace(dynamicModel) ? dynamicModel : (configuration["Gemini:Model"] ?? "gemini-1.5-flash");
    }

    private bool IsGeminiEnabled()
    {
        return dynamicIsEnabled && (configuration.GetValue<bool?>("Gemini:IsEnabled") ?? true);
    }

    public Task<ChatbotConfigDto> GetConfigAsync()
    {
        var key = GetActiveApiKey();
        var masked = string.IsNullOrWhiteSpace(key)
            ? string.Empty
            : (key.Length > 8 ? $"{key[..4]}...{key[^4..]}" : "****");

        return Task.FromResult(new ChatbotConfigDto
        {
            ApiKeyMasked = masked,
            Model = GetActiveModel(),
            IsEnabled = IsGeminiEnabled(),
            HasKey = !string.IsNullOrWhiteSpace(key),
            Provider = !string.IsNullOrWhiteSpace(key) && IsGeminiEnabled() ? "Google Gemini AI" : "Medika Clinical Assistant"
        });
    }

    public async Task<ChatbotConfigDto> UpdateConfigAsync(UpdateChatbotConfigDto updateDto)
    {
        if (updateDto.ApiKey != null)
        {
            dynamicApiKey = updateDto.ApiKey.Trim();
        }
        if (!string.IsNullOrWhiteSpace(updateDto.Model))
        {
            dynamicModel = updateDto.Model.Trim();
        }
        if (updateDto.IsEnabled.HasValue)
        {
            dynamicIsEnabled = updateDto.IsEnabled.Value;
        }

        return await GetConfigAsync();
    }

    public async Task<bool> TestGeminiConnectionAsync()
    {
        var key = GetActiveApiKey();
        if (string.IsNullOrWhiteSpace(key)) return false;

        try
        {
            var client = httpClientFactory.CreateClient();
            var model = GetActiveModel();
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={key}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = "Xin chào, hãy trả lời đúng một từ: 'OK'" }
                        }
                    }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            var res = await client.PostAsync(url, content);
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ChatbotMessageResponse> ProcessMessageAsync(string message, int? userId = null)
    {
        var raw = (message ?? string.Empty).Trim();
        var lower = raw.ToLowerInvariant();

        var response = new ChatbotMessageResponse();

        // 1. Kiểm tra dấu hiệu cấp cứu / khẩn cấp tuyệt đối
        if (lower.Contains("cấp cứu") || lower.Contains("nguy kịch") || lower.Contains("hôn mê") || 
            lower.Contains("khó thở dữ dội") || lower.Contains("đột quỵ") || lower.Contains("co giật"))
        {
            response.Intent = "emergency";
            response.Source = "Medika Safety Protocol";
            response.Reply = "🚨 CẢNH BÁO KHẨN CẤP: Đây là các dấu hiệu cần can thiệp y tế ngay lập tức!\n\n" +
                             "Vui lòng gọi ngay Hotline Cấp cứu Medika: 1900 6868 hoặc 115, hoặc đưa người bệnh đến khoa Cấp cứu gần nhất. " +
                             "Không nên tự ý điều trị tại nhà.";
            response.QuickActions = new List<ChatbotQuickAction>
            {
                new() { Label = "📞 Gọi cấp cứu: 1900 6868", ActionType = "info", Value = "tel:19006868" }
            };
            return response;
        }

        // Lấy danh sách bác sĩ từ DB để phục vụ nhận diện và gợi ý
        var allDoctors = await context.Doctors.AsNoTracking().ToListAsync();

        // Kiểm tra xem có cấu hình Gemini API hay không
        var apiKey = GetActiveApiKey();
        var useGemini = !string.IsNullOrWhiteSpace(apiKey) && IsGeminiEnabled();

        if (useGemini)
        {
            try
            {
                var geminiReply = await CallGeminiApiAsync(raw, allDoctors, apiKey);
                if (!string.IsNullOrWhiteSpace(geminiReply))
                {
                    response.Reply = geminiReply;
                    response.Source = "Google Gemini AI";
                    response.Intent = "ai_consultation";

                    // Gợi ý bác sĩ liên quan nếu câu hỏi hoặc câu trả lời đề cập đến chuyên khoa
                    var matchedSpecialty = DetectSpecialty(lower) ?? DetectSpecialty(geminiReply.ToLowerInvariant());
                    if (!string.IsNullOrEmpty(matchedSpecialty))
                    {
                        var relevantDocs = allDoctors
                            .Where(d => d.Specialty.Contains(matchedSpecialty, StringComparison.OrdinalIgnoreCase))
                            .Select(ToDoctorInfo)
                            .ToList();

                        response.Doctors = relevantDocs;
                        response.QuickActions.Add(new ChatbotQuickAction
                        {
                            Label = $"📅 Đặt lịch khám {matchedSpecialty}",
                            ActionType = "book",
                            Value = matchedSpecialty
                        });
                    }

                    response.QuickActions.Add(new ChatbotQuickAction { Label = "💬 Nhắn tin với nhân viên hỗ trợ", ActionType = "support" });
                    return response;
                }
            }
            catch
            {
                // Nếu Gemini lỗi mạng, tự động fallback xuống bộ xử lý nội bộ
            }
        }

        // 2. Fallback xử lý thông minh nội bộ của Medika
        return await ProcessInternalFallbackAsync(raw, lower, userId, allDoctors);
    }

    private async Task<string?> CallGeminiApiAsync(string userMessage, List<Doctor> doctors, string apiKey)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);
        var model = GetActiveModel();
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        var doctorSummary = string.Join("; ", doctors.Select(d => $"{d.FullName} (Chuyên khoa: {d.Specialty}, Phòng: {d.Room})"));

        var systemPrompt = $@"Bạn là Medika AI - Trợ lý Y tế & Đặt lịch thông minh của Phòng khám Đa khoa Quốc tế Medika.
Thông tin phòng khám:
- Địa chỉ: Tòa nhà Medika Clinic, Số 88 Đường Y Dược, Quận 1, TP. Hồ Chí Minh.
- Giờ làm việc: Thứ Hai - Chủ Nhật từ 07:00 đến 20:00 (làm việc thông trưa). Cấp cứu tiếp nhận 24/7.
- Hotline: 1900 6868.
- Tiếp nhận BHYT toàn quốc (đúng tuyến 80-100%) và bảo lãnh hơn 25 công ty bảo hiểm tư nhân.
- Danh sách các bác sĩ và chuyên khoa hiện có tại phòng khám: {doctorSummary}.

Nhiệm vụ của bạn:
1. Trả lời bằng tiếng Việt ân cần, chuyên nghiệp, chuẩn mực y khoa, dùng định dạng Markdown rõ ràng, dễ đọc.
2. Khi người dùng mô tả triệu chứng, hãy giải thích nguyên nhân sơ bộ có thể gặp, tư vấn chuyên khoa phù hợp và gợi ý bác sĩ tại phòng khám để người dùng đặt lịch khám.
3. Khi người dùng hỏi về thủ tục, BHYT, giá cả, giờ khám, hãy hướng dẫn tận tình, súc tích.
4. Luôn thêm một lưu ý ngắn gọn ở cuối rằng lời khuyên chỉ mang tính tham khảo và khuyên người bệnh nên đến khám trực tiếp để được chẩn đoán chính xác.";

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = $"{systemPrompt}\n\nNgười dùng hỏi: {userMessage}" }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.5,
                maxOutputTokens = 800
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(url, httpContent);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var resString = await response.Content.ReadAsStringAsync();
        var doc = JsonNode.Parse(resString);
        var text = doc?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();
        return text;
    }

    private async Task<ChatbotMessageResponse> ProcessInternalFallbackAsync(string raw, string lower, int? userId, List<Doctor> allDoctors)
    {
        var response = new ChatbotMessageResponse
        {
            Source = "Medika Assistant"
        };

        // Chào hỏi
        if (lower is "chào" or "xin chào" or "hi" or "hello" or "alo" || lower.StartsWith("chào bạn") || lower.StartsWith("chào bác sĩ"))
        {
            response.Intent = "greeting";
            response.Reply = "Xin chào! Tôi là Trợ lý Y tế của Phòng khám Đa khoa Medika 🏥.\n\n" +
                             "Tôi có thể hỗ trợ bạn:\n" +
                             "• Tư vấn chuyên khoa và tìm kiếm bác sĩ phù hợp\n" +
                             "• Hướng dẫn đặt lịch khám và kiểm tra lịch hẹn\n" +
                             "• Giải đáp thông tin bảo hiểm y tế (BHYT) & giờ làm việc\n\n" +
                             "Bạn đang gặp vấn đề sức khỏe nào hoặc cần hỗ trợ gì hôm nay?";
            response.QuickActions = GetSuggestedPrompts();
            return response;
        }

        // 1. Tìm chuyên khoa / Triệu chứng bệnh lý (Ưu tiên cao nhất khi người dùng nói về bệnh)
        var matchedSpecialty = DetectSpecialty(lower);
        if (!string.IsNullOrEmpty(matchedSpecialty))
        {
            response.Intent = "specialty_recommendation";
            var docs = allDoctors
                .Where(d => d.Specialty.Contains(matchedSpecialty, StringComparison.OrdinalIgnoreCase))
                .Select(ToDoctorInfo)
                .ToList();

            response.Doctors = docs;
            response.Reply = $"Với nhu cầu hoặc triệu chứng bạn mô tả, chuyên khoa phù hợp nhất là: **Khoa {matchedSpecialty}**.\n\n" +
                             (docs.Any()
                                 ? $"Hiện phòng khám có {docs.Count} bác sĩ đang tiếp nhận khám khoa này:"
                                 : "Hiện bạn có thể đặt lịch khám với bác sĩ chuyên khoa này tại quầy hoặc chọn giờ khám.");

            response.QuickActions = new List<ChatbotQuickAction>
            {
                new() { Label = $"📅 Đặt lịch khám {matchedSpecialty}", ActionType = "book", Value = matchedSpecialty },
                new() { Label = "💬 Nhắn tin với nhân viên hỗ trợ", ActionType = "support" }
            };
            return response;
        }

        // 2. Giờ làm việc & Địa chỉ phòng khám
        if (lower.Contains("giờ làm việc") || lower.Contains("mấy giờ") || lower.Contains("địa chỉ") || 
            lower.Contains("thời gian khám") || lower.Contains("ở đâu") || lower.Contains("vị trí phòng khám"))
        {
            response.Intent = "clinic_info";
            response.Reply = "🕒 THỜI GIAN LÀM VIỆC PHÒNG KHÁM MEDIKA:\n" +
                             "• Thứ Hai - Chủ Nhật: 07:00 - 20:00 (làm việc thông trưa)\n" +
                             "• Tiếp nhận cấp cứu & trực khám: 24/7\n\n" +
                             "📍 ĐỊA CHỈ: Tòa nhà Medika Clinic, Số 88 Đường Y Dược, Quận 1, TP. Hồ Chí Minh.\n" +
                             "📞 Hotline tiếp nhận: 1900 6868 (Miễn phí cước gọi).";
            response.QuickActions = new List<ChatbotQuickAction>
            {
                new() { Label = "📅 Đặt lịch khám ngay", ActionType = "book" },
                new() { Label = "👨‍⚕️ Xem danh sách bác sĩ", ActionType = "specialty", Value = "" }
            };
            return response;
        }

        // 3. BHYT
        if (lower.Contains("bhyt") || lower.Contains("bảo hiểm") || lower.Contains("thẻ bảo hiểm") || lower.Contains("bảo lãnh"))
        {
            response.Intent = "insurance_info";
            response.Reply = "💳 CHÍNH SÁCH BẢO HIỂM Y TẾ (BHYT) TẠI MEDIKA:\n\n" +
                             "• Medika tiếp nhận khám chữa bệnh BHYT cho tất cả người dân trên toàn quốc, không phân biệt nơi đăng ký ban đầu.\n" +
                             "• Mức thanh toán: Đúng tuyến hưởng 80% - 100% chi phí theo danh mục BHYT quy định.\n" +
                             "• Hỗ trợ bảo lãnh viện phí trực tiếp với hơn 25 công ty bảo hiểm tư nhân (Bảo Việt, Prudential, Manulife, AIA...).\n\n" +
                             "Khi đi khám, quý khách chỉ cần mang theo CCCD gắn chip hoặc thẻ BHYT (hoặc ứng dụng VssID).";
            response.QuickActions = new List<ChatbotQuickAction>
            {
                new() { Label = "📅 Đặt lịch khám BHYT", ActionType = "book" }
            };
            return response;
        }

        // Bác sĩ
        if (lower.Contains("bác sĩ") || lower.Contains("danh sách bác sĩ") || lower.Contains("chuyên khoa nào"))
        {
            response.Intent = "doctors_list";
            response.Doctors = allDoctors.Take(5).Select(ToDoctorInfo).ToList();
            response.Reply = "Dưới đây là một số bác sĩ chuyên khoa tiêu biểu tại Medika. Bạn có thể nhấn vào để đặt lịch:";
            response.QuickActions = new List<ChatbotQuickAction>
            {
                new() { Label = "📅 Đặt lịch khám ngay", ActionType = "book" },
                new() { Label = "💬 Hỗ trợ trực tuyến", ActionType = "support" }
            };
            return response;
        }

        // Quy trình đặt lịch
        if (lower.Contains("đặt lịch") || lower.Contains("hẹn khám") || lower.Contains("đăng ký khám"))
        {
            response.Intent = "booking_guide";
            response.Reply = "📝 QUY TRÌNH ĐẶT LỊCH KHÁM TẠI MEDIKA:\n\n" +
                             "1. Chọn Chuyên khoa bạn cần khám.\n" +
                             "2. Chọn Bác sĩ mong muốn (hoặc để hệ thống chỉ định).\n" +
                             "3. Chọn Ngày & Giờ khám phù hợp.\n" +
                             "4. Nhập lý do khám và xác nhận.\n\n" +
                             "Sau khi đặt, Lễ tân sẽ xác nhận ngay. Đến ngày khám bạn chỉ cần check-in tại quầy để lấy Số thứ tự vào phòng khám!";
            response.QuickActions = new List<ChatbotQuickAction>
            {
                new() { Label = "👉 Mở form đặt lịch ngay", ActionType = "book" },
                new() { Label = "💬 Chat với nhân viên hỗ trợ", ActionType = "support" }
            };
            return response;
        }

        // Kiểm tra lịch cá nhân
        if (lower.Contains("lịch của tôi") || lower.Contains("kiểm tra lịch") || lower.Contains("đã đặt"))
        {
            response.Intent = "check_my_appointments";
            if (userId.HasValue && userId.Value > 0)
            {
                var upcoming = await context.Appointments
                    .Where(a => a.UserId == userId.Value && a.AppointmentDate >= DateTime.Today.AddDays(-1))
                    .OrderBy(a => a.AppointmentDate)
                    .Take(3)
                    .ToListAsync();

                if (upcoming.Any())
                {
                    var details = string.Join("\n", upcoming.Select(a =>
                        $"• Ngày: {a.AppointmentDate:dd/MM/yyyy HH:mm} - Lý do: {a.Reason} (Trạng thái: {a.Status})"));
                    response.Reply = $"Lịch khám gần đây của bạn:\n{details}\n\nBạn có thể quản lý hoặc đổi lịch ở mục 'Lịch hẹn của tôi'.";
                }
                else
                {
                    response.Reply = "Bạn hiện chưa có lịch khám nào sắp tới. Bạn có muốn đặt lịch khám mới không?";
                }
            }
            else
            {
                response.Reply = "Vui lòng đăng nhập tài khoản bệnh nhân để tôi có thể tra cứu lịch khám chính xác cho bạn nhé!";
            }
            response.QuickActions = new List<ChatbotQuickAction>
            {
                new() { Label = "📅 Đặt lịch khám mới", ActionType = "book" }
            };
            return response;
        }

        // Mặc định
        response.Intent = "general_fallback";
        response.Reply = "Cảm ơn bạn đã đặt câu hỏi cho Medika AI! 💡\n\n" +
                         "Tôi có thể hỗ trợ bạn:\n" +
                         "• Tư vấn triệu chứng bệnh và đề xuất chuyên khoa phù hợp (Tim mạch, Tiêu hóa, Nhi, Sản, TMH...)\n" +
                         "• Giới thiệu bác sĩ chuyên khoa & hướng dẫn đặt lịch khám trực tuyến\n" +
                         "• Giải đáp thông tin quyền lợi BHYT, chuẩn bị trước khi khám và thời gian làm việc.\n\n" +
                         "*(Lưu ý: Quản trị viên có thể kết nối Google Gemini API Key trong trang Admin để mở khóa toàn bộ khả năng tư vấn sức khỏe AI chuyên sâu).*";
        response.QuickActions = GetSuggestedPrompts();
        return response;
    }

    public List<ChatbotQuickAction> GetSuggestedPrompts()
    {
        return new List<ChatbotQuickAction>
        {
            new() { Label = "📅 Hướng dẫn đặt lịch", ActionType = "info", Value = "Hướng dẫn đặt lịch khám" },
            new() { Label = "🩺 Tư vấn chuyên khoa", ActionType = "info", Value = "Tôi muốn tìm chuyên khoa phù hợp" },
            new() { Label = "🕒 Giờ làm việc & Địa chỉ", ActionType = "info", Value = "Giờ làm việc và địa chỉ phòng khám" },
            new() { Label = "💳 Thủ tục BHYT", ActionType = "info", Value = "Quy định khám chữa bệnh bằng BHYT" },
            new() { Label = "💬 Gặp nhân viên hỗ trợ", ActionType = "support" }
        };
    }

    private static ChatbotDoctorInfo ToDoctorInfo(Doctor d) => new()
    {
        Id = d.Id,
        FullName = d.FullName,
        Specialty = d.Specialty,
        Room = d.Room,
        Phone = d.Phone
    };

    private static string? DetectSpecialty(string input)
    {
        if (input.Contains("tim") || input.Contains("huyết áp") || input.Contains("đau ngực") || input.Contains("hồi hộp") || input.Contains("mạch"))
            return "Tim mạch";

        if (input.Contains("dạ dày") || input.Contains("tiêu hóa") || input.Contains("gan") || input.Contains("mật") || 
            input.Contains("ợ chua") || input.Contains("đau bụng") || input.Contains("đầy hơi") || input.Contains("trĩ"))
            return "Tiêu hóa - Gan mật";

        if (input.Contains("trẻ") || input.Contains("nhi") || input.Contains("em bé") || input.Contains("bé") || input.Contains("tiêm chủng"))
            return "Nhi khoa";

        if (input.Contains("phụ khoa") || input.Contains("thai") || input.Contains("sản") || input.Contains("kinh nguyệt") || input.Contains("sinh"))
            return "Sản phụ khoa";

        if (input.Contains("da") || input.Contains("mụn") || input.Contains("ngứa") || input.Contains("dị ứng") || input.Contains("mề đay") || input.Contains("nấm"))
            return "Da liễu";

        if (input.Contains("xương") || input.Contains("khớp") || input.Contains("thoái hóa") || input.Contains("cột sống") || input.Contains("mỏi gối") || input.Contains("vai gáy"))
            return "Cơ xương khớp";

        if (input.Contains("tai") || input.Contains("mũi") || input.Contains("họng") || input.Contains("viêm xoang") || input.Contains("ho lâu ngày") || input.Contains("ù tai"))
            return "Tai Mũi Họng";

        if (input.Contains("mắt") || input.Contains("nhìn mờ") || input.Contains("cận thị") || input.Contains("đỏ mắt") || input.Contains("cộm"))
            return "Mắt";

        if (input.Contains("tổng quát") || input.Contains("kiểm tra sức khỏe") || input.Contains("mệt mỏi") || input.Contains("khám sức khỏe"))
            return "Nội tổng quát";

        return null;
    }
}
