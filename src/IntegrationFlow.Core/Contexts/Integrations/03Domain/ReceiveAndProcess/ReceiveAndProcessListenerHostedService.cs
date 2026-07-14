using System;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Health;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Workers;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.InboxMessageProcessing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;

/// <summary>
/// Defers listener dependency resolution until background execution to avoid DI deadlocks during host startup.
/// </summary>
internal sealed class ReceiveAndProcessListenerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly string profileName;
    private readonly Func<IServiceProvider, IInboxMessageProcessing> createProcessing;
    private readonly Func<IServiceProvider, IMessageDeduplicationStore>? createDeduplicationStore;
    private readonly Func<IServiceProvider, ReceiveAndProcessHostedServiceOptions>? createRequestReplyOptions;
    private readonly RabbitMqListenerWorker worker = new();

    public ReceiveAndProcessListenerHostedService(
        IServiceScopeFactory scopeFactory,
        string profileName,
        Func<IServiceProvider, IInboxMessageProcessing> createProcessing,
        Func<IServiceProvider, IMessageDeduplicationStore>? createDeduplicationStore = null)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.profileName = profileName ?? throw new ArgumentNullException(nameof(profileName));
        this.createProcessing = createProcessing ?? throw new ArgumentNullException(nameof(createProcessing));
        this.createDeduplicationStore = createDeduplicationStore;
    }

    private ReceiveAndProcessListenerHostedService(
        IServiceScopeFactory scopeFactory,
        Func<IServiceProvider, ReceiveAndProcessHostedServiceOptions> createRequestReplyOptions)
    {
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.createRequestReplyOptions = createRequestReplyOptions
            ?? throw new ArgumentNullException(nameof(createRequestReplyOptions));
        this.profileName = string.Empty;
        this.createProcessing = _ => throw new InvalidOperationException("Request-reply listener does not use inbox processing factory.");
    }

    public static ReceiveAndProcessListenerHostedService ForRequestReply(
        IServiceScopeFactory scopeFactory,
        Func<IServiceProvider, ReceiveAndProcessHostedServiceOptions> createOptions)
        => new(scopeFactory, createOptions);

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Factory.StartNew(
            () => ExecuteAsync(cancellationToken),
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        using var scope = scopeFactory.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        ReceiveAndProcessHostedServiceOptions options;
        if (createRequestReplyOptions != null)
        {
            options = createRequestReplyOptions(scopedProvider);
        }
        else
        {
            var logger = scopedProvider.GetRequiredService<IIntegrationLogger>();
            var metrics = scopedProvider.GetService<IIntegrationFlowMetrics>();
            var healthRegistry = scopedProvider.GetService<RabbitMqTransportHealthRegistry>();
            healthRegistry?.Register(RabbitMqTransportKind.Listener, profileName);

            var processing = createProcessing(scopedProvider);
            var deduplicationStore = createDeduplicationStore?.Invoke(scopedProvider);
            options = ReceiveAndProcessHostedServiceOptions.CreateForProfile(
                profileName,
                logger,
                processing,
                deduplicationStore,
                metrics);
        }

        var integrationLogger = scopedProvider.GetRequiredService<IIntegrationLogger>();
        var transportHealthRegistry = scopedProvider.GetService<RabbitMqTransportHealthRegistry>();

        await worker.RunAsync(
            options.Configuration,
            options.ProcessMessageAsync,
            integrationLogger,
            stoppingToken,
            metrics: options.Metrics,
            healthRegistry: transportHealthRegistry);
    }
}
