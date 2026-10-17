using SupportDesk.Domain.Aggregates.Categories;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Access to <see cref="Category"/> aggregates.
/// </summary>
public interface ICategoryRepository
{
    Task<bool> ExistsAsync(int id, CancellationToken ct);
    Task<SupportDesk.Domain.Aggregates.Categories.Category?> GetByIdAsync(int id, CancellationToken ct);
}
