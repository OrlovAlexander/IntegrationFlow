using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.InboxMessageProcessing;
using IntegrationFlow.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationFlow.Core.Tests.DependencyInjection;

public sealed class ReceiveAndProcessHostedServiceRegistrationTests
{
    [Fact]
    public void AddIntegrationFlowRabbitMqListener_WithDelegate_RegistersHostedService()
    {
        var services = new ServiceCollection();
        services.AddIntegrationFlow();

        var processed = false;
        services.AddIntegrationFlowRabbitMqListener("Inbox", _ => processed = true);

        var provider = services.BuildServiceProvider();
        var hostedServices = provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>().ToList();

        Assert.NotEmpty(hostedServices);
    }

    [Fact]
    public void AddIntegrationFlowRabbitMqListener_WithFactory_ResolvesHandlerFromDi()
    {
        var services = new ServiceCollection();
        services.AddIntegrationFlow();
        services.AddSingleton<TestInboxHandler>();

        services.AddIntegrationFlowRabbitMqListener("Inbox", sp => sp.GetRequiredService<TestInboxHandler>());

        var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<TestInboxHandler>();

        Assert.NotNull(handler);
    }

    private sealed class TestInboxHandler : IInboxMessageProcessing
    {
        public int Count { get; private set; }

        public void ProcessInboxMessage(InboxMessage inboxMessage) => Count++;
    }
}
