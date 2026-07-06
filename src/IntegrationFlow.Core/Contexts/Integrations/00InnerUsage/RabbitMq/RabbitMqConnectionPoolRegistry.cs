using System;
using System.Collections.Generic;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;

/// <summary>
/// Registers pool dispose actions for application shutdown.
/// </summary>
internal static class RabbitMqConnectionPoolRegistry
{
    private static readonly object Sync = new();
    private static readonly List<Action> DisposeActions = new();

    internal static void Register(Action disposeAll)
    {
        if (disposeAll == null)
        {
            throw new ArgumentNullException(nameof(disposeAll));
        }

        lock (Sync)
        {
            DisposeActions.Add(disposeAll);
        }
    }

    internal static void DisposeAll()
    {
        lock (Sync)
        {
            for (var index = DisposeActions.Count - 1; index >= 0; index--)
            {
                try
                {
                    DisposeActions[index]();
                }
                catch
                {
                }
            }

            DisposeActions.Clear();
        }
    }
}
