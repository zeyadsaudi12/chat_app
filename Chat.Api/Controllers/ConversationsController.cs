using Chat.Api.Data;
using Chat.Api.Hubs;
using Chat.Api.Models;
using Chat.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Chat.Api.Controllers;

public sealed record CreatePrivateRequest(string UserId);
public sealed record CreateGroupRequest(string Title, string[] MemberIds);
public sealed record AddMemberRequest(string UserId);

[ApiController]
[Authorize]
[Route("api/conversations")]
public sealed class ConversationsController(ChatDbContext db, IHubContext<ChatHub> hub) : ControllerBase
{
    private string UserId => User.Id();

    private IQueryable<ConversationDto> ConversationQuery(string uid) =>
        db.Conversations
            .Where(c => c.Members.Any(m => m.UserId == uid))
            .Select(c => new ConversationDto
            {
                Id = c.Id,
                Title = c.Type == ConversationType.Private
                    ? (c.Members.Where(m => m.UserId != uid).Select(m => m.User.DisplayName).FirstOrDefault() ?? "Private chat")
                    : c.Title,
                Type = c.Type == ConversationType.Group ? "group" : "private",
                UnreadCount = c.Messages.Count(x => x.SenderId != uid && !x.Reads.Any(r => r.UserId == uid)),
                LastMessage = c.Messages.OrderByDescending(x => x.SentAt).Select(x => x.Content).FirstOrDefault(),
                LastAt = c.Messages.Max(x => (DateTimeOffset?)x.SentAt),
                MemberCount = c.Members.Count
            });

    /// <summary>Pushes each member their own view of the conversation (title/unread differ per user).</summary>
    private async Task NotifyMembers(Guid conversationId)
    {
        var memberIds = await db.ConversationMembers
            .Where(m => m.ConversationId == conversationId)
            .Select(m => m.UserId).ToListAsync();

        foreach (var uid in memberIds)
        {
            var dto = await ConversationQuery(uid).FirstAsync(c => c.Id == conversationId);
            await hub.Clients.User(uid).SendAsync("ConversationAdded", dto);
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetConversations()
    {
        var list = await ConversationQuery(UserId).ToListAsync();
        return Ok(list.OrderByDescending(c => c.LastAt ?? DateTimeOffset.MinValue));
    }

    [HttpGet("{id:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid id, [FromQuery] int take = 50, [FromQuery] DateTimeOffset? before = null)
    {
        if (!await db.ConversationMembers.AnyAsync(x => x.ConversationId == id && x.UserId == UserId))
            return Forbid();

        take = Math.Clamp(take, 1, 100);
        var query = db.Messages.Where(m => m.ConversationId == id);
        if (before.HasValue) query = query.Where(m => m.SentAt < before.Value);

        var messages = await query
            .OrderByDescending(m => m.SentAt)
            .Take(take)
            .Select(m => new MessageDto
            {
                Id = m.Id,
                ConversationId = m.ConversationId,
                SenderId = m.SenderId,
                SenderName = m.Sender.DisplayName,
                Content = m.Content,
                SentAt = m.SentAt,
                ReadBy = m.Reads.Select(r => r.UserId).ToList(),
                Attachments = m.Attachments.Select(a => new AttachmentDto
                {
                    Id = a.Id, FileName = a.FileName, ContentType = a.ContentType, Size = a.Size
                }).ToList()
            })
            .ToListAsync();

        return Ok(messages.OrderBy(m => m.SentAt));
    }

    [HttpPost("private")]
    public async Task<IActionResult> CreatePrivate(CreatePrivateRequest r)
    {
        var me = UserId;
        if (r.UserId == me) return BadRequest("You cannot chat with yourself.");
        if (!await db.Users.AnyAsync(u => u.Id == r.UserId)) return NotFound("User not found.");

        var pairKey = string.CompareOrdinal(me, r.UserId) < 0 ? $"{me}:{r.UserId}" : $"{r.UserId}:{me}";
        var existing = await db.Conversations.FirstOrDefaultAsync(c => c.PairKey == pairKey);

        if (existing is null)
        {
            existing = new Conversation { Type = ConversationType.Private, PairKey = pairKey };
            existing.Members.Add(new ConversationMember { UserId = me });
            existing.Members.Add(new ConversationMember { UserId = r.UserId });
            db.Conversations.Add(existing);

            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException) // lost a race with a concurrent create; reuse the winner
            {
                db.ChangeTracker.Clear();
                existing = await db.Conversations.FirstAsync(c => c.PairKey == pairKey);
            }
            await NotifyMembers(existing.Id);
        }

        return Ok(await ConversationQuery(me).FirstAsync(c => c.Id == existing.Id));
    }

    [HttpPost("group")]
    public async Task<IActionResult> CreateGroup(CreateGroupRequest r)
    {
        var title = r.Title?.Trim() ?? "";
        if (title.Length is < 1 or > 200) return BadRequest("Group title is required.");

        var ids = (r.MemberIds ?? Array.Empty<string>()).Append(UserId).Distinct().ToList();
        if (ids.Count < 2) return BadRequest("Add at least one other member.");

        var found = await db.Users.CountAsync(u => ids.Contains(u.Id));
        if (found != ids.Count) return BadRequest("One or more users do not exist.");

        var conv = new Conversation { Type = ConversationType.Group, Title = title };
        foreach (var id in ids) conv.Members.Add(new ConversationMember { UserId = id });
        db.Conversations.Add(conv);
        await db.SaveChangesAsync();

        await NotifyMembers(conv.Id);
        return Ok(await ConversationQuery(UserId).FirstAsync(c => c.Id == conv.Id));
    }

    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMember(Guid id, AddMemberRequest r)
    {
        var conv = await db.Conversations.Include(c => c.Members).FirstOrDefaultAsync(c => c.Id == id);
        if (conv is null) return NotFound();
        if (conv.Type != ConversationType.Group) return BadRequest("Only group chats accept new members.");
        if (conv.Members.All(m => m.UserId != UserId)) return Forbid();
        if (conv.Members.Any(m => m.UserId == r.UserId)) return Conflict("Already a member.");
        if (!await db.Users.AnyAsync(u => u.Id == r.UserId)) return NotFound("User not found.");

        conv.Members.Add(new ConversationMember { UserId = r.UserId });
        await db.SaveChangesAsync();

        await NotifyMembers(id);
        return NoContent();
    }
}
