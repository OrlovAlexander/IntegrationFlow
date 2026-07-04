using System.Threading;
using System.Threading.Tasks;

namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Transmitter
{
    /// <summary>
    /// Асинхронный способ обращения к противоположной стороне интеграции.
    /// </summary>
    public interface ITransmitterAsync
    {
        /// <summary>
        /// Асинхронно обратиться к противоположной стороне интеграции.
        /// </summary>
        Task<ObtainedData> TransmitAsync(TransmitData transmitData, CancellationToken cancellationToken);
    }
}
