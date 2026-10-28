namespace Chat.Api.Models;

public enum ConversationType { Private, Group }

public sealed class AppUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}

public sealed class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public ConversationType Type { get; set; }
    /// <summary>Unique "userA:userB" key (sorted) so a pair of users has exactly one private chat.</summary>
    public string? PairKey { get; set; }
    public ICollection<ConversationMember> Members { get; set; } = new List<ConversationMember>();
    public ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

public sealed class ConversationMember
{
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public string UserId { get; set; } = "";
    public AppUser User { get; set; } = null!;
    public DateTimeOffset? LastReadAt { get; set; }
}

public sealed class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;
    public string SenderId { get; set; } = "";
    public AppUser Sender { get; set; } = null!;
    public string Content { get; set; } = "";
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<MessageRead> Reads { get; set; } = new List<MessageRead>();
    public ICollection<ChatAttachment> Attachments { get; set; } = new List<ChatAttachment>();
}

public sealed class MessageRead
{
    public Guid MessageId { get; set; }
    public ChatMessage Message { get; set; } = null!;
    public string UserId { get; set; } = "";
    public DateTimeOffset ReadAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ChatAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? MessageId { get; set; }          // null until attached to a message
    public ChatMessage? Message { get; set; }
    public string UploaderId { get; set; } = "";
    public string FileName { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
}
