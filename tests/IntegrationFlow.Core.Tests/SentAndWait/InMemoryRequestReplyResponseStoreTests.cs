using System.Text;
using IntegrationFlow.Contexts.Integrations._00Samples.SentAndWait.ResponseCache;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.ResponseCache;
using Xunit;

namespace IntegrationFlow.Tests.SentAndWait;

public sealed class InMemoryRequestReplyResponseStoreTests
{
    [Fact]
    public async Task StoreResponse_ThenTryBegin_ReturnsAlreadyProcessed()
    {
        var store = new InMemoryRequestReplyResponseStore();
        var messageId = "msg-1";
        var body = Encoding.UTF8.GetBytes("""{"status":"ok"}""");

        Assert.Equal(RequestReplyCacheResult.Acquired, await store.TryBeginAsync(messageId));
        await store.StoreResponseAsync(messageId, body);

        Assert.Equal(RequestReplyCacheResult.AlreadyProcessed, await store.TryBeginAsync(messageId));
        var cached = await store.GetCachedResponseAsync(messageId);
        Assert.Equal(body, cached);
    }

    [Fact]
    public async Task TryBegin_WhileProcessing_ReturnsInProgress()
    {
        var store = new InMemoryRequestReplyResponseStore();
        var messageId = "msg-2";

        Assert.Equal(RequestReplyCacheResult.Acquired, await store.TryBeginAsync(messageId));
        Assert.Equal(RequestReplyCacheResult.InProgress, await store.TryBeginAsync(messageId));
    }
}
