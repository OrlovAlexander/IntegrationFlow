using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication;
using IntegrationFlow.EntityFrameworkCore.Deduplication;
using IntegrationFlow.EntityFrameworkCore.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IntegrationFlow.EntityFrameworkCore.DependencyInjection;

/// <summary>
/// Регистрация EF Core store-реализаций IntegrationFlow.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Регистрирует relay store (factory-based) для outbox worker.
    /// </summary>
    public static IServiceCollection AddIntegrationFlowEfOutboxRelayStore<TContext>(
        this IServiceCollection services)
        where TContext : DbContext
    {
        services.TryAddSingleton<IOutboxStore, EfOutboxStore<TContext>>();
        return services;
    }

    /// <summary>
    /// Регистрирует scoped staging outbox для записи в TX приложения.
    /// </summary>
    public static IServiceCollection AddIntegrationFlowEfOutboxEnqueue<TContext>(
        this IServiceCollection services)
        where TContext : DbContext
    {
        services.TryAddScoped<IOutboxEnqueue, EfOutboxEnqueue<TContext>>();
        return services;
    }

    /// <summary>
    /// Регистрирует relay store и scoped enqueue.
    /// </summary>
    public static IServiceCollection AddIntegrationFlowEfOutbox<TContext>(
        this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddIntegrationFlowEfOutboxRelayStore<TContext>();
        services.AddIntegrationFlowEfOutboxEnqueue<TContext>();
        return services;
    }

    public static IServiceCollection AddIntegrationFlowEfDeduplication<TContext>(
        this IServiceCollection services,
        Action<MessageDeduplicationOptions>? configure = null)
        where TContext : DbContext
    {
        var options = new MessageDeduplicationOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);
        services.TryAddScoped<IMessageDeduplicationStore, EfMessageDeduplicationStore<TContext>>();
        return services;
    }
}
