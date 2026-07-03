using System;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Publishers;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;

namespace IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess;

/// <summary>
/// Options for <see cref="ReceiveAndProcessHostedService"/>.
/// </summary>
internal sealed class ReceiveAndProcessHostedServiceOptions
{
    public RabbitMqConfiguration Configuration { get; set; } = null!;

    public Func<object, Task> ProcessMessageAsync { get; set; } = null!;

    internal static ReceiveAndProcessHostedServiceOptions CreateForProfile(
        string profileName,
        IIntegrationLogger logger)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("Profile name is required.", nameof(profileName));
        }

        var side = new NamedRabbitMqIntegrationPublisherSide(profileName);
        return Create(side, logger);
    }

    internal static ReceiveAndProcessHostedServiceOptions Create(
        IntegrationPublisherSideBase side,
        IIntegrationLogger logger)
    {
        if (side == null)
        {
            throw new ArgumentNullException(nameof(side));
        }

        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        var publisher = PublisherBase.Create<RabbitMqPublisher>(logger, side);
        var configuration = side.GetConfiguration(publisher, logger);

        if (configuration is not RabbitMqConfiguration rabbitMqConfiguration)
        {
            throw new InvalidOperationException(
                $"Expected {nameof(RabbitMqConfiguration)} for profile listener registration.");
        }

        var processor = side.GetProcessor(publisher, configuration, logger);

        return new ReceiveAndProcessHostedServiceOptions
        {
            Configuration = rabbitMqConfiguration,
            ProcessMessageAsync = message => processor.ProcessMessageAsync(message)
        };
    }
}
