using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;

/// <summary>
/// Shared reconnect/backoff helpers for long-lived RabbitMQ consumer sessions.
/// </summary>
internal static class RabbitMqConsumerSessionLifecycle
{
    internal const int ReconnectMaxDelaySeconds = 30;

    internal static int GetReconnectDelaySeconds(int attempt)
        => Math.Min(ReconnectMaxDelaySeconds, Math.Max(1, (int)Math.Pow(2, Math.Min(attempt - 1, 5))));

    internal static async Task DelayReconnectAsync(int attempt, CancellationToken cancellationToken)
    {
        var delaySeconds = GetReconnectDelaySeconds(attempt);
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<bool> WaitForSessionEndAsync(
        IConnection connection,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<ShutdownEventArgs>? onShutdown = (_, _) => completion.TrySetResult(false);

        connection.ConnectionShutdown += onShutdown;

        var registration = cancellationToken.Register(() => completion.TrySetResult(true));

        try
        {
            return await completion.Task.ConfigureAwait(false);
        }
        finally
        {
            connection.ConnectionShutdown -= onShutdown;
            registration.Dispose();
        }
    }
}
