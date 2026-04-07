using SignalRChatRoom.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using SignalRChatRoom.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// ==============================================================================
// 1. CẤU HÌNH SERVICES (AddCors, AddHttpClient, AddSignalR)
// ==============================================================================

// Cấu hình chính sách CORS.
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()
              .SetIsOriginAllowed(origin => true) // Cho phép tất cả domain
    )
);

// Thêm dịch vụ AI
builder.Services.AddHttpClient<IAiChatService, OpenAiChatService>();
// HttpClientFactory sẽ quản lý vòng đời của HttpClient.

// ⭐ CẤU HÌNH SIGNALR SERVICES (Thêm và đặt giới hạn) ⭐
builder.Services.AddSignalR(options =>
{
    // Tăng giới hạn tin nhắn lên 10MB (10 * 1024 * 1024 = 10485760 bytes).
    // Phải lớn hơn giới hạn đọc file 10_000_000 bytes trên Client.
    options.MaximumReceiveMessageSize = 10485760;
}).AddJsonProtocol(options =>
{
    // Tăng độ sâu cho các đối tượng phức tạp (nếu cần)
    options.PayloadSerializerOptions.MaxDepth = 64;
});
// ⭐ KẾT THÚC CẤU HÌNH SIGNALR SERVICES ⭐


// ==============================================================================
// 2. XÂY DỰNG ỨNG DỤNG (CHỈ MỘT LẦN)
// ==============================================================================
var app = builder.Build();

// ==============================================================================
// 3. CẤU HÌNH MIDDLEWARE VÀ ENDPOINT
// ==============================================================================

// Kích hoạt middleware CORS đã cấu hình ở trên.
app.UseCors();

// ⭐ CẤU HÌNH ENDPOINT SIGNALR (Đặt giới hạn buffer) ⭐
// Khai báo endpoint cho Hub.
// Client sẽ kết nối đến đường dẫn "/chathub" để sử dụng SignalR.
app.MapHub<ChatHub>("/chathub", options =>
{
    // Tăng giới hạn kích thước buffer cho nhận dữ liệu trên endpoint (10 MB)
    options.ApplicationMaxBufferSize = 10485760;
    options.TransportMaxBufferSize = 10485760;
});
// ⭐ KẾT THÚC CẤU HÌNH ENDPOINT ⭐


app.Run();