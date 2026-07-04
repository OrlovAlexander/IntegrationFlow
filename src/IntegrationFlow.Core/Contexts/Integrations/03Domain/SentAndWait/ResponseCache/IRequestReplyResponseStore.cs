using System.Threading;
using System.Threading.Tasks;

namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.ResponseCache
{
    /// <summary>
    /// Хранилище кешированных RPC-ответов для идемпотентного server-side request-reply.
    /// </summary>
    public interface IRequestReplyResponseStore
    {
        /// <summary>
        /// Пытается начать обработку запроса с указанным MessageId.
        /// </summary>
        Task<RequestReplyCacheResult> TryBeginAsync(string messageId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Сохраняет ответ после успешной обработки.
        /// </summary>
        Task StoreResponseAsync(string messageId, byte[] responseBody, CancellationToken cancellationToken = default);

        /// <summary>
        /// Возвращает ранее сохранённый ответ или <c>null</c>.
        /// </summary>
        Task<byte[]?> GetCachedResponseAsync(string messageId, CancellationToken cancellationToken = default);
    }
}
