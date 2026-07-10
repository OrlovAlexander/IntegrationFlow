#if NET8_0_OR_GREATER
using System;
using System.IO;
using System.Linq;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Health;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.SentAndWait.Cache;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace IntegrationFlow.DependencyInjection;

public static partial class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers REST HTTP transport and configuration overlay from host <see cref="IConfiguration"/>.
    /// </summary>
    public static IServiceCollection AddIntegrationFlowRest(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (services == null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        RestConfigurationComposition.OverlayConfiguration = configuration;
        services.TryAddSingleton<IRestClientResponseCache, InMemoryRestClientResponseCache>();
        services.TryAddSingleton<RestTransportHealthRegistry>();
        services.AddHttpClient();

        RegisterRestHttpClients(services);

        services.AddSingleton(sp =>
        {
            RestHttpClientProvider.Initialize(sp.GetRequiredService<IHttpClientFactory>());
            RestClientResponseCacheRegistry.Initialize(sp.GetService<IRestClientResponseCache>());
            sp.GetRequiredService<RestTransportHealthRegistry>()
                .SetMonitoredProfiles(LoadRestProfilesForHealth());
            return sp.GetRequiredService<IHttpClientFactory>();
        });

        return services;
    }

    /// <summary>
    /// Registers REST request-reply health checks.
    /// </summary>
    public static IHealthChecksBuilder AddIntegrationFlowRestHealthChecks(
        this IServiceCollection services,
        Action<RestHealthCheckOptions>? configure = null)
    {
        services.AddOptions<RestHealthCheckOptions>();
        if (configure != null)
        {
            services.Configure(configure);
        }

        services.TryAddSingleton<RestTransportHealthRegistry>();

        return services
            .AddHealthChecks()
            .AddCheck<RestHealthCheck>(
                "integrationflow.rest.requestreply",
                tags: new[] { "integrationflow", "rest", "ready" });
    }

    private static void RegisterRestHttpClients(IServiceCollection services)
    {
        try
        {
            foreach (var profile in RestRequestReplyConfigurationLoader.LoadAll())
            {
                RegisterRestHttpClient(services, profile.Name, profile);
            }

            foreach (var profile in RestPublishConfigurationLoader.LoadAll())
            {
                RegisterRestHttpClient(services, profile.Name, profile);
            }
        }
        catch (FileNotFoundException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void RegisterRestHttpClient(
        IServiceCollection services,
        string profileName,
        IRestConnectionConfiguration profile)
    {
        var clientName = RestHttpClientProvider.BuildClientName(profileName);
        services.AddHttpClient(clientName, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => RestHttpClientHandlerFactory.CreateHandler(profile));
    }

    private static RestRequestReplyConfiguration[] LoadRestProfilesForHealth()
    {
        try
        {
            return RestRequestReplyConfigurationLoader.LoadAll().ToArray();
        }
        catch (FileNotFoundException)
        {
            return Array.Empty<RestRequestReplyConfiguration>();
        }
        catch (InvalidOperationException)
        {
            return Array.Empty<RestRequestReplyConfiguration>();
        }
    }
}
#endif
