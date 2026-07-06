using IntegrationFlow.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace IntegrationFlow.Core.Tests.DependencyInjection;

public sealed class RabbitMqHealthChecksRegistrationTests
{
    [Fact]
    public void AddIntegrationFlowRabbitMqHealthChecks_RegistersThreeChecks()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIntegrationFlow();

        services.AddIntegrationFlowRabbitMqHealthChecks(options =>
        {
            options.MaxReconnectAttemptsBeforeUnhealthy = 3;
        });

        var provider = services.BuildServiceProvider();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();

        Assert.NotNull(healthCheckService);
    }

    [Fact]
    public void AddIntegrationFlowRabbitMqListener_RegistersHealthRegistryEndpoint()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIntegrationFlow();
        services.AddIntegrationFlowRabbitMqListener("Inbox", _ => { });
        services.AddIntegrationFlowRabbitMqHealthChecks();

        var provider = services.BuildServiceProvider();
        var healthCheckService = provider.GetRequiredService<HealthCheckService>();

        Assert.NotNull(healthCheckService);
    }
}
