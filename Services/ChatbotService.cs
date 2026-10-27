using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using StudentManagement.Data;
using StudentManagement.DTOs;
using StudentManagement.Models;

namespace StudentManagement.Services;

public class ChatbotService : IChatbotService
{
    private readonly AppDbContext context;
    private readonly IConfiguration configuration;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly IMemoryCache memoryCache;
    private readonly ILogger<ChatbotService> logger;

    // Lưu cấu hình động do Admin cập nhật trong runtime
    private static readonly ConcurrentDictionary<string, string> dynamicApiKeys = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string> dynamicModels = new(StringComparer.OrdinalIgnoreCase);
    private static string? dynamicProvider;
    private static string? dynamicAccountId;
    private static bool dynamicIsEnabled = true;

    public ChatbotService(
        AppDbContext context,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IMemoryCache memoryCache,
        ILogger<ChatbotService> logger)
    {
        this.context = context;
        this.configuration = configuration;
        this.httpClientFactory = httpClientFactory;
        this.memoryCache = memoryCache;
        this.logger = logger;
    }

    private string GetActiveApiKey()
    {
        var provider = GetActiveProvider();
        if (dynamicApiKeys.TryGetValue(provider, out var dynamicKey) && !string.IsNullOrWhiteSpace(dynamicKey))
            return dynamicKey.Trim();

        var configKey = provider switch
        {
            "Gemini" => configuration["Gemini:ApiKey"] ?? configuration["GeminiApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY"),
            "Mistral" => configuration["Mistral:ApiKey"] ?? Environment.GetEnvironmentVariable("MISTRAL_API_KEY"),
            "Groq" => configuration["Groq:ApiKey"] ?? Environment.GetEnvironmentVariable("GROQ_API_KEY"),
            "OpenRouter" => configuration["OpenRouter:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"),
            "Cloudflare" => configuration["Cloudflare:ApiKey"] ?? Environment.GetEnvironmentVariable("CLOUDFLARE_API_TOKEN"),
            _ => null
        };
        return configKey?.Trim() ?? string.Empty;
    }

    private string GetActiveProvider()
    {
        if (!string.IsNullOrWhiteSpace(dynamicProvider)) return dynamicProvider;
        var configuredProvider = configuration["Chatbot:Provider"];
        if (!string.IsNullOrWhiteSpace(configuredProvider)) return NormalizeProvider(configuredProvider);
        return !string.IsNullOrWhiteSpace(configuration["Gemini:ApiKey"] ?? configuration["GeminiApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY"))
            ? "Gemini"
            : "Mistral";
    }

    private static string NormalizeProvider(string? provider)
    {
        return provider?.Trim().ToLowerInvariant() switch
        {
            "mistral" => "Mistral",
            "groq" => "Groq",
            "openrouter" => "OpenRouter",
            "cloudflare" => "Cloudflare",
            _ => "Gemini"
        };
    }

    private string GetDefaultModel(string provider)
    {
        return provider switch
        {
            "Mistral" => "mistral-small-latest",
            "Groq" => "openai/gpt-oss-20b",
            "OpenRouter" => "mistralai/mistral-small-3.1-24b-instruct:free",
            "Cloudflare" => "@cf/meta/llama-3.1-8b-instruct",
            _ => configuration["Gemini:Model"] ?? "gemini-2.5-flash"
        };
    }

    private string GetActiveModel()
    {
        var provider = GetActiveProvider();
        var model = dynamicModels.TryGetValue(provider, out var selectedModel)
            ? selectedModel
            : GetDefaultModel(provider);
        if (model.Contains('('))
        {
            model = model.Split('(')[0].Trim();
        }
        if (model.StartsWith("models/"))
        {
            model = model["models/".Length..].Trim();
        }
        return model.Trim();
    }

    private bool IsAiEnabled()
    {
        return dynamicIsEnabled && (configuration.GetValue<bool?>("Chatbot:IsEnabled")
            ?? configuration.GetValue<bool?>("Gemini:IsEnabled")
            ?? true);
    }

    public Task<ChatbotConfigDto> GetConfigAsync()
    {
        var provider = GetActiveProvider();
        var key = GetActiveApiKey();
        var masked = string.IsNullOrWhiteSpace(key)
            ? string.Empty
            : (key.Length > 8 ? $"{key[..4]}...{key[^4..]}" : "****");

        return Task.FromResult(new ChatbotConfigDto
        {
            ApiKeyMasked = masked,
            Model = GetActiveModel(),
            Provider = provider,
            AccountId = dynamicAccountId ?? configuration["Cloudflare:AccountId"] ?? string.Empty,
            IsEnabled = IsAiEnabled(),
            HasKey = !string.IsNullOrWhiteSpace(key),
        });
    }

    public async Task<ChatbotConfigDto> UpdateConfigAsync(UpdateChatbotConfigDto updateDto)
    {
        if (!string.IsNullOrWhiteSpace(updateDto.Provider))
            dynamicProvider = NormalizeProvider(updateDto.Provider);

        var provider = GetActiveProvider();
        if (updateDto.ApiKey != null)
        {
            dynamicApiKeys[provider] = updateDto.ApiKey.Trim();
        }
        if (!string.IsNullOrWhiteSpace(updateDto.Model))
        {
            var cleanModel = updateDto.Model.Trim();
            if (cleanModel.Contains('(')) cleanModel = cleanModel.Split('(')[0].Trim();
            if (cleanModel.StartsWith("models/")) cleanModel = cleanModel["models/".Length..].Trim();
            dynamicModels[provider] = cleanModel;
        }
        if (updateDto.AccountId != null)
            dynamicAccountId = updateDto.AccountId.Trim();
        if (updateDto.IsEnabled.HasValue)
        {
            dynamicIsEnabled = updateDto.IsEnabled.Value;
        }

        return await GetConfigAsync();
    }

