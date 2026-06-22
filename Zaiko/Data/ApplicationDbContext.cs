using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Zaiko.Models;

namespace Zaiko.Data;

public class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IHttpContextAccessor httpContextAccessor)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Color> Colors => Set<Color>();
    public DbSet<Size> Sizes => Set<Size>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<SalesReport> SalesReports => Set<SalesReport>();
    public DbSet<ClientProduct> ClientProducts => Set<ClientProduct>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<SalesReport>()
            .HasIndex(sr => new { sr.ClientId, sr.ProductId, sr.YearMonth })
            .IsUnique();

        builder.Entity<ClientProduct>()
            .HasIndex(cp => new { cp.ClientId, cp.ProductId })
            .IsUnique();

        builder.Entity<Delivery>()
            .HasIndex(d => new { d.ClientId, d.ProductId, d.IsCarryOver, d.DeliveredAt });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var userName = httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "system";

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy = userName;
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = userName;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = userName;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
