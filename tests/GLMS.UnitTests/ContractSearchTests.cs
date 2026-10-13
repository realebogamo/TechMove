using GLMS.Core.Entities;
using GLMS.Core.Services;
using GLMS.Infrastructure.Data;
using GLMS.Infrastructure.Repositories;
using GLMS.Shared.Dtos;
using GLMS.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace GLMS.UnitTests;

public class ContractSearchTests
{
    private static readonly Client Acme = new() { Id = 1, Name = "Acme Shipping" };
    private static readonly Client Blue = new() { Id = 2, Name = "Blue Ocean" };

    private static List<Contract> Contracts() => new()
    {
        new() { Id = 1, Title = "Q1 freight", ClientId = 1, Client = Acme, StartDate = new(2026, 1, 1), EndDate = new(2026, 3, 31), Status = ContractStatus.Expired },
        new() { Id = 2, Title = "Annual warehousing", ClientId = 1, Client = Acme, StartDate = new(2026, 1, 1), EndDate = new(2026, 12, 31), Status = ContractStatus.Active },
        new() { Id = 3, Title = "Port handling", ClientId = 2, Client = Blue, StartDate = new(2026, 6, 1), EndDate = new(2027, 5, 31), Status = ContractStatus.OnHold },
        new() { Id = 4, Title = "Future lane", ClientId = 2, Client = Blue, StartDate = new(2027, 1, 1), EndDate = new(2027, 12, 31), Status = ContractStatus.Draft }
    };

    private static int[] Search(ContractFilterDto filter) =>
        Contracts().AsQueryable().ApplyFilter(filter).Select(c => c.Id).ToArray();

    [Fact]
    public void NoFilter_ReturnsAll_OrderedByEndDate()
    {
        Assert.Equal(new[] { 1, 2, 3, 4 }, Search(new ContractFilterDto()));
    }

    [Fact]
    public void DateRange_ReturnsContractsWhoseTermOverlapsTheRange()
    {
        // April-May 2026: #1 ended in March, #3 starts in June, #4 starts in 2027.
        Assert.Equal(new[] { 2 }, Search(new ContractFilterDto { From = new(2026, 4, 1), To = new(2026, 5, 31) }));
    }

    [Fact]
    public void DateRange_IncludesContractsTouchingTheBoundary()
    {
        Assert.Equal(new[] { 1, 2 }, Search(new ContractFilterDto { From = new(2026, 3, 31), To = new(2026, 3, 31) }));
    }

    [Fact]
    public void Status_FiltersByStatus()
    {
        Assert.Equal(new[] { 3 }, Search(new ContractFilterDto { Status = ContractStatus.OnHold }));
    }

    [Fact]
    public void DateRangeAndStatus_Combine()
    {
        Assert.Equal(new[] { 2 }, Search(new ContractFilterDto { From = new(2026, 1, 1), To = new(2026, 12, 31), Status = ContractStatus.Active }));
    }

    [Fact]
    public void TextSearch_MatchesTitleOrClientName()
    {
        Assert.Equal(new[] { 3, 4 }, Search(new ContractFilterDto { Search = "Blue" }));
        Assert.Equal(new[] { 2 }, Search(new ContractFilterDto { Search = "warehousing" }));
    }

    [Fact]
    public async Task Repository_SearchAsync_RunsFilterThroughEfCore()
    {
        var options = new DbContextOptionsBuilder<GlmsDbContext>()
            .UseInMemoryDatabase($"search-{Guid.NewGuid()}")
            .Options;
        await using (var seed = new GlmsDbContext(options))
        {
            await seed.Database.EnsureCreatedAsync(); // includes the seeded demo data
            seed.ServiceRequests.RemoveRange(seed.ServiceRequests);
            seed.Contracts.RemoveRange(seed.Contracts);
            seed.Clients.RemoveRange(seed.Clients);
            await seed.SaveChangesAsync();

            seed.Clients.AddRange(new Client { Id = 1, Name = "Acme Shipping" }, new Client { Id = 2, Name = "Blue Ocean" });
            seed.Contracts.AddRange(Contracts().Select(c => { c.Client = null; return c; }));
            await seed.SaveChangesAsync();
        }

        await using var db = new GlmsDbContext(options);
        var repository = new ContractRepository(db);

        var results = await repository.SearchAsync(new ContractFilterDto { Status = ContractStatus.Active });

        var single = Assert.Single(results);
        Assert.Equal("Acme Shipping", single.Client!.Name);
    }
}
