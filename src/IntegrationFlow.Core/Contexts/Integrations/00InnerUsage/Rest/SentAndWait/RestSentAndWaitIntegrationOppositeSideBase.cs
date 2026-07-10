using System;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.SentAndWait.Transmitters;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Cfg;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Connection;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Formatter;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Logging;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Transmitter;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Validator;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.SentAndWait;

/// <summary>
/// Base opposite side for SentAndWait over REST HTTP.
/// </summary>
internal abstract class RestSentAndWaitIntegrationOppositeSideBase : SentAndWaitIntegrationOppositeSide
{
    /// <summary>
    /// Profile name in rest.json (RestRequestReply section).
    /// </summary>
    protected abstract string ConfigurationName { get; }

    public override IFormatterTransmitData GetFormatterSourceData(IIntegrationLogger logger) => null!;

    public override IConfiguration GetTransmitterConfiguration(IIntegrationLogger logger)
        => RestRequestReplyConfigurationLoader.LoadProfile(ConfigurationName);

    public override IConnection GetConnection(IConfiguration configuration, IIntegrationLogger logger)
        => new RestHttpConnection((RestRequestReplyConfiguration)configuration);

    public override ITransmitter GetTransmitter(IConfiguration configuration, IConnection connection, IIntegrationLogger logger)
        => new RestHttpTransmitter(configuration, (RestHttpConnection)connection);

    public override IValidator GetValidator(IConfiguration configuration, IIntegrationLogger logger) => null!;

    public override IFormatterObtainedData GetFormatterObtainedData(IIntegrationLogger logger) => null!;

    public override ILogging GetLogging(IIntegrationLogger logger) => null!;
}

/// <summary>
/// Named REST SentAndWait opposite side.
/// </summary>
internal sealed class NamedRestSentAndWaitIntegrationOppositeSide : RestSentAndWaitIntegrationOppositeSideBase
{
    private readonly string configurationName;

    public NamedRestSentAndWaitIntegrationOppositeSide(string configurationName)
    {
        if (string.IsNullOrWhiteSpace(configurationName))
        {
            throw new ArgumentException("REST request-reply profile name is required.", nameof(configurationName));
        }

        this.configurationName = configurationName;
    }

    protected override string ConfigurationName => configurationName;

    protected override object GetIntegrationOppositeSideCode() => configurationName;
}
