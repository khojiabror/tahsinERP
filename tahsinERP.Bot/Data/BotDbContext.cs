using Microsoft.EntityFrameworkCore;

namespace tahsinERP.Bot.Data;

/// <summary>tahsin_bot bazasi. Sxema — Data/Schema.sql (idempotent, ishga tushishda qo'llanadi).</summary>
public sealed class BotDbContext(DbContextOptions<BotDbContext> options) : DbContext(options)
{
    public DbSet<BotTenant> Tenants => Set<BotTenant>();
    public DbSet<BotInboxItem> Inbox => Set<BotInboxItem>();
    public DbSet<BotOutboxItem> Outbox => Set<BotOutboxItem>();
    public DbSet<BotLink> Links => Set<BotLink>();
    public DbSet<BotSession> Sessions => Set<BotSession>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<BotTenant>(e =>
        {
            e.ToTable("BotTenants");
            e.Property(x => x.Slug).HasMaxLength(50);
            e.Property(x => x.ErpBaseUrl).HasMaxLength(300);
            e.Property(x => x.ErpTenant).HasMaxLength(50);
            e.Property(x => x.TokenEnc).HasMaxLength(500);
            e.Property(x => x.WebhookSecret).HasMaxLength(100);
            e.Property(x => x.BotUsername).HasMaxLength(100);
        });
        b.Entity<BotInboxItem>(e => e.ToTable("BotInbox"));
        b.Entity<BotOutboxItem>(e =>
        {
            e.ToTable("BotOutbox");
            e.Property(x => x.Method).HasMaxLength(50);
        });
        b.Entity<BotLink>(e =>
        {
            e.ToTable("BotLinks");
            e.HasKey(x => new { x.TenantId, x.TelegramUserId });
            e.Property(x => x.Role).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Language).HasMaxLength(5);
        });
        b.Entity<BotSession>(e =>
        {
            e.ToTable("BotSessions");
            e.HasKey(x => new { x.TenantId, x.TelegramUserId });
            e.Property(x => x.State).HasMaxLength(50);
        });
    }
}
