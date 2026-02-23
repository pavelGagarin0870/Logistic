using Microsoft.EntityFrameworkCore;

namespace Logistics.Infrastructure.Write;

public sealed class EventStoreDbContext : DbContext
{
    public EventStoreDbContext(DbContextOptions<EventStoreDbContext> options)
        : base(options)
    {
        Database.EnsureCreated();
    }

    public DbSet<EventRecord> Events => Set<EventRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EventRecord>(e =>
        {
            e.ToTable("Events");
            e.HasKey(x => x.GlobalSequence);
            e.Property(x => x.GlobalSequence)
                .ValueGeneratedOnAdd();
            e.HasIndex(x => x.AggregateId);
            e.HasIndex(x => x.GlobalSequence);
            e.HasIndex(x => new { x.AggregateId, x.Version }).IsUnique();
            e.Property(x => x.EventType).HasMaxLength(512);
            e.Property(x => x.Data).HasColumnType("jsonb");
        });
    }
}
