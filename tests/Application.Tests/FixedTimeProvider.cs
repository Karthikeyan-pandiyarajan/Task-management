namespace Application.Tests;

// Deterministic clock so date rules don't depend on when the tests run.
internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
