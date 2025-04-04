namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Transmitter
{
    /// <summary>
    /// Способ обращения к противоположной стороне интеграции
    /// </summary>
    public interface ITransmitter
    {
        /// <summary>
        /// Обратиться к противоположной стороне интеграции
        /// </summary>
        /// <param name="transmitData">Передаваемые данные</param>
        /// <returns>Результат интеграции</returns>
        ObtainedData Transmit(TransmitData transmitData);
    }
}
