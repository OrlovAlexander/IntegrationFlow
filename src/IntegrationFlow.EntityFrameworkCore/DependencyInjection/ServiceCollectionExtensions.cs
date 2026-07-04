using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.ResponseCache;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;
using IntegrationFlow.EntityFrameworkCore.Deduplication;
using IntegrationFlow.EntityFrameworkCore.Outbox;
using IntegrationFlow.EntityFrameworkCore.ResponseCache;
using IntegrationFlow.EntityFrameworkCore.RpcPending;
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

    /// <summary>
    /// Регистрирует EF store кеша RPC-ответов для идемпотентного server-side request-reply.
    /// </summary>
    public static IServiceCollection AddIntegrationFlowEfRequestReplyResponseCache<TContext>(
        this IServiceCollection services,
        Action<RequestReplyResponseCacheOptions>? configure = null)
        where TContext : DbContext
    {
        var options = new RequestReplyResponseCacheOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);
        services.TryAddScoped<IRequestReplyResponseStore, EfRequestReplyResponseStore<TContext>>();
        return services;
    }

    /// <summary>
    /// Регистрирует EF store async RPC pending (relay + enqueue).
    /// </summary>
    public static IServiceCollection AddIntegrationFlowEfRpcPending<TContext>(
        this IServiceCollection services)
        where TContext : DbContext
    {
        services.TryAddSingleton<IRpcPendingStore, EfRpcPendingStore<TContext>>();
        services.TryAddScoped<IRpcPendingEnqueue, EfRpcPendingEnqueue<TContext>>();
        return services;
    }
}
