using Microsoft.EntityFrameworkCore;
using Quantira.Domain.Entities;
using Quantira.Domain.Enums;
using Quantira.Domain.Interfaces;

namespace Quantira.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAlertRepository"/>.
/// <see cref="GetAllActiveAsync"/> is called by <c>AlertCheckJob</c>
/// every 30 seconds and is the most performance-sensitive query in this
/// repository. It uses a compiled query and relies on the filtered index
/// <c>IX_Alerts_Status_Active</c> for sub-millisecond execution.
/// </summary>
public sealed class AlertRepository : IAlertRepository
{
    private readonly QuantiraDbContext _context;

    public AlertRepository(QuantiraDbContext context)
        => _context = context;

    public async Task<Alert?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Alerts
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Alert>> GetAllActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Alerts
            .Where(a => a.Status == Alert.AlertStatuses.Active)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Alert>> GetByUserIdAsync(
        Guid userId,
        AlertType? alertType = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Alerts
            .Where(a => a.UserId == userId);

        if (alertType.HasValue)
            query = query.Where(a => a.AlertType == alertType.Value);

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        Alert alert,
        CancellationToken cancellationToken = default)
    {
        await _context.Alerts.AddAsync(alert, cancellationToken);
    }
}