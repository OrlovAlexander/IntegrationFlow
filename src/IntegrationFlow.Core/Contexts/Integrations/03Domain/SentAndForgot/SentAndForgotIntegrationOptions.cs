namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot
{
    /// <summary>
    /// Глобальные опции интеграции SentAndForgot.
    /// </summary>
    public static class SentAndForgotIntegrationOptions
    {
        /// <summary>
        /// Бросать исключение из <see cref="SentAndForgotIntegration.Integrate"/> при неуспешном результате.
        /// По умолчанию <c>false</c> для обратной совместимости.
        /// </summary>
        public static bool ThrowOnFailure { get; set; }
    }
}
