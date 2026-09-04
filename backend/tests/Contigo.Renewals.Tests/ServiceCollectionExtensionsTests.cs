using Contigo.Renewals.Application;
using Contigo.Renewals.Infrastructure;
using Contigo.Renewals.Tests.TestSupport;
using Contigo.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Contigo.Renewals.Tests;

/// <summary>
/// Proves task E03/F01/US01/T01's own wiring claim (mirrors
/// <c>Contigo.Chat.Tests.ServiceCollectionExtensionsTests</c>): <see cref="RenewalEngine"/> is
/// resolvable from a container that only has <see cref="ServiceCollectionExtensions.AddRenewalsModule"/>
/// registered — no external dependency needed, unlike the Chat module's own equivalent test — the
/// shape a future host wiring (<c>Contigo.Api.Program</c> / <c>Contigo.Worker.Program</c>) will
/// rely on once a task adds the first real caller. Task E03/F01/US01/T02 (renewal-opportunity)
/// extends this same proof to <see cref="RenewalOpportunityGenerator"/>, which depends on
/// <see cref="RenewalEngine"/> — resolving it from the same container proves the constructor
/// dependency is satisfied by this one <c>AddRenewalsModule</c> call, not by some other module's
/// registration.
/// </summary>
public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddRenewalsModule_resolves_RenewalEngine_with_no_captive_dependency()
    {
        var services = new ServiceCollection();

        services.AddRenewalsModule();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RenewalEngine>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IClock>());
    }

    [Fact]
    public void AddRenewalsModule_resolves_RenewalOpportunityGenerator_with_no_captive_dependency()
    {
        var services = new ServiceCollection();

        services.AddRenewalsModule();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RenewalOpportunityGenerator>());
    }

    [Fact]
    public void AddRenewalsModule_does_not_override_an_already_registered_IClock()
    {
        var services = new ServiceCollection();
        var preRegisteredClock = new FixedClock(new DateTimeOffset(2026, 9, 4, 0, 0, 0, TimeSpan.Zero));
        services.AddSingleton<IClock>(preRegisteredClock);

        services.AddRenewalsModule();

        using var provider = services.BuildServiceProvider();

        // TryAddSingleton: the first registration wins — same defensive convention every other
        // module's own AddXxxModule already uses.
        Assert.Same(preRegisteredClock, provider.GetRequiredService<IClock>());
    }
}
