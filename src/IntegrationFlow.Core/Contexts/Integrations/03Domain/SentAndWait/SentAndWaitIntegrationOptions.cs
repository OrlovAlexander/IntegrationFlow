namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait
{
    /// <summary>
    /// Глобальные опции интеграции SentAndWait.
    /// </summary>
    public static class SentAndWaitIntegrationOptions
    {
        /// <summary>
        /// Бросать исключение из <see cref="SentAndWaitIntegration.Integrate"/> при ошибке транспорта.
        /// По умолчанию <c>false</c> для обратной совместимости.
        /// </summary>
        public static bool ThrowOnFailure { get; set; }
    }
}
