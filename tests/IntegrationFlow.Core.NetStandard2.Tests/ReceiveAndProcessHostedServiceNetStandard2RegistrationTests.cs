using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.InboxMessageProcessing;
using IntegrationFlow.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace IntegrationFlow.NetStandard2.Tests;

/// <summary>
/// Validates listener registration against the netstandard2.0 build of IntegrationFlow.Core.
/// </summary>
public sealed class ReceiveAndProcessHostedServiceNetStandard2RegistrationTests
{
    [Fact]
    public void AddIntegrationFlowRabbitMqListener_Twice_RegistersTwoHostedServices()
    {
        var services = new ServiceCollection();
        services.AddIntegrationFlow();
        var baselineHostedServices = services.Count(
            descriptor => descriptor.ServiceType == typeof(IHostedService));

        services.AddIntegrationFlowRabbitMqListener("InboxA", _ => { });
        services.AddIntegrationFlowRabbitMqListener("InboxB", _ => { });

        var hostedServiceRegistrations = services.Count(
            descriptor => descriptor.ServiceType == typeof(IHostedService));

        Assert.Equal(0, baselineHostedServices);
        Assert.Equal(2, hostedServiceRegistrations - baselineHostedServices);
    }

    [Fact]
    public void AddIntegrationFlowRabbitMqListener_WithDelegate_RegistersHostedService()
    {
        var services = new ServiceCollection();
        services.AddIntegrationFlow();

        services.AddIntegrationFlowRabbitMqListener("Inbox", _ => { });

        var provider = services.BuildServiceProvider();
        var hostedServices = provider.GetServices<IHostedService>().ToList();

        Assert.Single(hostedServices);
        Assert.IsAssignableFrom<BackgroundService>(hostedServices[0]);
    }

    [Fact]
    public void AddIntegrationFlowRabbitMqListener_WithFactory_ResolvesHandlerFromDi()
    {
        var services = new ServiceCollection();
        services.AddIntegrationFlow();
        services.AddSingleton<TestInboxHandler>();

        services.AddIntegrationFlowRabbitMqListener("Inbox", sp => sp.GetRequiredService<TestInboxHandler>());

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<TestInboxHandler>());
        Assert.Single(provider.GetServices<IHostedService>());
    }

    private sealed class TestInboxHandler : IInboxMessageProcessing
    {
        public int Count { get; private set; }

        public void ProcessInboxMessage(InboxMessage inboxMessage) => Count++;
    }
}
