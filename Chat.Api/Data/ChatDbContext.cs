using Chat.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Chat.Api.Data;

public sealed class ChatDbContext(DbContextOptions<ChatDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMember> ConversationMembers => Set<ConversationMember>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();
    public DbSet<MessageRead> MessageReads => Set<MessageRead>();
    public DbSet<ChatAttachment> Attachments => Set<ChatAttachment>();

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<AppUser>(e =>
        {
            e.HasIndex(x => x.UserName).IsUnique();
            e.Property(x => x.UserName).HasMaxLength(50);
            e.Property(x => x.DisplayName).HasMaxLength(100);
        });

        m.Entity<Conversation>(e =>
        {
            e.HasIndex(x => x.PairKey).IsUnique();
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.PairKey).HasMaxLength(100);
        });

        m.Entity<ConversationMember>(e =>
        {
            e.HasKey(x => new { x.ConversationId, x.UserId });
            e.HasOne(x => x.Conversation).WithMany(x => x.Members)
                .HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        m.Entity<ChatMessage>(e =>
        {
            e.HasIndex(x => new { x.ConversationId, x.SentAt });
            e.Property(x => x.Content).HasMaxLength(4000);
            e.HasOne(x => x.Conversation).WithMany(x => x.Messages)
                .HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Sender).WithMany()
                .HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
        });

        m.Entity<MessageRead>(e =>
        {
            e.HasKey(x => new { x.MessageId, x.UserId });
            e.HasOne(x => x.Message).WithMany(x => x.Reads)
                .HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<ChatAttachment>(e =>
        {
            e.HasOne(x => x.Message).WithMany(x => x.Attachments)
                .HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.FileName).HasMaxLength(260);
            e.Property(x => x.StorageKey).HasMaxLength(64);
        });
    }
}
