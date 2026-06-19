using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait;

namespace IntegrationFlow.Contexts.Integrations._02Application
{
    /// <summary>
    /// Провайдер противоположной стороны интеграции
    /// </summary>
    internal interface ISentAndWaitIntegrationOppositeSideProvider
    {
        /// <summary>
        /// Предоставляет реализацию противоположной стороны интеграции по указанному коду
        /// </summary>
        /// <param name="integrationOppositeSideCode">Код противоположной стороны интеграции</param>
        SentAndWaitIntegrationOppositeSide IntegrationOppositeSideResolve(object integrationOppositeSideCode);

        /// <summary>
        /// Предоставляет реализацию обработчика результата интеграции
        /// </summary>
        /// <param name="integrationOppositeSideCode">Код противоположной стороны интеграции</param>
        SentAndWaitIntegrationResultHandler ResultHandlerResolve(object integrationOppositeSideCode);
    }
}
