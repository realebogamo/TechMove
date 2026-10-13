using GLMS.Core.Entities;
using GLMS.Shared.Dtos;

namespace GLMS.Core.Services;

/// <summary>
/// LINQ search/filter for contracts. Written against IQueryable so EF Core translates it to SQL,
/// and it can also be unit tested against an in-memory list.
/// </summary>
public static class ContractQueryExtensions
{
    public static IQueryable<Contract> ApplyFilter(this IQueryable<Contract> query, ContractFilterDto filter)
    {
        // Overlap test: a contract is in range if it ends on/after From and starts on/before To.
        if (filter.From.HasValue)
        {
            var from = filter.From.Value.Date;
            query = query.Where(c => c.EndDate >= from);
        }

        if (filter.To.HasValue)
        {
            var to = filter.To.Value.Date;
            query = query.Where(c => c.StartDate <= to);
        }

        if (filter.Status.HasValue)
            query = query.Where(c => c.Status == filter.Status.Value);

        if (filter.ClientId.HasValue)
            query = query.Where(c => c.ClientId == filter.ClientId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(c => c.Title.Contains(term) || (c.Client != null && c.Client.Name.Contains(term)));
        }

        return query.OrderBy(c => c.EndDate).ThenBy(c => c.Id);
    }
}
