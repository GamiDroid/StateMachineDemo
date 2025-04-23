using Microsoft.EntityFrameworkCore;
using StateMachineDemo.Infrastructure.Persistance.Tables;

namespace StateMachineDemo.Infrastructure.Persistance;

public class AppDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<ChocoReworkStation> ChocoReworkStations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChocoReworkStation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("choco_rework_station");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnType("int(11)")
                .HasColumnName("id");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'no_order'")
                .HasColumnType("enum('no_order','wait_pallet','scan_pallet','scan_tank','empty_bigbag','choose_tank')")
                .HasColumnName("status");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .HasDefaultValueSql("''")
                .HasColumnName("type");
            entity.Property(e => e.AxItemRequestId)
                .HasColumnType("int(11)")
                .HasColumnName("ax_item_request_id");
            entity.Property(e => e.ChocoProductionId)
                .HasColumnType("int(11)")
                .HasColumnName("choco_production_id");
            entity.Property(e => e.Component)
                .HasMaxLength(255)
                .HasColumnName("component");
        });
    }
}
