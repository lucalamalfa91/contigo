namespace Contigo.SharedKernel;

/// <summary>
/// Production <see cref="IClock"/> backed by <see cref="DateTimeOffset.UtcNow"/>. Register it via
/// DI (<c>services.TryAddSingleton&lt;IClock&gt;(SystemClock.Instance)</c>) so application/domain
/// services that take <see cref="IClock"/> in their constructor (e.g.
/// <c>Contigo.Identity.Workspace.Infrastructure.WorkspaceMembershipService</c>) get real time at
/// runtime. Tests use a fixed/fake <see cref="IClock"/> instead, so time-dependent assertions stay
/// deterministic — every test project in this solution already follows that pattern with its own
/// local fake rather than this type.
/// </summary>
public sealed class SystemClock : IClock
{
    public static readonly SystemClock Instance = new();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
