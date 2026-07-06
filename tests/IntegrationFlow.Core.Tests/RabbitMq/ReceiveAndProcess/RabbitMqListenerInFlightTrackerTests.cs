using System;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Workers;
using Xunit;

namespace IntegrationFlow.Tests.RabbitMq.ReceiveAndProcess;

public sealed class RabbitMqListenerInFlightTrackerTests
{
    [Fact]
    public async Task WaitForZeroAsync_CompletesWhenCountReturnsToZero()
    {
        var tracker = new RabbitMqListenerInFlightTracker();
        tracker.Increment();

        var waitTask = tracker.WaitForZeroAsync(TimeSpan.FromSeconds(1), CancellationToken.None);

        await Task.Delay(50);
        tracker.Decrement();

        await waitTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(0, tracker.Count);
    }

    [Fact]
    public async Task WaitForZeroAsync_ReturnsAfterTimeoutWhenCountRemainsPositive()
    {
        var tracker = new RabbitMqListenerInFlightTracker();
        tracker.Increment();

        await tracker.WaitForZeroAsync(TimeSpan.FromMilliseconds(100), CancellationToken.None);

        Assert.Equal(1, tracker.Count);
    }
}
