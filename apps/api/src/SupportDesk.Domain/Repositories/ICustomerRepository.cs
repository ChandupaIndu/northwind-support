using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Access to <see cref="Customer"/> aggregates.
/// </summary>
public interface ICustomerRepository
{
    Task<bool> ExistsAsync(int id, CancellationToken ct);
    Task<SupportDesk.Domain.Aggregates.Customers.Customer?> GetByIdAsync(int id, CancellationToken ct);
}
