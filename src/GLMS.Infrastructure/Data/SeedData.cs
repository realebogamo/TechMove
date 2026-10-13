using GLMS.Core.Entities;
using GLMS.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace GLMS.Infrastructure.Data;

/// <summary>Demo data: one contract in each status so every workflow path can be shown.</summary>
internal static class SeedData
{
    private static readonly DateTime Created = new(2026, 1, 15, 8, 0, 0, DateTimeKind.Utc);

    public static void Apply(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>().HasData(
            new Client { Id = 1, Name = "Cape Freight Holdings", ContactPerson = "Thandi Nkosi", ContactEmail = "thandi.nkosi@capefreight.example", ContactPhone = "+27 21 555 0101", Region = "Africa" },
            new Client { Id = 2, Name = "Rotterdam Maritime BV", ContactPerson = "Pieter de Vries", ContactEmail = "p.devries@rotterdam-maritime.example", ContactPhone = "+31 10 555 0202", Region = "Europe" },
            new Client { Id = 3, Name = "Pacific Rim Logistics", ContactPerson = "Mei Lin", ContactEmail = "mei.lin@pacificrim.example", ContactPhone = "+65 6555 0303", Region = "Asia-Pacific" });

        modelBuilder.Entity<Contract>().HasData(
            new Contract { Id = 1, ClientId = 1, Title = "Durban port container handling", StartDate = new(2026, 1, 1), EndDate = new(2027, 12, 31), Status = ContractStatus.Active, ServiceLevel = ServiceLevel.Premium, SlaResponseHours = 4, CreatedAtUtc = Created },
            new Contract { Id = 2, ClientId = 2, Title = "EU cross-dock distribution", StartDate = new(2025, 6, 1), EndDate = new(2027, 5, 31), Status = ContractStatus.OnHold, ServiceLevel = ServiceLevel.Express, SlaResponseHours = 24, CreatedAtUtc = Created },
            new Contract { Id = 3, ClientId = 3, Title = "Singapore air freight 2025", StartDate = new(2025, 1, 1), EndDate = new(2025, 12, 31), Status = ContractStatus.Expired, ServiceLevel = ServiceLevel.Standard, SlaResponseHours = 72, CreatedAtUtc = Created },
            new Contract { Id = 4, ClientId = 1, Title = "Johannesburg warehousing renewal", StartDate = new(2027, 1, 1), EndDate = new(2028, 12, 31), Status = ContractStatus.Draft, ServiceLevel = ServiceLevel.Standard, SlaResponseHours = 72, CreatedAtUtc = Created });

        modelBuilder.Entity<ServiceRequest>().HasData(
            new ServiceRequest { Id = 1, ContractId = 1, Description = "Additional reefer container inspection", CostUsd = 1200m, ExchangeRate = 18.25m, CostZar = 21900m, Status = ServiceRequestStatus.Approved, CreatedAtUtc = Created });
    }
}
