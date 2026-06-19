using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait;

namespace IntegrationFlow.Contexts.Integrations._02Application
{
    /// <summary>
    /// Точка входа в интеграцию из пользовательского расширения или чего угодно
    /// </summary>
    internal interface IOrgIntegration
    {
        /// <summary>
        /// Создать интеграцию
        /// </summary>
        /// <typeparam name="TOppositeSideProvider">Провайдер противоположной стороны интеграции</typeparam>
        /// <param name="oppositeSideCode">Код противоположной стороны интеграции</param>
        /// <param name="srcData">Исходные данные</param>
        /// <returns>Интеграция</returns>
        SentAndWaitIntegration CreateSentAndWaitIntegration<TOppositeSideProvider>(object oppositeSideCode, object srcData)
            where TOppositeSideProvider : ISentAndWaitIntegrationOppositeSideProvider, new();

        /// <summary>
        /// Возвращает обработчик результата запроса
        /// </summary>
        /// <typeparam name="TOppositeSideProvider">Провайдер противоположной стороны интеграции</typeparam>
        /// <param name="oppositeSideCode">Код противоположной стороны интеграции</param>
        SentAndWaitIntegrationResultHandler GetSentAndWaitResultHandler<TOppositeSideProvider>(object oppositeSideCode)
            where TOppositeSideProvider : ISentAndWaitIntegrationOppositeSideProvider, new();

        /// <summary>
        /// Создать интеграцию
        /// </summary>
        /// <typeparam name="TOppositeSideProvider">Провайдер противоположной стороны интеграции</typeparam>
        /// <param name="oppositeSideCode">Код противоположной стороны интеграции</param>
        /// <param name="srcData">Исходные данные</param>
        /// <returns>Интеграция</returns>
        SentAndForgotIntegration CreateSentAndForgotIntegration<TOppositeSideProvider>(object oppositeSideCode, object srcData)
            where TOppositeSideProvider : ISentAndForgotIntegrationOppositeSideProvider, new();
    }
}
