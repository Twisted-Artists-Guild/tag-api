using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TAGWEBAPI.Data;
using TAGWEBAPI.Hubs;
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConversationsController : ControllerBase
{
    private readonly TAGDBContext _context;
    private readonly IDataProtector _protector;
    private readonly IHubContext<MessagingHub> _hubContext;

    public ConversationsController(TAGDBContext context, IDataProtectionProvider provider, IHubContext<MessagingHub> hubContext)
    {
        _context = context;
        _protector = provider.CreateProtector("Messages.Envelope.v1");
        _hubContext = hubContext;
    }

    // GET /api/conversations?userId={id}
    [HttpGet]
    public async Task<IActionResult> GetConversations([FromQuery] int userId)
    {
        // Find conversation IDs the user participates in
        var conversationIds = await _context.ConversationParticipants
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.ConversationId)
            .Distinct()
            .ToListAsync();

        if (!conversationIds.Any())
            return Ok(Array.Empty<object>());

        // Load conversations
        var conversations = await _context.Conversations
            .AsNoTracking()
            .Where(c => conversationIds.Contains(c.Id))
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .ToListAsync();

        // Load participants for these conversations
        var participantsRows = await _context.ConversationParticipants
            .AsNoTracking()
            .Where(p => conversationIds.Contains(p.ConversationId))
            .ToListAsync();

        var participantsByConversation = participantsRows
            .GroupBy(p => p.ConversationId)
            .ToDictionary(g => g.Key, g => g.Select(p => p).ToList());

        // Resolve distinct user IDs and load users + profile pics
        var allParticipantUserIds = participantsRows.Select(p => p.UserId).Distinct().ToList();
        var users = await _context.NextAuthUsers
            .AsNoTracking()
            .Where(u => allParticipantUserIds.Contains(u.Id))
            .ToListAsync();

        var userMap = users.ToDictionary(u => u.Id, u => u);

        // Load last messages for conversations
        var lastMessages = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId.HasValue && conversationIds.Contains(m.ConversationId.Value))
            .GroupBy(m => m.ConversationId)
            .Select(g => g.OrderByDescending(m => m.Sent).FirstOrDefault())
            .ToListAsync();

        var lastMessageMap = lastMessages
            .Where(m => m != null && m.ConversationId.HasValue)
            .ToDictionary(m => m!.ConversationId!.Value, m => m!);

        // Unread counts
        var unreadCounts = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId.HasValue && conversationIds.Contains(m.ConversationId.Value) && !m.IsRead && m.FromUserID != userId)
            .GroupBy(m => m.ConversationId)
            .Select(g => new { ConversationId = g.Key!.Value, Count = g.Count() })
            .ToListAsync();

        var unreadMap = unreadCounts.ToDictionary(x => x.ConversationId, x => x.Count);

        var result = new List<object>();

        foreach (var conv in conversations)
        {
            participantsByConversation.TryGetValue(conv.Id, out var convParticipants);
            convParticipants ??= new List<ConversationParticipant>();

            lastMessageMap.TryGetValue(conv.Id, out var lastMsg);

            string? lastMsgContent = null;
            if (lastMsg != null && lastMsg.IsEncrypted && !string.IsNullOrEmpty(lastMsg.EncryptedBody))
            {
                try
                {
                    lastMsgContent = _protector.Unprotect(lastMsg.EncryptedBody);
                }
                catch
                {
                    lastMsgContent = null;
                }
            }
            else if (lastMsg != null)
            {
                lastMsgContent = lastMsg.DirMsg;
            }

            var participants = convParticipants.Select(p =>
            {
                userMap.TryGetValue(p.UserId, out var user);
                var displayName = user?.Name;
                var avatarUrl = user?.Image;

                return new
                {
                    id = $"{p.UserId}",
                    userId = $"{p.UserId}",
                    username = user?.Name,
                    displayName,
                    avatarUrl,
                    isOnline = false, // implement presence if available
                    role = "member", // adjust if you track roles
                    user = new
                    {
                        id = $"{user?.Id}",
                        name = displayName,
                        username = user?.Name,
                        image = avatarUrl
                    }
                };
            }).ToList();

            result.Add(new
            {
                id = $"{conv.Id}",
                conversationId = $"{conv.Id}",
                name = conv.Title ?? string.Empty,
                conversationType = conv.IsGroup ? "group" : "direct",
                isGroup = conv.IsGroup,
                lastActivityAt = (lastMsg?.Sent ?? conv.LastMessageAt ?? conv.CreatedAt),
                lastMessage = lastMsg == null ? null : new
                {
                    id = $"{lastMsg.MessageID}",
                    content = lastMsgContent,
                    senderId = $"{lastMsg.FromUserID}",
                    createdAt = lastMsg.Sent
                },
                unreadCount = unreadMap.TryGetValue(conv.Id, out var cnt) ? cnt : 0,
                participants
            });
        }

        return Ok(result);
    }

    // GET /api/conversations/unread-total?userId={id}
    [HttpGet("unread-total")]
    public async Task<IActionResult> GetUnreadTotal([FromQuery] int userId)
    {
        var total = await GetUnreadMessageTotalForUser(userId);
        var latest = await BuildLatestIncomingMessageSummaryForUser(userId);
        return Ok(new
        {
            userId,
            unreadMessages = total,
            latestMessage = latest,
            updatedAt = DateTime.UtcNow
        });
    }

    // POST /api/conversations
    [HttpPost]
    public async Task<IActionResult> CreateConversation([FromBody] CreateConversationRequest req)
    {
        if (req.ParticipantUserIds == null || !req.ParticipantUserIds.Any())
            return BadRequest("At least one participant is required.");

        var conv = new Conversation
        {
            Title = req.Title,
            IsGroup = req.IsGroup,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var uid in req.ParticipantUserIds)
        {
            conv.Participants.Add(new ConversationParticipant
            {
                UserId = uid
            });
        }

        _context.Conversations.Add(conv);
        await _context.SaveChangesAsync();

        // Return a compact DTO (avoid returning EF entity with navigation properties)
        var result = new
        {
            id = conv.Id,
            conversationId = conv.Id,
            title = conv.Title,
            isGroup = conv.IsGroup,
            createdAt = conv.CreatedAt,
            participants = conv.Participants.Select(p => p.UserId).ToArray()
        };

        // ADDED: Notify all participants via SignalR that a new conversation was created
        foreach (var uid in req.ParticipantUserIds)
        {
            await _hubContext.Clients.Group($"user-{uid}")
                .SendAsync("ConversationCreated", new
                {
                    conversationId = conv.Id,
                    name = conv.Title,
                    conversationType = conv.IsGroup ? "group" : "direct",
                    isGroup = conv.IsGroup,
                    createdAt = conv.CreatedAt
                });
        }

        return CreatedAtAction(nameof(GetConversationMessages), new { id = conv.Id }, result);
    }

    // POST /api/conversations/{id}/read  -> marks conversation as read for a given user
    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkConversationRead(int id, [FromBody] MarkReadRequest req)
    {
        // Validate conversation exists
        var conversation = await _context.Conversations.FindAsync(id);
        if (conversation == null)
        {
            return NotFound(new
            {
                error = "Conversation not found",
                message = $"No conversation found with ID conv-{id}"
            });
        }

        // Update participant last read time if present
        var participant = await _context.ConversationParticipants
            .FirstOrDefaultAsync(p => p.ConversationId == id && p.UserId == req.UserId);

        if (participant != null)
        {
            participant.LastReadAt = DateTime.UtcNow;
        }

        // Mark messages as read for that user (only messages not sent by that user)
        var unread = await _context.Messages
            .Where(m => m.ConversationId == id && !m.IsRead && m.FromUserID != req.UserId)
            .ToListAsync();

        foreach (var m in unread)
            m.IsRead = true;

        await _context.SaveChangesAsync();

        // ADDED: Notify other participants via SignalR that messages were read
        await _hubContext.Clients.Group($"conversation-{id}")
            .SendAsync("MessageStatusUpdated", new
            {
                conversationId = id,
                userId = req.UserId,
                status = "read",
                count = unread.Count,
                timestamp = DateTime.UtcNow
            });

        var unreadTotal = await GetUnreadMessageTotalForUser(req.UserId);
        var latest = await BuildLatestIncomingMessageSummaryForUser(req.UserId);
        await _hubContext.Clients.Group($"user-{req.UserId}")
            .SendAsync("NotificationSummaryUpdated", new
            {
                type = "messages",
                unreadMessages = unreadTotal,
                latestMessage = latest,
                timestamp = DateTime.UtcNow
            });

        return Ok(new
        {
            success = true,
            message = "Conversation marked as read",
            conversationId = $"{id}",
            userId = $"{req.UserId}"
        });
    }

    // GET /api/conversations/{id}/messages?page={n}&limit={m}
    [HttpGet("{id}/messages")]
    public async Task<IActionResult> GetConversationMessages(int id, [FromQuery] int page = 1, [FromQuery] int limit = 50)
    {
        if (page < 1) page = 1;
        if (limit < 1) limit = 20;

        // load non-deleted messages with related user and attachments
        var query = _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == id && !m.IsDeleted) // exclude deleted messages
            .Include(m => m.Attachments)
            .Include(m => m.FromUser)
            .Include(m => m.ToUser)
            .OrderByDescending(m => m.Sent);

        var total = await query.CountAsync();
        var items = await query.Skip((page - 1) * limit).Take(limit).ToListAsync();

        var messagesDto = items.Select(m =>
        {
            string? content = null;
            if (m.IsEncrypted && !string.IsNullOrEmpty(m.EncryptedBody))
            {
                try
                {
                    content = _protector.Unprotect(m.EncryptedBody);
                }
                catch
                {
                    content = null;
                }
            }
            else
            {
                content = m.DirMsg;
            }

            var sender = m.FromUser;
            var senderDisplayName = sender?.Name;
            var senderImage = sender?.Image;

            var user = sender;
            var userDisplayName = user?.Name;
            var userImage = user?.Image;

            var messageType = (m.GetType().GetProperty("MessageType") != null) ? m.GetType().GetProperty("MessageType")?.GetValue(m)?.ToString() : "text";
            var status = m.IsRead ? "read" : "delivered";

            var attachments = (m.Attachments ?? Enumerable.Empty<MessageAttachment>())
                .Select(a => (object)new
                {
                    id = a.Id,
                    fileName = a.FileName,
                    url = a.Url,
                    contentType = a.ContentType,
                    size = a.Size
                })
                .ToList();

            return new
            {
                id = $"{m.MessageID}",
                messageId = $"{m.MessageID}",
                conversationId = $"{m.ConversationId}",
                content,
                senderId = $"{m.FromUserID}",
                userId = $"{m.FromUserID}",
                sender = new
                {
                    id = $"{sender?.Id}",
                    name = senderDisplayName,
                    image = senderImage
                },
                user = new
                {
                    id = $"{user?.Id}",
                    name = userDisplayName,
                    username = user?.Name,
                    image = userImage
                },
                messageType = messageType ?? "text",
                status,
                isEdited = m.Edited,
                isDeleted = m.IsDeleted,
                createdAt = m.Sent,
                updatedAt = (m.GetType().GetProperty("UpdatedAt") != null) ? m.GetType().GetProperty("UpdatedAt")?.GetValue(m) : null,
                replyToId = (m.GetType().GetProperty("ReplyToId") != null) ? m.GetType().GetProperty("ReplyToId")?.GetValue(m) : null,
                attachments = attachments
            };
        }).ToList();

        var totalPages = (int)Math.Ceiling(total / (double)limit);
        var hasMore = page < totalPages;

        return Ok(new
        {
            messages = messagesDto,
            pagination = new
            {
                page,
                limit,
                total,
                totalPages,
                hasMore
            }
        });
    }

    // POST /api/conversations/messages
    // Accept ConversationId either via route {id} or in request body
    [HttpPost("messages")]
    [HttpPost("{id}/messages")]
    public async Task<IActionResult> PostMessageToConversation([FromRoute] int? id, [FromBody] PostMessageRequest req)
    {
        // Accept conversation id from route if provided; otherwise require it in body.
        var conversationId = id.HasValue && id.Value > 0 ? id.Value : req?.ConversationId ?? 0;
        if (conversationId <= 0)
            return BadRequest("ConversationId is required either as route parameter or in request body.");

        var conversation = await _context.Conversations.FindAsync(conversationId);
        if (conversation == null)
            return NotFound();

        var encrypted = req?.Body;
        try
        {
            // Protect payload server-side
            encrypted = _protector.Protect(req?.Body ?? string.Empty);
        }
        catch
        {
            // if protection fails, return 500
            return StatusCode(500, "Failed to protect message envelope");
        }

        var message = new Message
        {
            ConversationId = conversationId,
            FromUserID = req.FromUserId,
            ToUserID = req.ToUserId,
            EncryptedBody = encrypted,
            IsEncrypted = true,
            Sent = DateTime.UtcNow
        };

        _context.Messages.Add(message);
        conversation.LastMessageAt = message.Sent;
        await _context.SaveChangesAsync();

        // Load sender details for richer SignalR payload
        var sender = await _context.Users
            .AsNoTracking()
            .Include(u => u.ProfilePic)
            .FirstOrDefaultAsync(u => u.UserID == req.FromUserId);

        var senderDisplayName = sender?.PreferredName ?? $"{sender?.FirstName} {sender?.FamName}".Trim();
        var senderImage = sender?.ProfilePic?.URL;

        // Build an event payload to send via SignalR (includes decrypted body)
        var eventPayload = new
        {
            messageId = message.MessageID,
            conversationId = conversationId,
            body = req.Body,
            content = req.Body,
            fromUserId = message.FromUserID,
            senderId = message.FromUserID,
            senderDisplayName,
            avatarUrl = senderImage,
            createdAt = message.Sent,
            timestamp = message.Sent,
            messageType = req.MessageType ?? "text",
            attachments = "[]" // Add attachment handling if needed
        };

        // CRITICAL FIX: Broadcast to conversation group (includes sender via their connection)
        await _hubContext.Clients.Group($"conversation-{conversationId}")
            .SendAsync("ReceiveMessage", eventPayload);

        // Get participants who aren't currently in the conversation view
        var participants = await _context.ConversationParticipants
            .Where(cp => cp.ConversationId == conversationId && cp.UserId != req.FromUserId)
            .Select(cp => cp.UserId)
            .ToListAsync();

        // Notify offline users via their user-specific group
        foreach (var participantId in participants)
        {
            await _hubContext.Clients.Group($"user-{participantId}")
                .SendAsync("ConversationUpdated", new
                {
                    conversationId,
                    hasNewMessage = true,
                    lastMessage = new
                    {
                        content = req.Body,
                        senderId = req.FromUserId,
                        createdAt = message.Sent
                    }
                });

            var unreadTotal = await GetUnreadMessageTotalForUser(participantId);
            var latest = await BuildLatestIncomingMessageSummaryForUser(participantId);
            await _hubContext.Clients.Group($"user-{participantId}")
                .SendAsync("NotificationSummaryUpdated", new
                {
                    type = "messages",
                    unreadMessages = unreadTotal,
                    latestMessage = latest,
                    timestamp = DateTime.UtcNow
                });
        }

        // Return the created message details
        return Ok(new 
        { 
            messageId = message.MessageID,
            id = message.MessageID,
            content = req.Body,
            timestamp = message.Sent,
            status = "sent",
            conversationId = conversationId
        });
    }

    // DELETE /api/conversations/{id}?userId={userId}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteConversation(int id, [FromQuery] int userId)
    {
        // Load conversation with participants
        var conversation = await _context.Conversations
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (conversation == null)
        {
            return NotFound(new
            {
                error = "Conversation not found",
                message = $"No conversation found with ID {id}"
            });
        }

        // Ensure requester is a participant
        var participant = conversation.Participants.FirstOrDefault(p => p.UserId == userId);
        if (participant == null)
        {
            return StatusCode(Microsoft.AspNetCore.Http.StatusCodes.Status403Forbidden, new
            {
                error = "Unauthorized",
                message = "You are not a participant of this conversation"
            });
        }

        // ADDED: Get all participant IDs before deletion for SignalR notification
        var allParticipantIds = conversation.Participants.Select(p => p.UserId).ToList();

        // If only one participant (the requester), delete whole conversation and related data.
        if (conversation.Participants.Count <= 1)
        {
            // Remove message-related data before removing conversation to avoid FK issues.
            var messages = await _context.Messages
                .Where(m => m.ConversationId == id)
                .ToListAsync();

            if (messages.Any())
            {
                var messageIds = messages.Select(m => m.MessageID).ToList();

                var attachments = await _context.MessageAttachments
                    .Where(a => messageIds.Contains(a.MessageId))
                    .ToListAsync();
                if (attachments.Any()) _context.MessageAttachments.RemoveRange(attachments);

                var msgImpressions = await _context.MessageImpressions
                    .Where(mi => messageIds.Contains(mi.MessageId))
                    .ToListAsync();
                if (msgImpressions.Any()) _context.MessageImpressions.RemoveRange(msgImpressions);

                _context.Messages.RemoveRange(messages);
            }

            // Remove participants (likely only the requester)
            var parts = await _context.ConversationParticipants
                .Where(p => p.ConversationId == id)
                .ToListAsync();
            if (parts.Any()) _context.ConversationParticipants.RemoveRange(parts);

            // Finally remove conversation
            _context.Conversations.Remove(conversation);
            await _context.SaveChangesAsync();
        }
        else
        {
            // Just remove the participant (user leaves the conversation)
            _context.ConversationParticipants.Remove(participant);
            await _context.SaveChangesAsync();
        }

        // ADDED: Notify all participants via SignalR
        foreach (var participantId in allParticipantIds)
        {
            await _hubContext.Clients.Group($"user-{participantId}")
                .SendAsync("ConversationDeleted", new
                {
                    conversationId = id,
                    deletedBy = userId,
                    timestamp = DateTime.UtcNow
                });
        }

        // ADDED: Notify conversation group
        await _hubContext.Clients.Group($"conversation-{id}")
            .SendAsync("ConversationDeleted", new
            {
                conversationId = id,
                deletedBy = userId
            });

        return Ok(new
        {
            success = true,
            message = "Conversation deleted successfully"
        });
    }

    private async Task<int> GetUnreadMessageTotalForUser(int userId)
    {
        var conversationIds = await _context.ConversationParticipants
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.ConversationId)
            .Distinct()
            .ToListAsync();

        if (!conversationIds.Any())
        {
            return 0;
        }

        return await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId.HasValue
                && conversationIds.Contains(m.ConversationId.Value)
                && !m.IsRead
                && m.FromUserID != userId)
            .CountAsync();
    }

    private async Task<object?> BuildLatestIncomingMessageSummaryForUser(int userId)
    {
        var conversationIds = await _context.ConversationParticipants
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.ConversationId)
            .Distinct()
            .ToListAsync();

        if (!conversationIds.Any())
        {
            return null;
        }

        var latestIncoming = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId.HasValue
                && conversationIds.Contains(m.ConversationId.Value)
                && !m.IsDeleted
                && m.FromUserID != userId)
            .OrderByDescending(m => m.Sent)
            .Select(m => new
            {
                m.MessageID,
                ConversationId = m.ConversationId!.Value,
                m.FromUserID,
                m.Sent,
                m.IsEncrypted,
                m.EncryptedBody,
                m.DirMsg,
            })
            .FirstOrDefaultAsync();

        if (latestIncoming == null)
        {
            return null;
        }

        string? preview = null;
        if (latestIncoming.IsEncrypted && !string.IsNullOrEmpty(latestIncoming.EncryptedBody))
        {
            try
            {
                preview = _protector.Unprotect(latestIncoming.EncryptedBody);
            }
            catch
            {
                preview = null;
            }
        }
        else
        {
            preview = latestIncoming.DirMsg;
        }

        if (!string.IsNullOrWhiteSpace(preview) && preview.Length > 120)
        {
            preview = preview[..120];
        }

        var sender = await _context.NextAuthUsers
            .AsNoTracking()
            .Where(user => user.Id == latestIncoming.FromUserID)
            .Select(user => new { user.Name, user.Image })
            .FirstOrDefaultAsync();

        return new
        {
            messageId = latestIncoming.MessageID,
            conversationId = latestIncoming.ConversationId,
            senderName = string.IsNullOrWhiteSpace(sender?.Name) ? "Someone" : sender.Name,
            senderImage = sender?.Image,
            contentPreview = preview,
            href = $"/messages?conversationId={latestIncoming.ConversationId}",
            createdAt = latestIncoming.Sent,
        };
    }
}

// DTOs for controller requests
public record CreateConversationRequest(string? Title, bool IsGroup, int[] ParticipantUserIds);
public record MarkReadRequest(int UserId);
public record PostMessageRequest(int ConversationId, int FromUserId, int? ToUserId, string Body, string? MessageType = "text", int? AttachmentId = null);