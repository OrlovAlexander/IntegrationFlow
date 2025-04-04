namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Logging
{
    /// <summary>
    /// Логирование данных
    /// </summary>
    public interface ILogging
    {
        /// <summary>
        /// Записать в лог передаваемые данные
        /// </summary>
        /// <param name="transmitData">Передаваемые данные</param>
        void LogTransmitData(TransmitData transmitData);

        ///// <summary>
        ///// Записать в лог полученные данные
        ///// </summary>
        ///// <param name="obtainedData">Полученные данные</param>
        //void LogObtainedData(object obtainedData);
    }
}
