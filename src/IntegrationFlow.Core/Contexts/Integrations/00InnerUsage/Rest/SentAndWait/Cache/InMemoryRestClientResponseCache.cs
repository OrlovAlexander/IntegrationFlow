using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.SentAndWait.Cache;

/// <summary>
/// In-memory REST client response cache for tests and non-critical flows.
/// </summary>
public sealed class InMemoryRestClientResponseCache : IRestClientResponseCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan entryTtl;

    public InMemoryRestClientResponseCache()
        : this(TimeSpan.FromMinutes(5))
    {
    }

    public InMemoryRestClientResponseCache(TimeSpan entryTtl)
    {
        if (entryTtl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(entryTtl));
        }

        this.entryTtl = entryTtl;
    }

    public Task<byte[]?> TryGetAsync(string profileName, string messageId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PurgeExpired();

        if (string.IsNullOrWhiteSpace(profileName) || string.IsNullOrWhiteSpace(messageId))
        {
            return Task.FromResult<byte[]?>(null);
        }

        if (entries.TryGetValue(BuildKey(profileName, messageId), out var entry) && !entry.IsExpired())
        {
            return Task.FromResult<byte[]?>(entry.Body);
        }

        return Task.FromResult<byte[]?>(null);
    }

    public Task StoreAsync(
        string profileName,
        string messageId,
        byte[] responseBody,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(profileName) || string.IsNullOrWhiteSpace(messageId) || responseBody == null)
        {
            return Task.CompletedTask;
        }

        entries[BuildKey(profileName, messageId)] = new CacheEntry(responseBody, DateTime.UtcNow.Add(entryTtl));
        return Task.CompletedTask;
    }

    private void PurgeExpired()
    {
        foreach (var pair in entries)
        {
            if (pair.Value.IsExpired())
            {
                entries.TryRemove(pair.Key, out _);
            }
        }
    }

    private static string BuildKey(string profileName, string messageId)
        => $"{profileName}|{messageId}";

    private sealed class CacheEntry
    {
        public CacheEntry(byte[] body, DateTime expiresAtUtc)
        {
            Body = body;
            ExpiresAtUtc = expiresAtUtc;
        }

        public byte[] Body { get; }

        public DateTime ExpiresAtUtc { get; }

        public bool IsExpired() => DateTime.UtcNow >= ExpiresAtUtc;
    }
}
