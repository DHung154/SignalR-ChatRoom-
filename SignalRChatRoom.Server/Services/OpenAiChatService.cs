using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using System; // Cần thiết cho ArgumentNullException

namespace SignalRChatRoom.Server.Services
{
    public class OpenAiChatService : IAiChatService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        // URL của Google Gemini API
        private const string GeminiUrl = "https://generativelanguage.googleapis.com/v1/models/gemini-2.5-flash:generateContent";

        public OpenAiChatService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            // Lấy API Key từ cấu hình
            _apiKey = configuration["AIConfig:ApiKey"] ?? throw new ArgumentNullException("AIConfig:ApiKey is not configured.");

            // Không cần Bearer Header cho Gemini API Key.
        }

        public async Task<string> GetAiResponse(string prompt)
        {
            // SỬA LỖI 1: Đổi 'config' thành 'generationConfig'
            // SỬA LỖI 2: Gộp System Instruction vào trong User Prompt cho cấu trúc đơn giản (single-turn)
            var requestBody = new
            {
                // Truyền lời nhắc người dùng
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[]
                        { 
                            // Ghép system instruction vào đầu prompt
                            new { text = $"You are a helpful assistant in a chat room. Answer the user's request. User request: {prompt}" }
                        }
                    }
                },

                // ⭐ KHẮC PHỤC LỖI CHÍNH: Tên khối cấu hình phải là generationConfig
                generationConfig = new
                {
                    temperature = 0.7
                }
            };

            var content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json"
            );

            // Thêm API Key vào query string
            var requestUrl = $"{GeminiUrl}?key={_apiKey}";

            try
            {
                var response = await _httpClient.PostAsync(requestUrl, content);

                var jsonResponse = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // Trả về thông báo lỗi chi tiết nếu API không thành công
                    Console.WriteLine($"Gemini API Error: {response.StatusCode} - {jsonResponse}");
                    // Cắt bớt lỗi để tránh làm tràn tin nhắn
                    return $"Lỗi: Không thể gọi Gemini API. (Code: {(int)response.StatusCode}). Chi tiết: {jsonResponse.Substring(0, Math.Min(100, jsonResponse.Length))}";
                }

                // Phân tích phản hồi JSON từ cấu trúc Gemini
                using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                    {
                        var candidate = candidates[0];
                        if (candidate.TryGetProperty("content", out var contentElement) &&
                            contentElement.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                        {
                            if (parts[0].TryGetProperty("text", out var textElement))
                            {
                                return textElement.GetString() ?? "AI không tạo ra phản hồi.";
                            }
                        }
                    }
                }
                return "Định dạng phản hồi AI không mong đợi hoặc trống.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error calling AI API: {ex.Message}");
                return $"Lỗi: Không thể kết nối với dịch vụ AI. Exception: {ex.Message}";
            }
        }
    }
}