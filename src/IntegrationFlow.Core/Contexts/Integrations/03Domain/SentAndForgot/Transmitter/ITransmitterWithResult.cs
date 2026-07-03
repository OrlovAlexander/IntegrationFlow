namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Transmitter
{
    /// <summary>
    /// Способ обращения к противоположной стороне с возвратом результата передачи.
    /// </summary>
    public interface ITransmitterWithResult : ITransmitter
    {
        /// <summary>
        /// Обратиться к противоположной стороне интеграции и вернуть результат.
        /// </summary>
        /// <param name="transmitData">Передаваемые данные</param>
        TransmitResult TransmitWithResult(TransmitData transmitData);
    }
}
