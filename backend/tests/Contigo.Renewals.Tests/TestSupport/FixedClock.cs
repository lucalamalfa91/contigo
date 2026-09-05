using Contigo.SharedKernel;

namespace Contigo.Renewals.Tests.TestSupport;

/// <summary>
/// Fixed <see cref="IClock"/> for deterministic
/// <see cref="Contigo.Renewals.Application.RenewalEngine"/> "days until" assertions.
/// <c>SystemClock</c>'s own doc comment notes "every test project in this solution already
/// follows that pattern with its own local fake rather than [SystemClock]" — this is this
/// project's copy of that pattern (mirrors <c>Contigo.Chat.Tests.TestSupport.FixedClock</c> /
/// <c>Contigo.AiGateway.Tests.TestSupport.FixedClock</c>).
/// </summary>
public sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; } = now;
}
