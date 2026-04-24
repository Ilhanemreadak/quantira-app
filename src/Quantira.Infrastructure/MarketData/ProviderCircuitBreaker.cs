using System.Collections.Concurrent;

namespace Quantira.Infrastructure.MarketData;

/// <summary>
/// Tracks consecutive 429 failures per market data provider.
/// Opens a cooldown window after the threshold is reached,
/// preventing further calls until the window expires.
/// Registered as singleton so state survives across scoped service lifetimes.
/// </summary>
public sealed class ProviderCircuitBreaker
{
    private const int FailureThreshold = 3;
    private static readonly TimeSpan CooldownDuration = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, ProviderCircuitState> _states =
        new(StringComparer.OrdinalIgnoreCase);

    public bool TryGetCooldownRemaining(string providerName, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;

        if (!_states.TryGetValue(providerName, out var state))
            return false;

        lock (state.SyncRoot)
        {
            if (state.CooldownUntilUtc is null)
                return false;

            var now = DateTimeOffset.UtcNow;

            if (state.CooldownUntilUtc <= now)
            {
                state.CooldownUntilUtc = null;
                return false;
            }

            remaining = state.CooldownUntilUtc.Value - now;
            return true;
        }
    }

    public ProviderCircuitOutcome RegisterFailure(string providerName)
    {
        var state = _states.GetOrAdd(providerName, _ => new ProviderCircuitState());

        lock (state.SyncRoot)
        {
            state.Consecutive429Failures++;

            if (state.Consecutive429Failures < FailureThreshold)
            {
                return new ProviderCircuitOutcome(
                    CircuitOpened: false,
                    Consecutive429Count: state.Consecutive429Failures,
                    CooldownUntilUtc: null);
            }

            var cooldownUntil = DateTimeOffset.UtcNow.Add(CooldownDuration);
            state.Consecutive429Failures = 0;
            state.CooldownUntilUtc = cooldownUntil;

            return new ProviderCircuitOutcome(
                CircuitOpened: true,
                Consecutive429Count: FailureThreshold,
                CooldownUntilUtc: cooldownUntil);
        }
    }

    public void ResetFailures(string providerName)
    {
        if (!_states.TryGetValue(providerName, out var state))
            return;

        lock (state.SyncRoot)
        {
            state.Consecutive429Failures = 0;

            if (state.CooldownUntilUtc <= DateTimeOffset.UtcNow)
                state.CooldownUntilUtc = null;
        }
    }

    private sealed class ProviderCircuitState
    {
        public object SyncRoot { get; } = new();
        public int Consecutive429Failures { get; set; }
        public DateTimeOffset? CooldownUntilUtc { get; set; }
    }
}

public sealed record ProviderCircuitOutcome(
    bool CircuitOpened,
    int Consecutive429Count,
    DateTimeOffset? CooldownUntilUtc);
