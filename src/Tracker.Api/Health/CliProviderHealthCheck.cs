using Microsoft.Extensions.Diagnostics.HealthChecks;
using Tracker.AI.Cli;

namespace Tracker.Api.Health;

public sealed class CliProviderHealthCheck : IHealthCheck
{
    private readonly IEnumerable<ICliProviderAdapter> _adapters;

    public CliProviderHealthCheck(IEnumerable<ICliProviderAdapter> adapters)
    {
        _adapters = adapters;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var availabilities = _adapters
            .Select(x => x.GetAvailability())
            .OrderBy(x => x.Provider, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var data = availabilities.ToDictionary(
            x => x.Provider,
            x => (object)new
            {
                x.Enabled,
                x.Available,
                x.Command,
                x.ResolvedCommandPath,
                x.Message
            });

        if (availabilities.Any(x => x.Enabled && x.Available))
        {
            return Task.FromResult(new HealthCheckResult(
                HealthStatus.Healthy,
                "At least one CLI provider is available.",
                null,
                data));
        }

        if (availabilities.Any(x => x.Enabled))
        {
            return Task.FromResult(new HealthCheckResult(
                HealthStatus.Degraded,
                "No enabled CLI providers are currently available.",
                null,
                data));
        }

        return Task.FromResult(new HealthCheckResult(
            HealthStatus.Unhealthy,
            "No CLI providers are enabled.",
            null,
            data));
    }
}
