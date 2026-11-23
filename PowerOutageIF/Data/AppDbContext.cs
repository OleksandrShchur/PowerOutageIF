using Microsoft.EntityFrameworkCore;
using PowerOutageIF.Models;

namespace PowerOutageIF.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<BotUser> Users => Set<BotUser>();
    public DbSet<QueueScheduleSnapshot> ScheduleSnapshots => Set<QueueScheduleSnapshot>();
    public DbSet<ScheduleChangeLog> ChangeLogs => Set<ScheduleChangeLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BotUser>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Username).HasMaxLength(128);
            entity.Property(e => e.FirstName).HasMaxLength(128);
            entity.Property(e => e.LastName).HasMaxLength(128);
            entity.Property(e => e.SubscribedQueue).HasMaxLength(16);
            entity.HasIndex(e => e.SubscribedQueue);
        });

        modelBuilder.Entity<QueueScheduleSnapshot>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.QueueCode).HasMaxLength(16).IsRequired();
            entity.Property(e => e.EventDate).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ScheduleApprovedSince).HasMaxLength(50);
            entity.Property(e => e.CreatedAtFromApi).HasMaxLength(50);
            entity.HasIndex(e => new { e.QueueCode, e.EventDate }).IsUnique();
        });

        modelBuilder.Entity<ScheduleChangeLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.QueueCode).HasMaxLength(16).IsRequired();
            entity.Property(e => e.EventDate).HasMaxLength(20).IsRequired();
            entity.Property(e => e.PreviousApprovedSince).HasMaxLength(50);
            entity.Property(e => e.NewApprovedSince).HasMaxLength(50);
        });
    }
}
