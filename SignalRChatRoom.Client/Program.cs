using SignalRChatRoom.Client.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ⭐ CẤU HÌNH SIGNALR HUB ĐỂ HỖ TRỢ UPLOAD ẢNH LỚN ⭐
builder.Services.AddSignalR(options =>
{
    // Tăng giới hạn tin nhắn lên 10MB (phải lớn hơn giới hạn 10MB trong Client: 10_000_000).
    // Giá trị tính bằng byte. 10MB = 10 * 1024 * 1024 = 10485760.
    options.MaximumReceiveMessageSize = 10485760;
});
// ⭐ KẾT THÚC CẤU HÌNH ⭐

builder.Services.AddBlazorBootstrap();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
