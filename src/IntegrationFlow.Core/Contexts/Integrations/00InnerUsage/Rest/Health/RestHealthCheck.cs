#if NET8_0_OR_GREATER
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Connections;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Health;

internal sealed class RestHealthCheck : IHealthCheck
{
    private readonly RestTransportHealthRegistry registry;
    private readonly RestHealthCheckOptions options;

    public RestHealthCheck(RestTransportHealthRegistry registry, IOptions<RestHealthCheckOptions> options)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var profiles = registry.GetMonitoredProfiles();
        if (profiles.Count == 0)
        {
            return HealthCheckResult.Healthy("No REST health check endpoints configured.");
        }

        var failures = 0;
        foreach (var profile in profiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var healthy = await PingProfileAsync(profile, cancellationToken).ConfigureAwait(false);
            if (!healthy)
            {
                failures++;
                registry.ReportFailure(profile.Name);
            }
            else
            {
                registry.ReportSuccess(profile.Name);
            }
        }

        var consecutiveFailures = registry.GetMaxConsecutiveFailures();
        if (consecutiveFailures >= options.MaxConsecutiveFailures)
        {
            return HealthCheckResult.Unhealthy(
                $"REST health check failed for {failures} profile(s). Consecutive failures: {consecutiveFailures}.");
        }

        return failures > 0
            ? HealthCheckResult.Degraded($"REST health check failed for {failures} profile(s).")
            : HealthCheckResult.Healthy("REST request-reply endpoints are reachable.");
    }

    private static async Task<bool> PingProfileAsync(
        RestRequestReplyConfiguration profile,
        CancellationToken cancellationToken)
    {
        var healthUri = profile.BuildHealthCheckUri();
        if (healthUri == null)
        {
            return true;
        }

        using var client = RestHttpClientProvider.CreateStandaloneClient(profile);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, profile.HealthCheckTimeoutSeconds)));

        try
        {
            using var response = await client
                .GetAsync(healthUri, HttpCompletionOption.ResponseContentRead, timeoutCts.Token)
                .ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

internal sealed class RestTransportHealthRegistry
{
    private readonly object sync = new();
    private readonly System.Collections.Generic.Dictionary<string, int> consecutiveFailures =
        new(StringComparer.OrdinalIgnoreCase);

    private System.Collections.Generic.IReadOnlyList<RestRequestReplyConfiguration> monitoredProfiles =
        Array.Empty<RestRequestReplyConfiguration>();

    public void SetMonitoredProfiles(System.Collections.Generic.IReadOnlyList<RestRequestReplyConfiguration> profiles)
    {
        monitoredProfiles = profiles?
            .Where(profile => profile.BuildHealthCheckUri() != null)
            .ToArray() ?? Array.Empty<RestRequestReplyConfiguration>();
    }

    public System.Collections.Generic.IReadOnlyList<RestRequestReplyConfiguration> GetMonitoredProfiles()
        => monitoredProfiles;

    public void ReportFailure(string profileName)
    {
        lock (sync)
        {
            consecutiveFailures.TryGetValue(profileName, out var count);
            consecutiveFailures[profileName] = count + 1;
        }
    }

    public void ReportSuccess(string profileName)
    {
        lock (sync)
        {
            consecutiveFailures[profileName] = 0;
        }
    }

    public int GetMaxConsecutiveFailures()
    {
        lock (sync)
        {
            return consecutiveFailures.Count == 0 ? 0 : consecutiveFailures.Values.Max();
        }
    }
}

public sealed class RestHealthCheckOptions
{
    public int MaxConsecutiveFailures { get; set; } = 3;
}
#endif
