using Microsoft.EntityFrameworkCore;

namespace Logistics.Infrastructure.Read;

public sealed class ReadDbContext : DbContext
{
    public ReadDbContext(DbContextOptions<ReadDbContext> options)
        : base(options)
    {
        Database.EnsureCreated();
    }

    public DbSet<OrderDetailsView> Orders => Set<OrderDetailsView>();
    public DbSet<ProblematicOrder> ProblematicOrders => Set<ProblematicOrder>();
    public DbSet<ProjectionCheckpoint> ProjectionCheckpoints => Set<ProjectionCheckpoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderDetailsView>(e =>
        {
            e.ToTable("OrderDetailsView");
            e.HasKey(x => x.OrderId);
            e.Property(x => x.CustomerName).HasMaxLength(500);
            e.Property(x => x.Address).HasMaxLength(2000);
            e.Property(x => x.Status).HasMaxLength(64);
            e.Property(x => x.WarehouseId).HasMaxLength(128);
            e.Property(x => x.CourierName).HasMaxLength(256);
            e.Property(x => x.StatusHistoryJson).HasColumnType("jsonb");
        });

        modelBuilder.Entity<ProblematicOrder>(e =>
        {
            e.ToTable("ProblematicOrders");
            e.HasKey(x => x.OrderId);
            e.Property(x => x.CustomerName).HasMaxLength(500);
            e.Property(x => x.Address).HasMaxLength(2000);
            e.Property(x => x.Reason).HasMaxLength(2000);
        });

        modelBuilder.Entity<ProjectionCheckpoint>(e =>
        {
            e.ToTable("ProjectionCheckpoints");
            e.HasKey(x => x.ProjectionName);
            e.Property(x => x.ProjectionName).HasMaxLength(256);
        });
    }
}
