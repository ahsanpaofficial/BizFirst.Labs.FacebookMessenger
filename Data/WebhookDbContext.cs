using Microsoft.EntityFrameworkCore;

namespace BizFirstMetaMessenger.Data;

/// <summary>
/// Database context for webhook events and messages
/// </summary>
public class WebhookDbContext : DbContext
{
    public WebhookDbContext(DbContextOptions<WebhookDbContext> options) : base(options)
    {
    }

    public DbSet<WebhookEvent> WebhookEvents { get; set; } = null!;
    public DbSet<Message> Messages { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure WebhookEvent
        modelBuilder.Entity<WebhookEvent>(entity =>
        {
            entity.ToTable("WebhookEvents");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.ReceivedAt);
            entity.HasIndex(e => e.IsProcessed);

            // One WebhookEvent can have many Messages
            entity.HasMany(e => e.Messages)
                .WithOne(m => m.WebhookEvent)
                .HasForeignKey(m => m.WebhookEventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Message
        modelBuilder.Entity<Message>(entity =>
        {
            entity.ToTable("Messages");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.SenderId);
            entity.HasIndex(e => e.RecipientId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.MessageType);
            entity.HasIndex(e => e.IsResponded);
            entity.HasIndex(e => e.MessageId);
        });
    }
}