    public async Task<List<string>> GetAvailableModelsAsync(string? provider = null, string? accountId = null)
    {
        var selectedProvider = string.IsNullOrWhiteSpace(provider) ? GetActiveProvider() : NormalizeProvider(provider);
        var apiKey = GetActiveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey)) return [];
        if (selectedProvider == "Gemini") return await QueryAvailableModelsAsync(apiKey);

        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        using var request = new HttpRequestMessage(HttpMethod.Get, GetModelsEndpoint(selectedProvider, accountId));
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("[{Provider}] Model list request failed: {StatusCode}", selectedProvider, (int)response.StatusCode);
            return [];
        }

        var body = await response.Content.ReadAsStringAsync();
        var node = JsonNode.Parse(body);
        var models = (node?["data"] ?? node?["result"])?.AsArray()
            ?.Select(item => item?["id"]?.ToString() ?? item?["name"]?.ToString() ?? string.Empty)
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Where(model => selectedProvider != "OpenRouter" || model.EndsWith(":free", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(model => model, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
        return models;
    }

    private static string GetModelsEndpoint(string provider, string? accountId) => provider switch
    {
        "Mistral" => "https://api.mistral.ai/v1/models",
        "Groq" => "https://api.groq.com/openai/v1/models",
        "OpenRouter" => "https://openrouter.ai/api/v1/models",
        "Cloudflare" when !string.IsNullOrWhiteSpace(accountId) =>
            $"https://api.cloudflare.com/client/v4/accounts/{Uri.EscapeDataString(accountId.Trim())}/ai/models/search?task=Text%20Generation&per_page=100",
        "Cloudflare" => throw new InvalidOperationException("Nhập Cloudflare Account ID để tải danh sách model."),
        _ => throw new InvalidOperationException($"Nhà cung cấp AI không được hỗ trợ: {provider}")
    };

    public async Task<TestConnectionResponseDto> TestConnectionAsync(string? provider, string? testApiKey, string? testModel, string? accountId)
    {
        var selectedProvider = string.IsNullOrWhiteSpace(provider) ? GetActiveProvider() : NormalizeProvider(provider);
        dynamicProvider = selectedProvider;
        if (accountId != null)
            dynamicAccountId = accountId.Trim();
        if (selectedProvider == "Gemini")
            return await TestGeminiConnectionAsync(testApiKey, testModel);

        var apiKey = !string.IsNullOrWhiteSpace(testApiKey) ? testApiKey.Trim() : GetActiveApiKey();
        var model = !string.IsNullOrWhiteSpace(testModel) ? testModel.Trim() : GetActiveModel();
        if (string.IsNullOrWhiteSpace(apiKey))
            return new TestConnectionResponseDto { Success = false, Message = $"Chưa nhập API key {selectedProvider}." };
        if (selectedProvider == "Cloudflare" && string.IsNullOrWhiteSpace(accountId ?? dynamicAccountId ?? configuration["Cloudflare:AccountId"]))
            return new TestConnectionResponseDto { Success = false, Message = "Nhập Cloudflare Account ID để kiểm tra kết nối." };

        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            var endpoint = selectedProvider == "Cloudflare"
                ? $"https://api.cloudflare.com/client/v4/accounts/{Uri.EscapeDataString((accountId ?? dynamicAccountId ?? configuration["Cloudflare:AccountId"]!).Trim())}/ai/v1/chat/completions"
                : GetChatCompletionsEndpoint(selectedProvider);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                model,
                messages = new[] { new { role = "user", content = "Reply with the single word OK." } },
                max_tokens = 16,
                temperature = 0
            }), Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            var availableModels = await GetAvailableModelsForTestAsync(selectedProvider, apiKey, accountId);

            if (response.IsSuccessStatusCode)
            {
                if (!string.IsNullOrWhiteSpace(testApiKey))
                    dynamicApiKeys[selectedProvider] = apiKey;
                if (!string.IsNullOrWhiteSpace(testModel))
                    dynamicModels[selectedProvider] = model;
                if (selectedProvider == "Cloudflare" && !string.IsNullOrWhiteSpace(accountId))
                    dynamicAccountId = accountId.Trim();

                return new TestConnectionResponseDto
                {
                    Success = true,
                    StatusCode = (int)response.StatusCode,
                    Message = $"Kết nối {selectedProvider} thành công với model {model}.",
                    AvailableModels = availableModels
                };
            }

            var providerMessage = ExtractProviderError(responseBody);
            var message = (int)response.StatusCode == 429
                ? $"{selectedProvider} từ chối yêu cầu do giới hạn quota/rate limit. Hạn mức miễn phí phụ thuộc tài khoản và model."
                : $"{selectedProvider} trả lỗi {(int)response.StatusCode}: {providerMessage}";
            logger.LogWarning("[{Provider} Test] Status {StatusCode}: {ProviderMessage}", selectedProvider, (int)response.StatusCode, providerMessage);
            return new TestConnectionResponseDto
            {
                Success = false,
                StatusCode = (int)response.StatusCode,
                Message = message,
                ErrorDetails = providerMessage,
                AvailableModels = availableModels
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{Provider} Test] Kết nối thất bại", selectedProvider);
            return new TestConnectionResponseDto
            {
                Success = false,
                Message = $"Không thể kết nối tới {selectedProvider}: {ex.Message}"
            };
        }
    }

    private async Task<List<string>> GetAvailableModelsForTestAsync(string provider, string apiKey, string? accountId)
    {
        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            using var request = new HttpRequestMessage(HttpMethod.Get, GetModelsEndpoint(provider, accountId));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            using var response = await client.SendAsync(request);
            if (!response.IsSuccessStatusCode) return [];

            var node = JsonNode.Parse(await response.Content.ReadAsStringAsync());
            var models = (node?["data"] ?? node?["result"])?.AsArray()
                ?.Select(item => item?["id"]?.ToString() ?? item?["name"]?.ToString() ?? string.Empty)
                .Where(model => !string.IsNullOrWhiteSpace(model))
                .Where(model => provider != "OpenRouter" || model.EndsWith(":free", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(model => model, StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];
            return models;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[{Provider}] Không lấy được danh sách model", provider);
            return [];
        }
    }

    private static string GetChatCompletionsEndpoint(string provider) => provider switch
    {
        "Mistral" => "https://api.mistral.ai/v1/chat/completions",
        "Groq" => "https://api.groq.com/openai/v1/chat/completions",
        "OpenRouter" => "https://openrouter.ai/api/v1/chat/completions",
        _ => throw new InvalidOperationException($"Nhà cung cấp AI không được hỗ trợ: {provider}")
    };

    private static string ExtractProviderError(string responseBody)
    {
        try
        {
            var node = JsonNode.Parse(responseBody);
            return node?["error"]?["message"]?.ToString()
                ?? node?["message"]?.ToString()
                ?? "Không có nội dung lỗi chi tiết.";
        }
        catch
        {
            return "Không đọc được phản hồi lỗi từ nhà cung cấp.";
        }
    }

    private async Task<List<string>> QueryAvailableModelsAsync(string apiKey)
    {
        var models = new List<string>();
        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            var endpoints = new[]
            {
                $"https://generativelanguage.googleapis.com/v1beta/models?key={apiKey}",
                $"https://generativelanguage.googleapis.com/v1/models?key={apiKey}"
            };

            foreach (var listUrl in endpoints)
            {
                try
                {
                    var res = await client.GetAsync(listUrl);
                    if (res.IsSuccessStatusCode)
                    {
                        var body = await res.Content.ReadAsStringAsync();
                        var node = JsonNode.Parse(body);
                        var items = node?["models"]?.AsArray();
                        if (items != null)
                        {
                            foreach (var item in items)
                            {
                                var name = item?["name"]?.ToString() ?? "";
                                var methods = item?["supportedGenerationMethods"]?.AsArray();
                                bool supportsGenerate = methods != null && methods.Any(m => m?.ToString() == "generateContent");
                                if (supportsGenerate)
                                {
                                    var cleanName = name.StartsWith("models/") ? name["models/".Length..] : name;
                                    if (!models.Contains(cleanName)) models.Add(cleanName);
                                }
                            }
                        }
                        if (models.Count > 0) break;
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Gemini] Không thể lấy danh sách models từ Google");
        }
        return models;
    }

    private async Task<TestConnectionResponseDto> TestGeminiConnectionAsync(string? testApiKey = null, string? testModel = null)
    {
        var key = !string.IsNullOrWhiteSpace(testApiKey) ? testApiKey.Trim() : GetActiveApiKey();
        if (string.IsNullOrWhiteSpace(key))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[Gemini Test WARNING] Chưa có API Key để kiểm tra kết nối.");
            Console.ResetColor();
            return new TestConnectionResponseDto
            {
                Success = false,
                Message = "Chưa cung cấp API Key. Vui lòng nhập API Key vào ô cấu hình trước khi kiểm tra."
            };
        }

        var model = !string.IsNullOrWhiteSpace(testModel) ? testModel.Trim() : GetActiveModel();
        if (model.Contains('(')) model = model.Split('(')[0].Trim();
        if (model.StartsWith("models/")) model = model["models/".Length..].Trim();

        Console.WriteLine("\n================== [GEMINI TEST CONNECTION] ==================");
        Console.WriteLine($"[Gemini Test] Model: {model}");
        Console.WriteLine($"[Gemini Test] API Key: {(key.Length > 8 ? $"{key[..4]}...{key[^4..]}" : "****")}");

        try
        {
            var client = httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);

            // Payload chuẩn: bắt buộc có role:"user" cho Gemini 2.x
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = "Xin chào! Hãy trả lời đúng một từ: 'OK'" } }
                    }
                }
            };
            var json = JsonSerializer.Serialize(requestBody);

            // Helper: thử gọi generateContent theo endpoint đã cho, trả về HttpResponseMessage
            async Task<(HttpResponseMessage Res, string Body)> TryGenerate(string endpointModel)
            {
                var body = new StringContent(json, Encoding.UTF8, "application/json");
                // Thử v1beta trước
                var urlBeta = $"https://generativelanguage.googleapis.com/v1beta/models/{endpointModel}:generateContent?key={key}";
                Console.WriteLine($"[Gemini Test] Thử  → {urlBeta[..urlBeta.IndexOf('?')]}");
                var r = await client.PostAsync(urlBeta, body);
                if (r.IsSuccessStatusCode) return (r, await r.Content.ReadAsStringAsync());

                if (r.StatusCode == HttpStatusCode.NotFound)
                {
                    // Thử endpoint v1 (GA)
                    var urlV1 = $"https://generativelanguage.googleapis.com/v1/models/{endpointModel}:generateContent?key={key}";
                    Console.WriteLine($"[Gemini Test] v1beta→404, thử v1 → {urlV1[..urlV1.IndexOf('?')]}");
                    var bodyV1 = new StringContent(json, Encoding.UTF8, "application/json");
                    var r2 = await client.PostAsync(urlV1, bodyV1);
                    return (r2, await r2.Content.ReadAsStringAsync());
                }

                return (r, await r.Content.ReadAsStringAsync());
            }

            var (res, responseBody) = await TryGenerate(model);

            // Nếu model đã chọn không tồn tại, trả danh sách model khả dụng để Admin tự chọn.
            var availableModels = new List<string>();

            if (!res.IsSuccessStatusCode && res.StatusCode == HttpStatusCode.NotFound)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("[Gemini Test] Đang truy vấn ModelService.ListModels để tìm model thực sự khả dụng...");
                Console.ResetColor();

                availableModels = await QueryAvailableModelsAsync(key);
                if (availableModels.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"[Gemini Test] Models khả dụng: {string.Join(", ", availableModels)}");
                    Console.ResetColor();
                }
            }

            if (res.IsSuccessStatusCode)
            {
                if (!string.IsNullOrWhiteSpace(testModel))
                    dynamicModels["Gemini"] = model;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[Gemini Test SUCCESS] StatusCode: {(int)res.StatusCode}");
                Console.WriteLine($"[Gemini Test SUCCESS] Kết nối thành công với model: {model}");
                Console.ResetColor();
                Console.WriteLine("==============================================================\n");

                // Giữ hành vi hiện tại: nhận API key vừa nhập cho các lần gọi tiếp theo.
                if (!string.IsNullOrWhiteSpace(testApiKey))
                {
                    dynamicApiKeys["Gemini"] = key;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[Gemini] Đã lưu runtime API key: ****...{key[^4..]}");
                    Console.ResetColor();
                }

                return new TestConnectionResponseDto
                {
                    Success = true,
                    StatusCode = (int)res.StatusCode,
                    Message = $"Kết nối Google Gemini AI ({model}) thành công!",
                    AvailableModels = availableModels
                };
            }

            // Xử lý lỗi cuối cùng
            var statusCode = (int)res.StatusCode;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Gemini Test ERROR] StatusCode: {statusCode} ({res.ReasonPhrase})");
            Console.WriteLine($"[Gemini Test ERROR] Response Body:\n{responseBody}");
            Console.ResetColor();
            Console.WriteLine("==============================================================\n");

            logger.LogError("[Gemini Test Error] Status: {StatusCode}, Body: {Body}", statusCode, responseBody);

            string errorMsgFromGoogle = string.Empty;
            string quotaModel = string.Empty;
            string quotaDescription = string.Empty;
            string retryDelay = string.Empty;
            try
            {
                var errNode = JsonNode.Parse(responseBody);
                var errorNode = errNode?["error"];
                errorMsgFromGoogle = errorNode?["message"]?.ToString() ?? string.Empty;
                if (errorNode?["details"] is JsonArray errorDetails)
                {
                    foreach (var detail in errorDetails)
                    {
                        var type = detail?["@type"]?.ToString() ?? string.Empty;
                        if (type.EndsWith("QuotaFailure", StringComparison.Ordinal) &&
                            detail?["violations"] is JsonArray violations)
                        {
                            var violation = violations.FirstOrDefault();
                            quotaModel = violation?["quotaDimensions"]?["model"]?.ToString() ?? string.Empty;
                            quotaDescription = violation?["description"]?.ToString() ?? string.Empty;
                        }
                        else if (type.EndsWith("RetryInfo", StringComparison.Ordinal))
                        {
                            retryDelay = detail?["retryDelay"]?.ToString() ?? string.Empty;
                        }
                    }
                }
            }
            catch { }

            string userFriendlyMsg;
            if (statusCode == 400)
                userFriendlyMsg = "Yêu cầu không hợp lệ (400 Bad Request). Vui lòng kiểm tra lại mã Model hoặc định dạng yêu cầu.";
            else if (statusCode is 401 or 403)
                userFriendlyMsg = "Khóa API không hợp lệ hoặc bị từ chối (403/401). Vui lòng kiểm tra lại API Key từ Google AI Studio.";
            else if (statusCode == 404)
            {
                if (availableModels.Count > 0)
                    userFriendlyMsg = $"Model '{model}' không khả dụng với API key này. Hãy chọn một model trong danh sách được tải từ tài khoản của bạn.";
                else
                    userFriendlyMsg = $"Không tìm thấy Model '{model}' (404 Not Found). Vui lòng chọn model khác (ví dụ: gemini-2.5-flash hoặc gemini-2.5-pro).";
            }
            else if (statusCode == 429)
            {
                var modelInfo = !string.IsNullOrWhiteSpace(quotaModel) ? $" Model bị từ chối: {quotaModel}." : string.Empty;
                var quotaInfo = responseBody.Contains("limit: 0", StringComparison.OrdinalIgnoreCase)
                    ? " Hạn mức hiện tại của model bằng 0; chờ rồi thử lại sẽ không khắc phục nếu Google chưa cấp quota."
                    : string.Empty;
                var retryInfo = !string.IsNullOrWhiteSpace(retryDelay)
                    ? $" Google đề nghị thử lại sau {retryDelay}."
                    : string.Empty;
                userFriendlyMsg = $"Google từ chối yêu cầu vì quota Gemini API (429).{modelInfo}{quotaInfo}{retryInfo} Danh sách model hiển thị không bảo đảm tài khoản có quota cho model đó. Hãy chọn model đã kiểm tra kết nối thành công hoặc kiểm tra quota/billing trong Google AI Studio.";
            }
            else
                userFriendlyMsg = $"Google API báo lỗi: {statusCode} {res.ReasonPhrase}";

            return new TestConnectionResponseDto
            {
                Success = false,
                StatusCode = statusCode,
                Message = userFriendlyMsg,
                ErrorDetails = statusCode == 429
                    ? (!string.IsNullOrWhiteSpace(quotaDescription) ? quotaDescription : errorMsgFromGoogle)
                    : (!string.IsNullOrWhiteSpace(errorMsgFromGoogle) ? errorMsgFromGoogle : responseBody),
                AvailableModels = availableModels
            };
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Gemini Test EXCEPTION] Lỗi kết nối: {ex.Message}");
            Console.ResetColor();
            Console.WriteLine("==============================================================\n");
            logger.LogError(ex, "[Gemini Test Exception] Lỗi kết nối tới Google API");

            return new TestConnectionResponseDto
            {
                Success = false,
                Message = $"Không thể kết nối tới máy chủ Google (Lỗi mạng hoặc Timeout): {ex.Message}",
                ErrorDetails = ex.Message
            };
        }
    }


    public async Task<ChatbotMessageResponse> ProcessMessageAsync(string message, int? userId = null, string? sessionId = null)
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

        // Quản lý bộ nhớ hội thoại (Memory Cache) theo sessionId (SlidingExpiration: 30 phút)
        var activeSessionId = !string.IsNullOrWhiteSpace(sessionId) ? sessionId.Trim() : Guid.NewGuid().ToString();
        var cacheKey = $"chatbot_memory_{activeSessionId}";
        var history = memoryCache.Get<List<GeminiChatMessage>>(cacheKey) ?? new List<GeminiChatMessage>();

        // Đảm bảo không có 2 message 'user' liên tiếp nếu lượt trước bị gián đoạn
        while (history.Count > 0 && history[^1].Role == "user")
        {
            history.RemoveAt(history.Count - 1);
        }

        // Thêm tin nhắn hiện tại của user vào lịch sử
        history.Add(new GeminiChatMessage
        {
            Role = "user",
            Parts = new List<GeminiChatPart> { new() { Text = raw } }
        });
        memoryCache.Set(cacheKey, history, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(30) });

        // TỐI ƯU HÓA CONTEXT WINDOW (Nhiệm vụ 3):
        // Loại bỏ việc nhét toàn bộ List<Doctor> vào systemPrompt mặc định.
        // Chỉ query SQL Server (EF Core) 2-3 bác sĩ phù hợp nhất khi người dùng hỏi về chuyên khoa hoặc tìm bác sĩ.
        var matchedSpecialty = DetectSpecialty(lower);
        List<Doctor> relevantDoctors = new();

        if (!string.IsNullOrEmpty(matchedSpecialty))
        {
            relevantDoctors = await context.Doctors.AsNoTracking()
                .Where(d => d.IsActive && d.Specialty.Contains(matchedSpecialty))
                .OrderByDescending(d => d.ExperienceYears)
                .Take(3)
                .ToListAsync();
        }
        else if (lower.Contains("bác sĩ") || lower.Contains("chuyên khoa") || lower.Contains("đặt lịch") || lower.Contains("hẹn khám") || lower.Contains("khám"))
        {
            var prevUserText = history.Where(h => h.Role == "user").SelectMany(h => h.Parts).LastOrDefault()?.Text?.ToLowerInvariant() ?? "";
            var prevSpecialty = DetectSpecialty(prevUserText);
            if (!string.IsNullOrEmpty(prevSpecialty))
            {
                relevantDoctors = await context.Doctors.AsNoTracking()
                    .Where(d => d.IsActive && d.Specialty.Contains(prevSpecialty))
                    .OrderByDescending(d => d.ExperienceYears)
                    .Take(3)
                    .ToListAsync();
                matchedSpecialty = prevSpecialty;
            }
            else
            {
                relevantDoctors = await context.Doctors.AsNoTracking()
                    .Where(d => d.IsActive)
                    .OrderByDescending(d => d.ExperienceYears)
                    .Take(3)
                    .ToListAsync();
            }
        }

        // Kiểm tra cấu hình nhà cung cấp AI đang được chọn.
        var apiKey = GetActiveApiKey();
        var provider = GetActiveProvider();
        var useAiProvider = !string.IsNullOrWhiteSpace(apiKey) && IsAiEnabled();

        if (useAiProvider)
        {
            try
            {
                var aiResult = provider == "Gemini"
                    ? await CallGeminiApiAsync(history, relevantDoctors, apiKey)
                    : await CallOpenAiCompatibleApiAsync(provider, history, relevantDoctors, apiKey);

                // NHIỆM VỤ 4: Xử lý Function Calling (Tự động đặt lịch khám)
                if (aiResult != null && aiResult.IsFunctionCall && aiResult.FunctionName == "create_appointment")
                {
                    var args = aiResult.FunctionArgs;
                    int reqDoctorId = 0;
                    if (args?["doctorId"] != null)
                    {
                        _ = int.TryParse(args["doctorId"]?.ToString(), out reqDoctorId);
                    }
                    string dateStr = args?["date"]?.ToString() ?? "";
                    string reason = args?["reason"]?.ToString() ?? "Khám bệnh theo tư vấn của Medika AI";
                    int reqPatientId = 0;
                    if (args?["patientId"] != null)
                    {
                        _ = int.TryParse(args["patientId"]?.ToString(), out reqPatientId);
                    }

                    // 1. Xác thực hoặc gán Bác sĩ phù hợp
                    var doctor = reqDoctorId > 0 ? await context.Doctors.FindAsync(reqDoctorId) : null;
                    if (doctor == null)
                    {
                        doctor = relevantDoctors.FirstOrDefault() ?? await context.Doctors.FirstOrDefaultAsync(d => d.IsActive);
                    }
                    var finalDoctorId = doctor?.Id ?? 1;

                    // 2. Phân tích thời gian hẹn khám
                    if (!DateTime.TryParse(dateStr, out var appointmentDate) || appointmentDate < DateTime.Now)
                    {
                        // Mặc định ngày mai lúc 09:00 nếu không parse được thời gian
                        appointmentDate = DateTime.Today.AddDays(1).AddHours(9);
                    }

                    // 3. Xác thực hoặc khởi tạo Hồ sơ Bệnh nhân
                    int finalPatientId;
                    if (userId.HasValue && userId.Value > 0)
                    {
                        var patient = await context.Patients.FirstOrDefaultAsync(p => p.UserId == userId.Value);
                        if (patient == null)
                        {
                            var user = await context.Users.FindAsync(userId.Value);
                            patient = new Patient
                            {
                                UserId = userId.Value,
                                FullName = !string.IsNullOrWhiteSpace(user?.FullName) ? user.FullName : (user?.Username ?? "Bệnh nhân"),
                                DateOfBirth = new DateOnly(1990, 1, 1),
                                Phone = "0900000000",
                                Address = "TP. Hồ Chí Minh",
                                MedicalHistory = "Tạo tự động qua Medika AI"
                            };
                            context.Patients.Add(patient);
                            await context.SaveChangesAsync();
                        }
                        finalPatientId = patient.Id;
                    }
                    else if (reqPatientId > 0 && await context.Patients.AnyAsync(p => p.Id == reqPatientId))
                    {
                        finalPatientId = reqPatientId;
                    }
                    else
                    {
                        var guestPatient = await context.Patients.FirstOrDefaultAsync(p => p.FullName == "Khách vãng lai (AI Booking)");
                        if (guestPatient == null)
                        {
                            guestPatient = new Patient
                            {
                                FullName = "Khách vãng lai (AI Booking)",
                                DateOfBirth = new DateOnly(1990, 1, 1),
                                Phone = "19006868",
                                Address = "Đăng ký trực tuyến",
                                MedicalHistory = "Khách vãng lai đặt lịch qua Chatbot"
                            };
                            context.Patients.Add(guestPatient);
                            await context.SaveChangesAsync();
                        }
                        finalPatientId = guestPatient.Id;
                    }

                    // 4. Khởi tạo entity Appointment với trạng thái Pending và lưu thẳng vào AppDbContext
                    var appointment = new Appointment
                    {
                        PatientId = finalPatientId,
                        DoctorId = finalDoctorId,
                        UserId = userId,
                        AppointmentDate = appointmentDate,
                        Reason = reason.Trim(),
                        Status = AppointmentStatus.Pending,
                        CreatedAt = DateTime.Now
                    };

                    context.Appointments.Add(appointment);
                    await context.SaveChangesAsync();

                    // 5. Chuẩn bị câu trả lời xác nhận và cờ cập nhật UI
                    var docName = doctor?.FullName ?? $"Bác sĩ #{finalDoctorId}";
                    var specialty = doctor?.Specialty ?? "Đa khoa";
                    var room = doctor?.Room ?? "Khu khám bệnh Medika";

                    var confirmationReply = $"🎉 **XÁC NHẬN ĐẶT LỊCH KHÁM THÀNH CÔNG!**\n\n" +
                                            $"• **Mã lịch hẹn:** #{appointment.Id}\n" +
                                            $"• **Bác sĩ phụ trách:** {docName} ({specialty} - {room})\n" +
                                            $"• **Thời gian hẹn:** {appointmentDate:dd/MM/yyyy HH:mm}\n" +
                                            $"• **Lý do khám:** {reason}\n" +
                                            $"• **Trạng thái:** ⏳ Chờ xác nhận (Pending)\n\n" +
                                            $"Phòng khám Đa khoa Medika đã tiếp nhận lịch khám. Nhân viên chăm sóc khách hàng sẽ liên hệ xác nhận trong thời gian sớm nhất. Hệ thống đang tự động làm mới giao diện của bạn!";

                    history.Add(new GeminiChatMessage
                    {
                        Role = "model",
                        Parts = new List<GeminiChatPart> { new() { Text = confirmationReply } }
                    });

                    if (history.Count > 20) history = history.Skip(history.Count - 20).ToList();
                    memoryCache.Set(cacheKey, history, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(30) });

                    response.Reply = confirmationReply;
                    response.Source = $"{provider} AI (Auto-Booking)";
                    response.Intent = "appointment_created";
                    response.ShouldRefreshUi = true;
                    response.CreatedAppointmentId = appointment.Id;
                    response.Doctors = doctor != null ? new List<ChatbotDoctorInfo> { ToDoctorInfo(doctor) } : [];
                    response.QuickActions = new List<ChatbotQuickAction>
                    {
                        new() { Label = "📋 Xem lịch hẹn của tôi", ActionType = "info", Value = "Lịch của tôi" },
                        new() { Label = "💬 Nhắn tin lễ tân hỗ trợ", ActionType = "support" }
                    };

                    return response;
                }

                // Xử lý phản hồi dạng Text thông thường
                if (aiResult != null && !string.IsNullOrWhiteSpace(aiResult.Text))
                {
                    var geminiReply = aiResult.Text;
                    history.Add(new GeminiChatMessage
                    {
                        Role = "model",
                        Parts = new List<GeminiChatPart> { new() { Text = geminiReply } }
                    });

                    if (history.Count > 20) history = history.Skip(history.Count - 20).ToList();
                    memoryCache.Set(cacheKey, history, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(30) });

                    response.Reply = geminiReply;
                    response.Source = provider == "Gemini" ? "Google Gemini AI" : $"{provider} AI";
                    response.Intent = "ai_consultation";

                    // Gợi ý bác sĩ nếu có trong danh sách truy vấn phù hợp hoặc nhận diện chuyên khoa
                    if (relevantDoctors.Count > 0)
                    {
                        response.Doctors = relevantDoctors.Select(ToDoctorInfo).ToList();
                    }
                    else
                    {
                        var detected = DetectSpecialty(lower) ?? DetectSpecialty(geminiReply.ToLowerInvariant());
                        if (!string.IsNullOrEmpty(detected))
                        {
                            var docs = await context.Doctors.AsNoTracking()
                                .Where(d => d.IsActive && d.Specialty.Contains(detected))
                                .OrderByDescending(d => d.ExperienceYears)
                                .Take(3)
                                .ToListAsync();
                            response.Doctors = docs.Select(ToDoctorInfo).ToList();
                            matchedSpecialty = detected;
                        }
                    }

                    if (!string.IsNullOrEmpty(matchedSpecialty))
                    {
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
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ChatbotService ProcessMessage EXCEPTION]: {ex.Message}");
                Console.ResetColor();
                logger.LogError(ex, "Lỗi ngoại lệ khi gọi nhà cung cấp AI {Provider}", provider);
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"[ChatbotService] AI provider {provider} chưa sẵn sàng (HasKey: {!string.IsNullOrWhiteSpace(apiKey)}, IsEnabled: {IsAiEnabled()}). Tự động dùng fallback nội bộ Medika.");
            Console.ResetColor();
        }

        // 2. Fallback xử lý thông minh nội bộ của Medika
        var fallbackResponse = await ProcessInternalFallbackAsync(raw, lower, userId, relevantDoctors);

        if (!string.IsNullOrWhiteSpace(fallbackResponse.Reply))
        {
            history.Add(new GeminiChatMessage
            {
                Role = "model",
                Parts = new List<GeminiChatPart> { new() { Text = fallbackResponse.Reply } }
            });

            if (history.Count > 20) history = history.Skip(history.Count - 20).ToList();
            memoryCache.Set(cacheKey, history, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(30) });
        }

        return fallbackResponse;
    }

    private async Task<GeminiCallResult?> CallOpenAiCompatibleApiAsync(
        string provider,
        List<GeminiChatMessage> history,
        List<Doctor> relevantDoctors,
        string apiKey)
    {
        var systemPrompt = $@"Bạn là Medika AI - Trợ lý Y tế & Đặt lịch thông minh của Phòng khám Đa khoa Quốc tế Medika.
Trả lời bằng tiếng Việt, ân cần, súc tích, không thay thế chẩn đoán của bác sĩ. Với triệu chứng, nêu khả năng sơ bộ, hướng dẫn dấu hiệu cần cấp cứu, gợi ý chuyên khoa và bác sĩ phù hợp.
Phòng khám tại Tòa nhà Medika Clinic, Số 88 Đường Y Dược, Quận 1, TP. Hồ Chí Minh; mở cửa 07:00-20:00 hằng ngày, cấp cứu 24/7, hotline 1900 6868. Medika tiếp nhận BHYT toàn quốc.
Ngày giờ hệ thống: {DateTime.Now:yyyy-MM-dd (dddd, HH:mm)}.
Khi người dùng xác nhận đặt lịch, chỉ gọi create_appointment sau khi đã có bác sĩ và thời gian rõ ràng. Không tự đặt lịch khi người dùng chưa xác nhận.";

        if (relevantDoctors.Count > 0)
        {
            var doctors = string.Join("\n", relevantDoctors.Select(d =>
                $"- ID: {d.Id} | {d.FullName} | {d.Specialty} | {d.Degree} | {d.ExperienceYears} năm kinh nghiệm | Phòng {d.Room}"));
            systemPrompt += $"\n\nBác sĩ phù hợp hiện có:\n{doctors}\nChỉ dùng ID trong danh sách này khi gọi create_appointment.";
        }

        var messages = new List<object> { new { role = "system", content = systemPrompt } };
        messages.AddRange(history.Select(item => new
        {
            role = item.Role == "model" ? "assistant" : "user",
            content = string.Join("\n", item.Parts.Select(part => part.Text))
        }));

        var tool = new
        {
            type = "function",
            function = new
            {
                name = "create_appointment",
                description = "Tạo lịch khám khi bệnh nhân đã xác nhận đặt lịch và cung cấp bác sĩ, thời gian.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        patientId = new { type = "integer", description = "ID bệnh nhân; dùng 0 nếu đang đăng nhập hoặc khách" },
                        doctorId = new { type = "integer", description = "ID bác sĩ đã chọn" },
                        date = new { type = "string", description = "Ngày giờ hẹn định dạng ISO hoặc YYYY-MM-DD HH:mm" },
                        reason = new { type = "string", description = "Lý do khám" }
                    },
                    required = new[] { "doctorId", "date", "reason" }
                }
            }
        };

        var accountId = dynamicAccountId ?? configuration["Cloudflare:AccountId"];
        var endpoint = provider == "Cloudflare"
            ? $"https://api.cloudflare.com/client/v4/accounts/{Uri.EscapeDataString(accountId ?? string.Empty)}/ai/v1/chat/completions"
            : GetChatCompletionsEndpoint(provider);
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            model = GetActiveModel(),
            messages,
            tools = new[] { tool },
            tool_choice = "auto",
            temperature = 0.3,
            max_tokens = 800
        }), Encoding.UTF8, "application/json");

        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(30);
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("[{Provider} Chat Error] Status: {StatusCode}, Body: {Body}", provider, (int)response.StatusCode, body);
            return null;
        }

        try
        {
            var message = JsonNode.Parse(body)?["choices"]?[0]?["message"];
            var toolCall = message?["tool_calls"]?[0]?["function"];
            if (toolCall != null)
            {
                var argsText = toolCall["arguments"]?.ToString();
                return new GeminiCallResult
                {
                    IsFunctionCall = true,
                    FunctionName = toolCall["name"]?.ToString(),
                    FunctionArgs = string.IsNullOrWhiteSpace(argsText) ? null : JsonNode.Parse(argsText)
                };
            }

            return new GeminiCallResult { Text = message?["content"]?.ToString() };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[{Provider}] Không đọc được phản hồi chat completions", provider);
            return null;
        }
    }

    private async Task<GeminiCallResult?> CallGeminiApiAsync(List<GeminiChatMessage> history, List<Doctor> relevantDoctors, string apiKey)
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(20);
        var model = GetActiveModel();
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

        // Tối ưu hóa Context Window: Hệ thống không còn nhồi nhét toàn bộ bác sĩ vào prompt mặc định
        var systemPrompt = $@"Bạn là Medika AI - Trợ lý Y tế & Đặt lịch thông minh của Phòng khám Đa khoa Quốc tế Medika.
