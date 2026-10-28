using Chat.Api.Data;
using Chat.Api.Models;
using Chat.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Chat.Api.Hubs;

[Authorize]
public sealed class ChatHub(ChatDbContext db) : Hub
{
    private string UserId => Context.User!.Id();
    private string UserName => Context.User!.DisplayName();

    public static string Room(Guid id) => $"conversation:{id}";

    /// <summary>Every (re)connection re-joins all of the user's conversations, so reconnects just work.</summary>
    public override async Task OnConnectedAsync()
    {
        var ids = await db.ConversationMembers
            .Where(m => m.UserId == UserId)
            .Select(m => m.ConversationId)
            .ToListAsync();

        foreach (var id in ids)
            await Groups.AddToGroupAsync(Context.ConnectionId, Room(id));

        await base.OnConnectedAsync();
    }

    private async Task EnsureMember(Guid conversationId)
    {
        var ok = await db.ConversationMembers.AnyAsync(x =>
            x.ConversationId == conversationId && x.UserId == UserId);
        if (!ok) throw new HubException("You are not a member of this conversation.");
    }

    /// <summary>Used for conversations created/joined after the connection was opened.</summary>
    public async Task JoinConversation(Guid conversationId)
    {
        await EnsureMember(conversationId);
        await Groups.AddToGroupAsync(Context.ConnectionId, Room(conversationId));
    }

    public async Task SendMessage(SendChatMessage request)
    {
        var content = request.Content?.Trim() ?? "";
        var attachmentIds = (request.AttachmentIds ?? Array.Empty<Guid>()).Distinct().ToArray();

        if (content.Length > 4000) throw new HubException("Message is too long.");
        if (content.Length == 0 && attachmentIds.Length == 0)
            throw new HubException("Message cannot be empty.");

        await EnsureMember(request.ConversationId);

        // Only attachments uploaded by this user that are not yet attached to a message.
        var attachments = await db.Attachments
            .Where(a => attachmentIds.Contains(a.Id) && a.UploaderId == UserId && a.MessageId == null)
            .ToListAsync();

        if (attachments.Count != attachmentIds.Length)
            throw new HubException("Invalid attachment.");

        var message = new ChatMessage
        {
            ConversationId = request.ConversationId,
            SenderId = UserId,
            Content = content,
            SentAt = DateTimeOffset.UtcNow
        };
        foreach (var a in attachments) message.Attachments.Add(a);

        db.Messages.Add(message);
        await db.SaveChangesAsync();

        var dto = new MessageDto
        {
            Id = message.Id,
            ConversationId = message.ConversationId,
            SenderId = UserId,
            SenderName = UserName,
            Content = message.Content,
            SentAt = message.SentAt,
            Attachments = attachments.Select(a => new AttachmentDto
            {
                Id = a.Id, FileName = a.FileName, ContentType = a.ContentType, Size = a.Size
            }).ToList()
        };

        await Clients.Group(Room(request.ConversationId)).SendAsync("MessageReceived", dto);
    }

    public async Task SendTyping(Guid conversationId, bool isTyping)
    {
        await EnsureMember(conversationId);
        await Clients.OthersInGroup(Room(conversationId)).SendAsync("TypingChanged", new
        {
            conversationId,
            userId = UserId,
            userName = UserName,
            isTyping
        });
    }

    public async Task MarkRead(Guid conversationId)
    {
        await EnsureMember(conversationId);
        var now = DateTimeOffset.UtcNow;

        var member = await db.ConversationMembers.SingleAsync(x =>
            x.ConversationId == conversationId && x.UserId == UserId);
        member.LastReadAt = now;

        var unread = await db.Messages
            .Where(m => m.ConversationId == conversationId &&
                        m.SenderId != UserId &&
                        !m.Reads.Any(r => r.UserId == UserId))
            .ToListAsync();

        foreach (var m in unread)
            m.Reads.Add(new MessageRead { UserId = UserId, ReadAt = now });

        await db.SaveChangesAsync();

        if (unread.Count == 0) return;

        await Clients.Group(Room(conversationId)).SendAsync("ReadReceipt", new
        {
            conversationId,
            userId = UserId,
            messageIds = unread.Select(m => m.Id),
            readAt = now
        });
    }
}
