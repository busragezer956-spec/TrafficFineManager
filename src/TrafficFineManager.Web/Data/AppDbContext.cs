using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TrafficFineManager.Web.Models;

namespace TrafficFineManager.Web.Data;
public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<TrafficFine> TrafficFines => Set<TrafficFine>();
    public DbSet<ApprovalHistory> ApprovalHistories => Set<ApprovalHistory>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Vehicle>(e => {
            e.Property(x => x.Plate).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Plate).IsUnique();
            e.Property(x => x.Brand).HasMaxLength(60).IsRequired();
            e.Property(x => x.Model).HasMaxLength(60).IsRequired();
        });
        b.Entity<TrafficFine>(e => {
            e.Property(x => x.ReferenceNumber).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.ReferenceNumber).IsUnique();
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.Description).HasMaxLength(1000).IsRequired();
            e.Property(x => x.CreatedById).HasMaxLength(450).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne(x => x.Vehicle).WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });
        b.Entity<ApprovalHistory>(e => {
            e.Property(x => x.ActorId).HasMaxLength(450).IsRequired();
            e.Property(x => x.ActorName).HasMaxLength(256).IsRequired();
            e.Property(x => x.Note).HasMaxLength(1000);
            e.HasOne(x => x.TrafficFine).WithMany(x => x.History).HasForeignKey(x => x.TrafficFineId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
