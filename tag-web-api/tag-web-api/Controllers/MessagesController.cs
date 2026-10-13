using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR; // Add this
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TAGWEBAPI.Data;
using TAGWEBAPI.Hubs; // Add this
using TAGWEBAPI.Models;

namespace TAGWEBAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly TAGDBContext _context;
    private readonly IDataProtector _protector;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;
    private readonly IHubContext<MessagingHub> _hubContext; // Add this

    // allowed extensions / mime types
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".pdf", ".doc", ".docx", ".mp4", ".mov"
    };

    // Update constructor to inject IHubContext
    public MessagesController(
        TAGDBContext context, 
        IDataProtectionProvider provider, 
        IWebHostEnvironment env, 
        IConfiguration config,
        IHubContext<MessagingHub> hubContext) // Add this parameter
    {
        _context = context;
        _protector = provider.CreateProtector("Messages.Envelope.v1");
        _env = env;
        _config = config;
        _hubContext = hubContext; // Add this
    }

    // PUT /api/messages/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> PutMessage(int id, [FromBody] UpdateMessageRequest req)
    {
        if (req == null)
            return BadRequest("Request body is required.");

        var messageId = id > 0 ? id : req.MessageId;
        if (messageId <= 0)
            return BadRequest("MessageId is required either in route or body.");

        if (id > 0 && req.MessageId > 0 && id != req.MessageId)
            return BadRequest("Route id and body.MessageId do not match.");

        var msg = await _context.Messages.FindAsync(messageId);
        if (msg == null) return NotFound();

        if (req.UserId != msg.FromUserID)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Unauthorized", message = "You can only edit your own messages" });
        }

        if (string.IsNullOrWhiteSpace(req.Body))
            return BadRequest("Body is required.");

        try
        {
            msg.EncryptedBody = _protector.Protect(req.Body);
        }
        catch
        {
            return StatusCode(500, "Failed to protect message envelope");
        }

        msg.IsEncrypted = true;
        msg.Edited = true;

        var updatedAt = DateTime.UtcNow;
        var updatedAtProp = msg.GetType().GetProperty("UpdatedAt");
        if (updatedAtProp != null && updatedAtProp.CanWrite)
        {
            updatedAtProp.SetValue(msg, updatedAt);
        }

        await _context.SaveChangesAsync();

        // Broadcast the message update via SignalR
        if (msg.ConversationId.HasValue)
        {
            await _hubContext.Clients.Group($"conversation-{msg.ConversationId}")
                .SendAsync("MessageUpdated", new
                {
                    messageId = msg.MessageID,
                    conversationId = msg.ConversationId,
                    body = req.Body,
                    isEdited = true,
                    updatedAt = updatedAt
                });
        }

        return Ok(new
        {
            id = $"{messageId}",
            messageId = $"{messageId}",
            content = req.Body,
            isEdited = true,
            updatedAt = updatedAt.ToUniversalTime()
        });
    }

    // DELETE /api/messages/{id}?userId={id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMessage(int id, [FromQuery] int userId)
    {
        var msg = await _context.Messages.FindAsync(id);
        if (msg == null) return NotFound();

        if (msg.FromUserID != userId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Unauthorized", message = "You can only delete your own messages" });
        }

        msg.IsDeleted = true;

        var updatedAt = DateTime.UtcNow;
        var updatedAtProp = msg.GetType().GetProperty("UpdatedAt");
        if (updatedAtProp != null && updatedAtProp.CanWrite)
        {
            updatedAtProp.SetValue(msg, updatedAt);
        }

        await _context.SaveChangesAsync();

        // Broadcast the message deletion via SignalR
        if (msg.ConversationId.HasValue)
        {
            await _hubContext.Clients.Group($"conversation-{msg.ConversationId}")
                .SendAsync("MessageDeleted", new
                {
                    messageId = msg.MessageID,
                    conversationId = msg.ConversationId
                });
        }

        return Ok(new
        {
            success = true,
            message = "Message deleted successfully",
            messageId = $"{id}"
        });
    }

    // POST /api/messages/{id}/read
    [HttpPost("{id}/read")]
    public async Task<IActionResult> ReadMessage(int id, [FromBody] ReadMessageRequest req)
    {
        if (req == null)
            return BadRequest("Request body is required.");

        var messageId = id > 0 ? id : req.MessageId;
        if (messageId <= 0)
            return BadRequest("MessageId is required either in route or body.");

        if (id > 0 && req.MessageId > 0 && id != req.MessageId)
            return BadRequest("Route id and body.MessageId do not match.");

        var msg = await _context.Messages.FindAsync(messageId);
        if (msg == null)
            return NotFound();

        msg.IsRead = true;

        if (msg.ConversationId.HasValue)
        {
            var part = await _context.ConversationParticipants
                .FirstOrDefaultAsync(p => p.ConversationId == msg.ConversationId && p.UserId == req.UserId);
            if (part != null)
            {
                part.LastReadAt = DateTime.UtcNow;
            }

            // Broadcast read status via SignalR
            await _hubContext.Clients.Group($"conversation-{msg.ConversationId}")
                .SendAsync("MessageStatusUpdated", new
                {
                    messageId = msg.MessageID,
                    status = "read",
                    userId = req.UserId
                });
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "Message marked as read",
            messageId = $"{messageId}",
            userId = $"{req.UserId}"
        });
    }

    // POST /api/messages/upload
    // Accepts multipart/form-data with fields: file (IFormFile), ConversationId (string), UserId (string)
    [HttpPost("upload")]
    [DisableRequestSizeLimit]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAttachment([FromForm] UploadAttachmentRequest request)
    {
        if (request == null || request.File == null)
            return BadRequest(new { error = "Invalid file", message = "File is required" });

        var file = request.File;

        // Configurable max size (default 10 MB)
        var maxBytes = _config.GetValue<long?>("MessageUpload:MaxFileSizeBytes") ?? 10L * 1024 * 1024;
        if (file.Length > maxBytes)
        {
            return BadRequest(new { error = "Invalid file", message = $"File size exceeds {maxBytes / (1024*1024)}MB limit" });
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
        {
            return BadRequest(new { error = "Invalid file", message = "File type is not allowed" });
        }

        // Build storage path and filename
        var conversationFolder = string.IsNullOrWhiteSpace(request.ConversationId) ? "misc" : request.ConversationId.Trim();
        var safeFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
        var relativePath = Path.Combine("messages", conversationFolder, safeFileName).Replace('\\', '/');

        string fileUrl = string.Empty;
        bool storedInAzure = false;

        // If Azure Blob storage configured, upload to Azure; otherwise fall back to local wwwroot
        var azureConn = _config.GetValue<string>("AzureBlob:ConnectionString");
        var azureContainer = _config.GetValue<string>("AzureBlob:ContainerName") ?? "messages";

        if (!string.IsNullOrEmpty(azureConn))
        {
            try
            {
                // Requires Azure.Storage.Blobs package
                // var blobService = new BlobServiceClient(azureConn);
                // var container = blobService.GetBlobContainerClient(azureContainer);
                // await container.CreateIfNotExistsAsync(PublicAccessType.Blob);
                // var blobClient = container.GetBlobClient(relativePath);
                // await using (var stream = file.OpenReadStream())
                // {
                //     await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType });
                // }
                // fileUrl = blobClient.Uri.ToString();

                // If Azure SDK not available at compile time, fallback to throw to move to local path
                throw new InvalidOperationException("Azure Blob upload code not enabled; install Azure.Storage.Blobs and uncomment the implementation.");
            }
            catch (Exception ex)
            {
                // If Azure upload fails or not available, fall back to local storage
                // (Do not fail the request for recoverable fallback)
                _ = ex; // swallow for now
                storedInAzure = false;
            }
        }

        if (!storedInAzure)
        {
            var uploadsRoot = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
            var targetDir = Path.Combine(uploadsRoot, "messages", conversationFolder);
            Directory.CreateDirectory(targetDir);

            var filePath = Path.Combine(targetDir, safeFileName);
            await using (var stream = System.IO.File.Create(filePath))
            {
                await file.CopyToAsync(stream);
            }

            // Build a public URL relative to the app's base (ensure app.UseStaticFiles() enabled)
            var baseUrl = _config.GetValue<string>("App:BaseUrl")?.TrimEnd('/');
            if (!string.IsNullOrEmpty(baseUrl))
            {
                fileUrl = $"{baseUrl}/uploads/messages/{conversationFolder}/{safeFileName}";
            }
            else
            {
                // Relative URL
                fileUrl = $"/uploads/messages/{conversationFolder}/{safeFileName}";
            }
        }

        // Persist attachment metadata. MessageId may be null because upload happens before message creation.
        var attachment = new MessageAttachment
        {
            // If your model has MessageId and it's nullable, leave it null; otherwise don't set it here.
            FileName = file.FileName,
            ContentType = file.ContentType ?? "application/octet-stream",
            Url = fileUrl,
            Size = file.Length,
            CreatedAt = DateTime.UtcNow
        };

        _context.MessageAttachments.Add(attachment);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            fileName = attachment.FileName,
            fileUrl = attachment.Url,
            url = attachment.Url,
            fileType = attachment.ContentType,
            fileSize = attachment.Size,
            mimeType = attachment.ContentType,
            uploadedAt = attachment.CreatedAt.ToUniversalTime()
        });
    }
}

// DTOs
public record UpdateMessageRequest(int MessageId, string Body, int UserId);
public record ReadMessageRequest(int UserId, int MessageId);
public record UploadAttachmentRequest(IFormFile File, string ConversationId, string UserId);