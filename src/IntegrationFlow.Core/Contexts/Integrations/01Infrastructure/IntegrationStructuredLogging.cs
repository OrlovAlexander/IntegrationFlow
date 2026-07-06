using System;
using System.Collections.Generic;
using IntegrationFlow.Contexts.Integrations._03Domain;

namespace IntegrationFlow.Contexts.Integrations._01Infrastructure;

/// <summary>
/// Helpers for attaching structured fields to integration logs via scopes.
/// </summary>
public static class IntegrationStructuredLogging
{
    public static IDisposable BeginScope(
        IIntegrationLogger logger,
        params (string Key, object? Value)[] fields)
    {
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        if (fields.Length == 0)
        {
            return NullScope.Instance;
        }

        var state = new Dictionary<string, object?>(fields.Length);
        foreach (var (key, value) in fields)
        {
            if (string.IsNullOrWhiteSpace(key) || value == null)
            {
                continue;
            }

            if (value is string text && string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            state[key] = value;
        }

        if (state.Count == 0)
        {
            return NullScope.Instance;
        }

        return BeginScope(logger, state);
    }

    public static IDisposable BeginScope(
        IIntegrationLogger logger,
        IReadOnlyDictionary<string, object?> state)
    {
        if (logger == null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        if (state == null || state.Count == 0)
        {
            return NullScope.Instance;
        }

        if (logger is IScopedIntegrationLogger scopedLogger)
        {
            return scopedLogger.BeginScope(state);
        }

        return NullScope.Instance;
    }

    public static IDisposable BeginOutcomeScope(IIntegrationLogger logger, string outcome)
        => BeginScope(logger, (IntegrationStructuredLogFields.Outcome, outcome));

    private sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}
