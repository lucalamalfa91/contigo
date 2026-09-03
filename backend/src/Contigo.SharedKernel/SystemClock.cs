namespace Contigo.SharedKernel;

/// <summary>
/// Real-time <see cref="IClock"/> implementation backed by <see cref="DateTimeOffset.UtcNow"/>.
/// Production composition roots register this as the default <see cref="IClock"/>; tests
/// substitute a fixed/fake clock instead so "now"-derived assertions are deterministic (see
/// <see cref="IClock"/>'s own doc comment).
/// </summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
