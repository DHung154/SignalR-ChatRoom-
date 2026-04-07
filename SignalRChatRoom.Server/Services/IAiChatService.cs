namespace SignalRChatRoom.Server.Services
{
    public interface IAiChatService
    {
        Task<string> GetAiResponse(string prompt);
    }
}