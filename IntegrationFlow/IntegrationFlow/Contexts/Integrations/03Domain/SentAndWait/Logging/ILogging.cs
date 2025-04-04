namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Logging
{
    /// <summary>
    /// Логгерирование данных
    /// </summary>
    public interface ILogging
    {
        /// <summary>
        /// Записать в лог передаваемые данные
        /// </summary>
        /// <param name="transmitData">Передаваемые данные</param>
        void LogTransmitData(TransmitData transmitData);

        /// <summary>
        /// Записать в лог полученные данные
        /// </summary>
        /// <param name="obtainedData">Полученные данные до форматирования</param>
        void LogObtainedData(ObtainedData obtainedData);

        /// <summary>
        /// Записать в лог данные после форматирования
        /// </summary>
        /// <param name="obtainedData">Полученные данные после форматирования</param>
        void LogIntegrationResultData(ObtainedData obtainedData);
    }
}