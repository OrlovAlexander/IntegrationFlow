using System;
using IntegrationFlow.Contexts.Integrations._03Domain;

namespace IntegrationFlow.Contexts.Integrations._01Infrastructure;

/// <summary>
/// No-op logger used when no logging provider is configured.
/// </summary>
public sealed class NullIntegrationLogger : IIntegrationLogger
{
    public static NullIntegrationLogger Instance { get; } = new();

    private NullIntegrationLogger()
    {
    }

    public void LogException(string message, Exception ex)
    {
    }

    public void LogWarn(string message)
    {
    }

    public void Log(string message)
    {
    }

    public void Log(string format, params object[] args)
    {
    }

    public void LogInfo(string message)
    {
    }
}
