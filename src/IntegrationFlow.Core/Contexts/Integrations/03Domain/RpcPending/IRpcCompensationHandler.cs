using System.Threading;
using System.Threading.Tasks;

namespace IntegrationFlow.Contexts.Integrations._03Domain.RpcPending
{
    /// <summary>
    /// Обработчик компенсации для terminal async RPC pending (Failed / TimedOut).
    /// </summary>
    public interface IRpcCompensationHandler
    {
        /// <summary>
        /// Attempts compensation for the given pending request.
        /// </summary>
        /// <returns>True when compensation was staged successfully.</returns>
        Task<bool> TryCompensateAsync(RpcPendingRequest request, CancellationToken cancellationToken = default);
    }
}
