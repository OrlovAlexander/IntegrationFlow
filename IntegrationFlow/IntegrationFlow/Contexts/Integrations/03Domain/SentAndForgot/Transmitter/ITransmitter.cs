namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Transmitter
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
        void Transmit(TransmitData transmitData);
    }
}
