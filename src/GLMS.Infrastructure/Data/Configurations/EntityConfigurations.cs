using GLMS.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GLMS.Infrastructure.Data.Configurations;

// Fluent API constraints. Enums are stored as strings so the database stays readable for reporting.

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> b)
    {
        b.ToTable("Clients");
        b.HasKey(c => c.Id);
        b.Property(c => c.Name).IsRequired().HasMaxLength(150);
        b.Property(c => c.ContactPerson).IsRequired().HasMaxLength(100);
        b.Property(c => c.ContactEmail).IsRequired().HasMaxLength(200);
        b.Property(c => c.ContactPhone).IsRequired().HasMaxLength(30);
        b.Property(c => c.Region).IsRequired().HasMaxLength(100);
        b.HasIndex(c => c.Name);
    }
}

public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> b)
    {
        b.ToTable("Contracts", t =>
            t.HasCheckConstraint("CK_Contracts_EndAfterStart", "[EndDate] > [StartDate]"));
        b.HasKey(c => c.Id);
        b.Property(c => c.Title).IsRequired().HasMaxLength(200);
        b.Property(c => c.StartDate).HasColumnType("date");
        b.Property(c => c.EndDate).HasColumnType("date");
        b.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(c => c.ServiceLevel).HasConversion<string>().HasMaxLength(20);
        b.Property(c => c.SignedAgreementStoredName).HasMaxLength(64);
        b.Property(c => c.SignedAgreementOriginalName).HasMaxLength(255);

        b.HasOne(c => c.Client)
            .WithMany(cl => cl.Contracts)
            .HasForeignKey(c => c.ClientId)
            .OnDelete(DeleteBehavior.Restrict); // never silently delete a client's legal agreements

        // Supports the date-range + status search and the nightly expiry scan.
        b.HasIndex(c => new { c.Status, c.EndDate });
        b.HasIndex(c => c.StartDate);
    }
}

public class ServiceRequestConfiguration : IEntityTypeConfiguration<ServiceRequest>
{
    public void Configure(EntityTypeBuilder<ServiceRequest> b)
    {
        b.ToTable("ServiceRequests", t =>
        {
            t.HasCheckConstraint("CK_ServiceRequests_CostUsd", "[CostUsd] > 0");
            t.HasCheckConstraint("CK_ServiceRequests_ExchangeRate", "[ExchangeRate] > 0");
        });
        b.HasKey(r => r.Id);
        b.Property(r => r.Description).IsRequired().HasMaxLength(1000);
        b.Property(r => r.CostUsd).HasPrecision(18, 2);
        b.Property(r => r.CostZar).HasPrecision(18, 2);
        b.Property(r => r.ExchangeRate).HasPrecision(18, 6);
        b.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);

        b.HasOne(r => r.Contract)
            .WithMany(c => c.ServiceRequests)
            .HasForeignKey(r => r.ContractId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.ToTable("Users");
        b.HasKey(u => u.Id);
        b.Property(u => u.Username).IsRequired().HasMaxLength(100);
        b.HasIndex(u => u.Username).IsUnique();
        b.Property(u => u.PasswordHash).IsRequired().HasMaxLength(256);
        b.Property(u => u.Role).IsRequired().HasMaxLength(20);
    }
}

public class ContractStatusHistoryConfiguration : IEntityTypeConfiguration<ContractStatusHistory>
{
    public void Configure(EntityTypeBuilder<ContractStatusHistory> b)
    {
        b.ToTable("ContractStatusHistory");
        b.HasKey(h => h.Id);
        b.Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(20);
        b.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(20);
        b.Property(h => h.Reason).IsRequired().HasMaxLength(200);

        b.HasOne<Contract>()
            .WithMany(c => c.StatusHistory)
            .HasForeignKey(h => h.ContractId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
