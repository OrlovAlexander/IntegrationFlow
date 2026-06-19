using System;
using IntegrationFlow.Contexts.Integrations._03Domain;
using Microsoft.Extensions.Logging;

namespace IntegrationFlow.Contexts.Integrations._01Infrastructure;

/// <summary>
/// Adapts <see cref="ILogger"/> to <see cref="IIntegrationLogger"/>.
/// </summary>
public sealed class MicrosoftExtensionsIntegrationLogger : IIntegrationLogger
{
    private readonly ILogger logger;

    public MicrosoftExtensionsIntegrationLogger(ILogger logger)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void LogException(string message, Exception ex) =>
        logger.LogError(ex, "{Message}", message);

    public void LogWarn(string message) =>
        logger.LogWarning("{Message}", message);

    public void Log(string message) =>
        logger.LogDebug("{Message}", message);

    public void Log(string format, params object[] args) =>
        logger.LogDebug(format, args);

    public void LogInfo(string message) =>
        logger.LogInformation("{Message}", message);
}
