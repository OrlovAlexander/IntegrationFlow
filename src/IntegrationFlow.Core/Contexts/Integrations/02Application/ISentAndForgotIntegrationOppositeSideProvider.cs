using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot;

namespace IntegrationFlow.Contexts.Integrations._02Application
{
    /// <summary>
    /// Провайдер противоположной стороны интеграции
    /// </summary>
    internal interface ISentAndForgotIntegrationOppositeSideProvider
    {
        /// <summary>
        /// Предоставляет реализацию противоположной стороны интеграции по указанному коду
        /// </summary>
        /// <param name="integrationOppositeSideCode">Код противоположной стороны интеграции</param>
        SentAndForgotIntegrationOppositeSide IntegrationOppositeSideResolve(object integrationOppositeSideCode);
    }
}
