using Xunit;

namespace IntegrationFlow.IntegrationTests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class RabbitMqIntegrationCollection
{
    public const string Name = "RabbitMqIntegration";
}
