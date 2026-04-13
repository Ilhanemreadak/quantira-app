using MediatR;
using Microsoft.Extensions.Logging;
using Quantira.Domain.Interfaces;

namespace Quantira.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that wraps commands implementing
/// <see cref="ITransactionalCommand"/> in a database transaction.
/// Guarantees atomicity for multi-step write operations — if any step
/// throws, the entire transaction is rolled back and no partial state
/// is persisted.
/// Read-only queries and commands that manage their own transactions
/// should NOT implement <see cref="ITransactionalCommand"/>.
/// The behavior delegates to <see cref="IUnitOfWork.SaveChangesAsync"/>
/// at the end of a successful handler execution, so individual handlers
/// must not call SaveChanges themselves when this behavior is active.
/// </summary>
/// <typeparam name="TRequest">The command type.</typeparam>
/// <typeparam name="TResponse">The handler response type.</typeparam>
public sealed class TransactionBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(
        IUnitOfWork unitOfWork,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ITransactionalCommand)
            return await next();

        var requestName = typeof(TRequest).Name;

        _logger.LogDebug("[TX BEGIN] {RequestName}", requestName);

        try
        {
            var response = await next();

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("[TX COMMIT] {RequestName}", requestName);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TX ROLLBACK] {RequestName}", requestName);
            throw;
        }
    }
}

/// <summary>
/// Marker interface for commands that require an explicit database transaction.
/// Apply to any command that performs multiple write operations that must
/// succeed or fail together (e.g. <c>AddTradeCommand</c> which writes both
/// a <c>Trade</c> record and updates a <c>Position</c>).
/// </summary>
public interface ITransactionalCommand { }