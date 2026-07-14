using System.Collections.Concurrent;
using RmqSAF_RmqRAP_Rest.Contracts;

namespace Storage.Api.Storage;

public interface IPayloadStore
{
    Task StoreAsync(StoredPayload payload, CancellationToken cancellationToken);

    IReadOnlyList<StoredPayload> GetAll();

    IReadOnlyList<StoredPayload> GetByCorrelationId(string correlationId);

    StoredPayload? GetById(string id);
}

public sealed record StoredPayload(
    string Id,
    string CorrelationId,
    DateTimeOffset ReceivedAt,
    string Body,
    IReadOnlyDictionary<string, string> SourceHeaders);

public sealed class InMemoryPayloadStore : IPayloadStore
{
    private readonly ConcurrentDictionary<string, StoredPayload> store = new(StringComparer.OrdinalIgnoreCase);

    public Task StoreAsync(StoredPayload payload, CancellationToken cancellationToken)
    {
        store[payload.Id] = payload;
        return Task.CompletedTask;
    }

    public IReadOnlyList<StoredPayload> GetAll()
        => store.Values.OrderByDescending(x => x.ReceivedAt).ToList();

    public IReadOnlyList<StoredPayload> GetByCorrelationId(string correlationId)
        => store.Values
            .Where(x => string.Equals(x.CorrelationId, correlationId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.ReceivedAt)
            .ToList();

    public StoredPayload? GetById(string id)
        => store.TryGetValue(id, out var payload) ? payload : null;
}
