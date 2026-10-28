namespace Chat.Api.Models;

public sealed class AttachmentDto
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = "";
    public string ContentType { get; init; } = "";
    public long Size { get; init; }
}

public sealed class MessageDto
{
    public Guid Id { get; init; }
    public Guid ConversationId { get; init; }
    public string SenderId { get; init; } = "";
    public string SenderName { get; init; } = "";
    public string Content { get; init; } = "";
    public DateTimeOffset SentAt { get; init; }
    public List<string> ReadBy { get; init; } = new();
    public List<AttachmentDto> Attachments { get; init; } = new();
}

public sealed class ConversationDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public string Type { get; init; } = "private";
    public int UnreadCount { get; init; }
    public string? LastMessage { get; init; }
    public DateTimeOffset? LastAt { get; init; }
    public int MemberCount { get; init; }
}

public sealed record SendChatMessage(Guid ConversationId, string Content, Guid[] AttachmentIds);
public sealed record AuthResult(string Token, string UserId, string DisplayName);
