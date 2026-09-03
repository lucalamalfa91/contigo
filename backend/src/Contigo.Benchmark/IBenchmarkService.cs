namespace Contigo.Benchmark;

/// <summary>
/// Benchmark Service interface consumed by domain modules (Renewals, Savings, Quotes).
/// Domain modules depend on this abstraction; the implementation behind it
/// owns provider adapters (fixture adapter for first demo, per brief section 3).
/// </summary>
public interface IBenchmarkService
{
    // R0 placeholder — concrete operations (query, refresh, compare)
    // will be added as domain features land.
}
