using System.Threading;
using System.Threading.Tasks;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Workers;

/// <summary>
/// Tracks in-flight message handlers for graceful listener shutdown.
/// </summary>
internal sealed class RabbitMqListenerInFlightTracker
{
    private int count;

    public int Count => Volatile.Read(ref count);

    public void Increment() => Interlocked.Increment(ref count);

    public void Decrement() => Interlocked.Decrement(ref count);

    public async Task WaitForZeroAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (Count == 0)
        {
            return;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        while (Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await Task.Delay(50, timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
