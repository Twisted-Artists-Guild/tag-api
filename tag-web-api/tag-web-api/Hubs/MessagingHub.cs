using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace TAGWEBAPI.Hubs;

public class MessagingHub : Hub
{
    // Adds the connection to a user-specific group so you can target messages to a user
    public async Task RegisterUser(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
    }

    // Join a conversation group to receive conversation-specific events
    public async Task JoinConversation(string conversationId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation-{conversationId}");
    }

    // Leave a conversation group
    public async Task LeaveConversation(string conversationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation-{conversationId}");
    }

    // Typing indicator broadcast to other clients in the conversation group
    public async Task SendTypingIndicator(string conversationId, bool isTyping)
    {
        var userId = Context.User?.Identity?.Name ?? Context.ConnectionId;
        await Clients.OthersInGroup($"conversation-{conversationId}")
            .SendAsync("TypingIndicator", new { conversationId, userId, isTyping });
    }

    // Mark message as read - ADD THIS METHOD
    public async Task MarkMessageAsRead(string conversationId, string messageId)
    {
        var userId = Context.User?.Identity?.Name ?? Context.ConnectionId;
        await Clients.Group($"conversation-{conversationId}")
            .SendAsync("MessageStatusUpdated", new
            {
                messageId,
                status = "read",
                userId
            });
    }

    // Override OnConnectedAsync to handle connection events
    public override async Task OnConnectedAsync()
    {
        Console.WriteLine($"Client connected: {Context.ConnectionId}");
        await base.OnConnectedAsync();
    }

    // Override OnDisconnectedAsync to handle disconnection events
    public override async Task OnDisconnectedAsync(Exception exception)
    {
        Console.WriteLine($"Client disconnected: {Context.ConnectionId}");
        await base.OnDisconnectedAsync(exception);
    }
}