Thông tin phòng khám:
- Địa chỉ: Tòa nhà Medika Clinic, Số 88 Đường Y Dược, Quận 1, TP. Hồ Chí Minh.
- Giờ làm việc: Thứ Hai - Chủ Nhật từ 07:00 đến 20:00 (làm việc thông trưa). Cấp cứu tiếp nhận 24/7.
- Hotline: 1900 6868.
- Tiếp nhận BHYT toàn quốc (đúng tuyến 80-100%) và bảo lãnh hơn 25 công ty bảo hiểm tư nhân.
- Các chuyên khoa tại phòng khám: Tim mạch, Tiêu hóa - Gan mật, Nhi khoa, Sản phụ khoa, Da liễu, Cơ xương khớp, Tai Mũi Họng, Mắt, Nội tổng quát.
- Ngày giờ hiện tại của hệ thống: {DateTime.Now:yyyy-MM-dd (dddd, HH:mm)}.

Nhiệm vụ của bạn:
1. Trả lời bằng tiếng Việt ân cần, chuyên nghiệp, chuẩn mực y khoa, dùng định dạng Markdown rõ ràng, dễ đọc.
2. Khi người dùng mô tả triệu chứng, hãy giải thích nguyên nhân sơ bộ có thể gặp, tư vấn chuyên khoa phù hợp và gợi ý bác sĩ tại phòng khám để người dùng đặt lịch khám.
3. Khi người dùng hỏi về thủ tục, BHYT, giá cả, giờ khám, hãy hướng dẫn tận tình, súc tích.
4. Ghi nhớ ngữ cảnh các câu hỏi và câu trả lời trước đó trong cuộc hội thoại để phản hồi tự nhiên, liền mạch và chính xác.
5. Khi người dùng đồng ý chốt thời gian, hãy gọi hàm create_appointment thay vì trả lời text thông thường.
6. Luôn thêm một lưu ý ngắn gọn ở cuối rằng lời khuyên chỉ mang tính tham khảo và khuyên người bệnh nên đến khám trực tiếp để được chẩn đoán chính xác.";

        // Nếu người dùng hỏi về chuyên khoa hoặc tìm bác sĩ, chèn 2-3 bác sĩ phù hợp nhất vào prompt
        if (relevantDoctors.Count > 0)
        {
            var doctorLines = string.Join("\n", relevantDoctors.Select(d =>
                $"- Bác sĩ ID: {d.Id} | Họ tên: {d.FullName} | Chuyên khoa: {d.Specialty} | Học vị: {d.Degree} | Kinh nghiệm: {d.ExperienceYears} năm | Phòng: {d.Room}"));
            systemPrompt += $"\n\n[DANH SÁCH BÁC SĨ PHÙ HỢP HIỆN CÓ ĐỂ TƯ VẤN & ĐẶT LỊCH]:\n{doctorLines}\n(Gợi ý bệnh nhân chọn bác sĩ và sử dụng đúng ID của bác sĩ này khi gọi hàm create_appointment).";
        }

        // Khai báo tool create_appointment cho Function Calling
        var tools = new object[]
        {
            new
            {
                function_declarations = new object[]
                {
                    new
                    {
                        name = "create_appointment",
                        description = "Tạo lịch hẹn khám bệnh mới cho bệnh nhân với bác sĩ tại phòng khám Medika khi người dùng chốt thời gian hoặc đồng ý đặt lịch.",
                        parameters = new
                        {
                            type = "OBJECT",
                            properties = new
                            {
                                patientId = new
                                {
                                    type = "INTEGER",
                                    description = "Mã ID hồ sơ bệnh nhân (truyền 0 nếu hệ thống tự động gán theo tài khoản đang đăng nhập)"
                                },
                                doctorId = new
                                {
                                    type = "INTEGER",
                                    description = "Mã ID của bác sĩ được chọn khám (lấy từ danh sách bác sĩ phù hợp được cung cấp)"
                                },
                                date = new
                                {
                                    type = "STRING",
                                    description = "Thời gian hẹn khám định dạng chuẩn ISO hoặc YYYY-MM-DD HH:mm (ví dụ: '2026-10-26 09:00')"
                                },
                                reason = new
                                {
                                    type = "STRING",
                                    description = "Lý do khám hoặc triệu chứng bệnh nhân mô tả"
                                }
                            },
                            required = new[] { "doctorId", "date", "reason" }
                        }
                    }
                }
            }
        };

        var requestBody = new
        {
            system_instruction = new
            {
                parts = new[]
                {
                    new { text = systemPrompt }
                }
            },
            contents = history.Select(h => new
            {
                role = h.Role,
                parts = h.Parts.Select(p => new { text = p.Text }).ToArray()
            }).ToArray(),
            tools = tools,
            generationConfig = new
            {
                temperature = 0.3,
                maxOutputTokens = 800
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(url, httpContent);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var v1Url = $"https://generativelanguage.googleapis.com/v1/models/{model}:generateContent?key={apiKey}";
            var v1Res = await client.PostAsync(v1Url, httpContent);
            if (v1Res.IsSuccessStatusCode)
            {
                var v1String = await v1Res.Content.ReadAsStringAsync();
                return ParseGeminiResponse(v1String);
            }
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            var statusCode = (int)response.StatusCode;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[Gemini Call ERROR] StatusCode: {statusCode} ({response.ReasonPhrase})");
            Console.WriteLine($"[Gemini Call ERROR] URL: https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent");
            Console.WriteLine($"[Gemini Call ERROR] Response Body:\n{errorBody}\n");
            Console.ResetColor();

            logger.LogError("[Gemini Call Error] Status: {StatusCode}, Body: {Body}", statusCode, errorBody);

            // Dự phòng nếu model endpoint không hỗ trợ system_instruction hoặc tools, gộp systemPrompt vào tin nhắn đầu
            var fallbackContents = history.Select((h, idx) => new
            {
                role = h.Role,
                parts = idx == 0 && h.Role == "user"
                    ? new[] { new { text = $"{systemPrompt}\n\n{h.Parts.FirstOrDefault()?.Text}" } }
                    : h.Parts.Select(p => new { text = p.Text }).ToArray()
            }).ToArray();

            var fallbackBody = new
            {
                contents = fallbackContents,
                generationConfig = new
                {
                    temperature = 0.3,
                    maxOutputTokens = 800
                }
            };

            var fallbackRes = await client.PostAsync(url, new StringContent(JsonSerializer.Serialize(fallbackBody), Encoding.UTF8, "application/json"));
            if (!fallbackRes.IsSuccessStatusCode)
            {
                var fallbackErr = await fallbackRes.Content.ReadAsStringAsync();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[Gemini Fallback Call ERROR] StatusCode: {(int)fallbackRes.StatusCode} ({fallbackRes.ReasonPhrase})");
                Console.WriteLine($"[Gemini Fallback Call ERROR] Response Body:\n{fallbackErr}\n");
                Console.ResetColor();

                logger.LogError("[Gemini Fallback Call Error] Status: {StatusCode}, Body: {Body}", (int)fallbackRes.StatusCode, fallbackErr);
                return null;
            }

            var fallbackStr = await fallbackRes.Content.ReadAsStringAsync();
            return ParseGeminiResponse(fallbackStr);
        }

        var resString = await response.Content.ReadAsStringAsync();
        return ParseGeminiResponse(resString);
    }

    private static GeminiCallResult? ParseGeminiResponse(string jsonResponse)
    {
        try
        {
            var doc = JsonNode.Parse(jsonResponse);
            var parts = doc?["candidates"]?[0]?["content"]?["parts"]?.AsArray();
            if (parts == null || parts.Count == 0) return null;

            foreach (var part in parts)
            {
                if (part?["functionCall"] != null)
                {
                    var fc = part["functionCall"];
                    return new GeminiCallResult
                    {
                        IsFunctionCall = true,
                        FunctionName = fc?["name"]?.ToString(),
                        FunctionArgs = fc?["args"]
                    };
                }
            }

            var textPart = parts.FirstOrDefault(p => p?["text"] != null);
            var text = textPart?["text"]?.ToString();
            return new GeminiCallResult { Text = text };
        }
        catch
        {
            return null;
        }
    }

    private async Task<ChatbotMessageResponse> ProcessInternalFallbackAsync(string raw, string lower, int? userId, List<Doctor> doctors)
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
            var docs = doctors.Where(d => d.Specialty.Contains(matchedSpecialty, StringComparison.OrdinalIgnoreCase)).ToList();
            if (docs.Count == 0)
            {
                docs = await context.Doctors.AsNoTracking()
                    .Where(d => d.IsActive && d.Specialty.Contains(matchedSpecialty))
                    .OrderByDescending(d => d.ExperienceYears)
                    .Take(3)
                    .ToListAsync();
            }

            response.Doctors = docs.Select(ToDoctorInfo).ToList();
            response.Reply = $"Với nhu cầu hoặc triệu chứng bạn mô tả, chuyên khoa phù hợp nhất là: **Khoa {matchedSpecialty}**.\n\n" +
                             (docs.Any()
                                 ? $"Hiện phòng khám có {docs.Count} bác sĩ chuyên khoa này sẵn sàng tiếp nhận khám:"
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
            var docs = doctors.Any() ? doctors : await context.Doctors.AsNoTracking().Where(d => d.IsActive).OrderByDescending(d => d.ExperienceYears).Take(3).ToListAsync();
            response.Doctors = docs.Take(3).Select(ToDoctorInfo).ToList();
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

public class GeminiChatMessage
{
    public string Role { get; set; } = "user";
    public List<GeminiChatPart> Parts { get; set; } = new();
}

public class GeminiChatPart
{
    public string Text { get; set; } = string.Empty;
}

public class GeminiCallResult
{
    public string? Text { get; set; }
    public bool IsFunctionCall { get; set; }
    public string? FunctionName { get; set; }
    public JsonNode? FunctionArgs { get; set; }
}
