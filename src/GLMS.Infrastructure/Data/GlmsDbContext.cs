using GLMS.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace GLMS.Infrastructure.Data;

public class GlmsDbContext : DbContext
{
    public GlmsDbContext(DbContextOptions<GlmsDbContext> options) : base(options)
    {
    }

    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<ContractStatusHistory> ContractStatusHistory => Set<ContractStatusHistory>();
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GlmsDbContext).Assembly);
        SeedData.Apply(modelBuilder);
    }
}
