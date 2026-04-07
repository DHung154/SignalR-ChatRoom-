using Microsoft.AspNetCore.SignalR;
using SignalRChatRoom.Server.InMemoryData;
using SignalRChatRoom.Server.Models;
using SignalRChatRoom.Server.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SignalRChatRoom.Server.Hubs
{
    public class ChatHub : Hub
    {
        private readonly IAiChatService _aiChatService;
        private const string AiAssistantName = "AI Assistant"; // Định nghĩa tên AI

        // Cập nhật Constructor để Inject dịch vụ AI
        public ChatHub(IAiChatService aiChatService)
        {
            _aiChatService = aiChatService;
        }

        // ==============================================================================
        // PHƯƠNG THỨC GỬI TIN NHẮN TỚI AI (ĐÃ THÊM messageId)
        // ==============================================================================
        // SignalRChatRoom.Server/Hubs/ChatHub.cs

        public async Task SendMessageToAiAsync(string message, string messageId) // ⭐ ĐÃ SỬA: Thêm messageId
        {
            var senderClient = ClientSource.Clients.FirstOrDefault(c => c.ConnectionId == Context.ConnectionId);

            if (senderClient == null) return;

            var aiClient = new Client { Username = AiAssistantName, ConnectionId = "AI_CONN_ID" };

            // 1. Phát lại tin nhắn gốc của người dùng (giúp người gửi thấy tin nhắn có ID đồng bộ)
            // LƯU Ý: Client đã tự thêm tin nhắn này vào local. Server phát lại để đảm bảo client nghe được.
            // await Clients.Caller.SendAsync("receiveMessage", message, senderClient, null, null, messageId); // KHÔNG CẦN, vì logic Client đã handle local.

            // 2. Lấy phản hồi từ AI
            var aiResponse = await _aiChatService.GetAiResponse(message);
            // Gán ID mới cho tin nhắn AI
            string aiMessageId = Guid.NewGuid().ToString();

            // 3. Gửi phản hồi của AI đến người dùng (Đây là tin nhắn nhận)
            // Tham số mới nhất: message, senderClient, client, groupName, messageId
            await Clients.Caller.SendAsync("receiveMessage", aiResponse, aiClient, senderClient, null, aiMessageId); // ⭐ ĐÃ SỬA: Thêm ID AI
        }

        // ==============================================================================
        // PHƯƠNG THỨC GỬI CẢM XÚC (REACTION) (ĐÃ SỬA LỖI LOGIC TRUYỀN TÍN HIỆU)
        // ==============================================================================
        /// <summary>
        /// Xử lý việc gửi cảm xúc (reaction) cho một tin nhắn và phát sóng đến người nhận/nhóm.
        /// </summary>
        /// <param name="targetUsernameOrGroupName">Tên người nhận (chat riêng) hoặc Tên nhóm (chat nhóm)</param>
        /// <param name="messageId">ID duy nhất của tin nhắn cần thả cảm xúc</param>
        /// <param name="reaction">Emoji cảm xúc (ví dụ: 👍, ❤️)</param>
        public async Task SendReactionAsync(string targetUsernameOrGroupName, string messageId, string reaction)
        {
            var sender = ClientSource.Clients.FirstOrDefault(c => c.ConnectionId == Context.ConnectionId);
            if (sender == null) return;

            // Không xử lý reaction cho AI Chat
            if (targetUsernameOrGroupName == AiAssistantName) return;

            // Xác định đích đến và loại chat
            var targetGroup = GroupSource.Groups.FirstOrDefault(g => g.GroupName == targetUsernameOrGroupName);
            var targetClient = ClientSource.Clients.FirstOrDefault(c => c.Username == targetUsernameOrGroupName);

            // Nếu là chat nhóm
            if (targetGroup != null)
            {
                // ⭐ ĐÃ SỬA: Dùng Clients.Group để gửi đến TẤT CẢ thành viên, BAO GỒM người gửi (Caller)
                // Params: senderUsername, targetGroupName, reaction, messageId, isGroup = true
                await Clients.Group(targetGroup.GroupName)
                    .SendAsync("receiveReaction", sender.Username, targetGroup.GroupName, reaction, messageId, true);
            }
            // Nếu là chat riêng (targetClient là người nhận ban đầu của tin nhắn)
            else if (targetClient != null)
            {
                var callerConnectionId = Context.ConnectionId;
                var targetConnectionId = targetClient.ConnectionId;

                // Lấy ConnectionIds của hai bên (A và B)
                var connectionIds = new List<string> { callerConnectionId, targetConnectionId }.Distinct().ToList();

                // ⭐ ĐÃ SỬA: Dùng Clients.Clients(connectionIds) để gửi đến CẢ A VÀ B
                // Params: senderUsername (người thả reaction), targetUsername, reaction, messageId, isGroup = false
                await Clients.Clients(connectionIds)
                    .SendAsync("receiveReaction", sender.Username, targetClient.Username, reaction, messageId, false);
            }
        }

        // ==============================================================================
        // CÁC PHƯƠNG THỨC HIỆN CÓ CỦA BẠN (ĐÃ SỬA LỖI MESSAGE ID)
        // ==============================================================================

        // Khi client gửi username (GIỮ NGUYÊN)
        public async Task GetUsernameAsync(string username)
        {
            var client = new Client
            {
                ConnectionId = Context.ConnectionId,
                Username = username
            };

            // 1. Lưu client mới vào danh sách
            ClientSource.Clients.Add(client);

            // 2. Thông báo cho người khác client đã tham gia
            await Clients.Others.SendAsync("clientJoined", username);

            // 3. Cập nhật danh sách toàn bộ client cho TẤT CẢ mọi người
            await GetClientsAsync();

            // 4. Gửi danh sách nhóm cho NGƯỜI VỪA ĐĂNG NHẬP (Caller)
            await Clients.Caller.SendAsync("groups", GroupSource.Groups);
        }

        // Cập nhật danh sách toàn bộ client cho TẤT CẢ mọi người (GIỮ NGUYÊN)
        public async Task GetClientsAsync()
        {
            await Clients.All.SendAsync("clients", ClientSource.Clients);
        }

        // Gửi tin nhắn riêng (Private Message) (ĐÃ SỬA: THÊM messageId và gửi cho CẢ hai bên)
        public async Task SendMessageAsync(string message, Client client, string messageId) // ⭐ ĐÃ SỬA: Thêm messageId
        {
            var sender = ClientSource.Clients.FirstOrDefault(c => c.ConnectionId == Context.ConnectionId);

            if (sender == null || client == null)
                return;

            // ⭐ ĐÃ SỬA: Gửi tin nhắn đến CẢ người nhận và người gửi (người gọi)
            var connectionIds = new List<string> { Context.ConnectionId, client.ConnectionId }.Distinct().ToList();

            // Tham số mới nhất: message, senderClient, client, groupName, messageId
            await Clients.Clients(connectionIds)
                .SendAsync("receiveMessage", message, sender, client, null, messageId); // ⭐ ĐÃ SỬA: Thêm messageId
        }

        // Thêm nhóm chat mới (GIỮ NGUYÊN)
        public async Task AddGroupAsync(string groupName)
        {
            var group = new Group { GroupName = groupName };

            // Lưu nhóm mới
            GroupSource.Groups.Add(group);

            // Thông báo cho TẤT CẢ mọi người nhóm mới đã được tạo
            await Clients.All.SendAsync("groupAdded", groupName);

            // Cập nhật danh sách nhóm cho TẤT CẢ mọi người
            await GetGroupsAsync();
        }

        // Cập nhật danh sách nhóm cho TẤT CẢ mọi người (GIỮ NGUYÊN)
        public async Task GetGroupsAsync()
        {
            await Clients.All.SendAsync("groups", GroupSource.Groups);
        }

        // Thêm client vào nhóm chat (GIỮ NGUYÊN)
        public async Task AddClientToGroupAsync(string groupName)
        {
            var client = ClientSource.Clients.FirstOrDefault(c => c.ConnectionId == Context.ConnectionId);
            var group = GroupSource.Groups.FirstOrDefault(g => g.GroupName == groupName);

            if (client == null || group == null)
                return;

            // Nếu client chưa là thành viên của nhóm
            if (!group.Clients.Any(c => c.ConnectionId == client.ConnectionId))
            {
                group.Clients.Add(client);
                await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

                // 1. Gửi danh sách cập nhật đến TẤT CẢ thành viên trong nhóm (bao gồm người mới)
                await Clients.Group(groupName)
                    .SendAsync("clientsOfGroup", group.Clients, group.GroupName);

                // 2. Thông báo cho các thành viên khác trong nhóm rằng client này đã tham gia
                await Clients.OthersInGroup(groupName).SendAsync("clientJoined", client.Username);
            }
        }

        // Gửi danh sách thành viên trong nhóm đến NGƯỜI GỌI (Người dùng đang xem nhóm này) (GIỮ NGUYÊN)
        public async Task GetClientsOfGroupAsync(string groupName)
        {
            var group = GroupSource.Groups.FirstOrDefault(g => g.GroupName == groupName);
            if (group == null) return;

            await Clients.Caller
                .SendAsync("clientsOfGroup", group.Clients, group.GroupName);
        }

        // Gửi tin nhắn nhóm (Group Message) (ĐÃ SỬA: THÊM messageId)
        public async Task SendMessageToGroupAsync(string groupName, string message, string messageId) // ⭐ ĐÃ SỬA: Thêm messageId
        {
            var sender = ClientSource.Clients.FirstOrDefault(c => c.ConnectionId == Context.ConnectionId);
            if (sender == null) return;

            // Gửi tin nhắn tới tất cả client trong nhóm, bao gồm cả người gửi
            // Tham số mới nhất: message, senderClient, client, groupName, messageId
            await Clients.Group(groupName)
                .SendAsync("receiveMessage", message, sender, null, groupName, messageId); // ⭐ ĐÃ SỬA: Thêm messageId
        }

        // Xử lý khi client ngắt kết nối (GIỮ NGUYÊN)
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Client client = ClientSource.Clients
                .FirstOrDefault(c => c.ConnectionId == Context.ConnectionId);

            if (client != null)
            {
                // Thông báo client đã rời đi
                await Clients.All.SendAsync("clientLeaved", client.Username);

                // Xóa client
                ClientSource.Clients.Remove(client);

                // Cập nhật danh sách toàn bộ client
                await GetClientsAsync();
            }

            // Gỡ client khỏi nhóm và cập nhật danh sách cho các nhóm đó
            foreach (var group in GroupSource.Groups.ToList()) // Dùng ToList() để tránh lỗi sửa đổi collection
            {
                var member = group.Clients.FirstOrDefault(c => c.ConnectionId == Context.ConnectionId);
                if (member != null)
                {
                    group.Clients.Remove(member);
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, group.GroupName);

                    // Cập nhật danh sách thành viên cho những người còn lại trong nhóm
                    await Clients.Group(group.GroupName)
                        .SendAsync("clientsOfGroup", group.Clients, group.GroupName);
                }
            }

            await base.OnDisconnectedAsync(exception);
        }

        // Xử lý tín hiệu gõ (Typing Indicator) (GIỮ NGUYÊN)
        public async Task SendTypingIndicator(string targetName)
        {
            // ⭐️ 1. Lấy thông tin người gọi (Sender)
            var callerClient = ClientSource.Clients.FirstOrDefault(c => c.ConnectionId == Context.ConnectionId);

            if (callerClient == null) return;

            var callerName = callerClient.Username;

            // ⭐️ 2. Logic xác định đích đến (Target)
            // Trường hợp 1: Trò chuyện riêng tư HOẶC Trò chuyện với AI
            if (targetName == AiAssistantName)
            {
                // AI không cần tín hiệu gõ từ phía người dùng
                return;
            }

            var targetClient = ClientSource.Clients.FirstOrDefault(c => c.Username == targetName);

            if (targetClient != null) // Nếu targetName là tên của một người dùng
            {
                // Gửi tín hiệu gõ đến người nhận
                await Clients.Client(targetClient.ConnectionId)
                    .SendAsync("userTyping", callerName, targetName);
            }
            // Trường hợp 2: Trò chuyện nhóm
            else
            {
                var targetGroup = GroupSource.Groups.FirstOrDefault(g => g.GroupName == targetName);
                if (targetGroup != null)
                {
                    // Gửi tín hiệu gõ đến tất cả thành viên trong nhóm, NGOẠI TRỪ người gửi.
                    // Clients.Group(targetName) sẽ gửi đến tất cả thành viên. 
                    // Client sẽ tự loại trừ nếu username == CallerUsername
                    await Clients.Group(targetName)
                        .SendAsync("userTyping", callerName, targetName);
                }
            }
        }
    }
}