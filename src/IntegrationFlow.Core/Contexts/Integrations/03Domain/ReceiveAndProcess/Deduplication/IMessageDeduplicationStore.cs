using System.Threading;
using System.Threading.Tasks;

namespace IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Deduplication
{
    /// <summary>
    /// Хранилище для идемпотентной обработки входящих сообщений.
    /// </summary>
    public interface IMessageDeduplicationStore
    {
        Task<bool> TryBeginProcessingAsync(string messageId, CancellationToken cancellationToken = default);

        Task MarkProcessedAsync(string messageId, CancellationToken cancellationToken = default);
    }
}
