using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;
using IntegrationFlow.Contexts.Integrations._02Application;
using IntegrationFlow.Contexts.Integrations._03Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace IntegrationFlow.DependencyInjection;

/// <summary>
/// DI registration helpers for IntegrationFlow.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers core integration services.
    /// </summary>
    public static IServiceCollection AddIntegrationFlow(this IServiceCollection services)
    {
        LocalizationBootstrap.UseEmbeddedResources();

        services.TryAddSingleton<IOrgIntegration, OrgIntegration>();
        services.TryAddSingleton<IIntegrationLogger>(sp =>
        {
            var loggerFactory = sp.GetService<ILoggerFactory>();
            if (loggerFactory == null)
            {
                return NullIntegrationLogger.Instance;
            }

            return new MicrosoftExtensionsIntegrationLogger(
                loggerFactory.CreateLogger("IntegrationFlow"));
        });

        return services;
    }

    /// <summary>
    /// Registers a SentAndWait opposite-side provider.
    /// </summary>
    internal static IServiceCollection AddSentAndWaitProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, ISentAndWaitIntegrationOppositeSideProvider
    {
        services.AddSingleton<ISentAndWaitIntegrationOppositeSideProvider, TProvider>();
        services.AddSingleton<TProvider>();
        return services;
    }

    /// <summary>
    /// Registers a SentAndForgot opposite-side provider.
    /// </summary>
    internal static IServiceCollection AddSentAndForgotProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, ISentAndForgotIntegrationOppositeSideProvider
    {
        services.AddSingleton<ISentAndForgotIntegrationOppositeSideProvider, TProvider>();
        services.AddSingleton<TProvider>();
        return services;
    }
}
