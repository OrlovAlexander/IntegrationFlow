namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait
{
    /// <summary>
    /// Обработчик результата интеграции
    /// </summary>
    public abstract class SentAndWaitIntegrationResultHandler
    {
        /// <summary>
        /// Обработать результат интеграции
        /// </summary>
        /// <param name="obtainedData">Результат интеграции</param>
        public abstract void ProcessResult(ObtainedData obtainedData);

        /// <summary>
        /// Обработать результат интеграции не прошедший проверку
        /// </summary>
        /// <param name="obtainedData">Результат интеграции</param>
        public abstract void ProcessFailedResult(ObtainedData obtainedData);
    }
}
