using System.Collections.Concurrent;

namespace RmqSAF_RmqRAP_Rest.LoadTests.Infrastructure;

public sealed class CorrelationTracker
{
    private readonly ConcurrentBag<string> correlationIds = new();

    public void Track(string correlationId)
        => correlationIds.Add(correlationId);

    public IReadOnlyCollection<string> Snapshot()
        => correlationIds.ToArray();
}
