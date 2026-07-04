using System.Threading;
using System.Threading.Tasks;

namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait
{
    /// <summary>
    /// Асинхронный обработчик результата интеграции SentAndWait.
    /// </summary>
    public abstract class AsyncSentAndWaitIntegrationResultHandler
    {
        /// <summary>
        /// Обработать успешный результат интеграции.
        /// </summary>
        public abstract Task ProcessResultAsync(ObtainedData obtainedData, CancellationToken cancellationToken);

        /// <summary>
        /// Обработать результат интеграции, не прошедший проверку.
        /// </summary>
        public abstract Task ProcessFailedResultAsync(ObtainedData obtainedData, CancellationToken cancellationToken);
    }
}
