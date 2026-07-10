using System;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Connections;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Cfg;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Connection;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Formatter;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Logging;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Transmitter;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.SentAndForgot;

/// <summary>
/// Base opposite side for SentAndForgot over REST HTTP publish.
/// </summary>
internal abstract class RestSentAndForgotIntegrationOppositeSideBase : SentAndForgotIntegrationOppositeSide
{
    protected abstract string ConfigurationName { get; }

    public override IFormatterTransmitData GetFormatterSourceData(IIntegrationLogger logger) => null!;

    public override IConfiguration GetTransmitterConfiguration(IIntegrationLogger logger)
        => RestPublishConfigurationLoader.LoadProfile(ConfigurationName);

    public override IConnection GetConnection(IConfiguration configuration, IIntegrationLogger logger)
        => new RestPublishConnection((RestPublishConfiguration)configuration);

    public override ITransmitter GetTransmitter(IConfiguration configuration, IConnection connection, IIntegrationLogger logger)
        => new Transmitters.RestPublishTransmitter(
            (RestPublishConfiguration)configuration,
            (RestPublishConnection)connection);

    public override ILogging GetLogging(IIntegrationLogger logger) => null!;
}

/// <summary>
/// Named REST SentAndForgot opposite side.
/// </summary>
internal sealed class NamedRestSentAndForgotIntegrationOppositeSide : RestSentAndForgotIntegrationOppositeSideBase
{
    private readonly string configurationName;

    public NamedRestSentAndForgotIntegrationOppositeSide(string configurationName)
    {
        if (string.IsNullOrWhiteSpace(configurationName))
        {
            throw new ArgumentException("REST publish profile name is required.", nameof(configurationName));
        }

        this.configurationName = configurationName;
    }

    protected override string ConfigurationName => configurationName;

    protected override object GetIntegrationOppositeSideCode() => configurationName;
}
