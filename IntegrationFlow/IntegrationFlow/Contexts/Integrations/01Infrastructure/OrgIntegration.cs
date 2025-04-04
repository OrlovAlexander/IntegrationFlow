using System;
using System.Collections.Concurrent;
using IntegrationFlow.Contexts.Integrations._02Application;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait;

namespace IntegrationFlow.Contexts.Integrations._01Infrastructure
{
    /// <summary>
    /// Реализация интеграции Веста Банка с партнерами
    /// </summary>
    internal class OrgIntegration : IOrgIntegration
    {
        /// <summary>
        /// Провайдер противоположной стороны интеграции
        /// </summary>
        private ConcurrentDictionary<Type, ISentAndWaitIntegrationOppositeSideProvider> SentAndWaitProvider { get; set; }

        /// <summary>
        /// Провайдер противоположной стороны интеграции
        /// </summary>
        private ConcurrentDictionary<Type, ISentAndForgotIntegrationOppositeSideProvider> SentAndForgotProvider { get; set; }

        /// <summary>
        /// Ctor
        /// </summary>
        public OrgIntegration()
        {
            SentAndForgotProvider = new ConcurrentDictionary<Type, ISentAndForgotIntegrationOppositeSideProvider>();
            SentAndWaitProvider = new ConcurrentDictionary<Type, ISentAndWaitIntegrationOppositeSideProvider>();
        }

        /// <summary>
        /// Создать интеграцию
        /// </summary>
        /// <typeparam name="TOppositeSideProvider">Провайдер противоположной стороны интеграции</typeparam>
        /// <param name="oppositeSideCode">Код противоположной стороны интеграции</param>
        /// <param name="srcData">Исходные данные</param>
        /// <returns>Интеграция</returns>
        SentAndWaitIntegration IOrgIntegration.CreateSentAndWaitIntegration<TOppositeSideProvider>(object oppositeSideCode, object srcData)
        {
            ISentAndWaitIntegrationOppositeSideProvider provider = FindSentAndWaitProvider<TOppositeSideProvider>();
            var integrationOppositeSide = provider.IntegrationOppositeSideResolve(oppositeSideCode);

            return new SentAndWaitIntegration(integrationOppositeSide, srcData, Logger.Create());
        }

        /// <summary>
        /// Возвращает обработчик результата запроса
        /// </summary>
        /// <typeparam name="TOppositeSideProvider">Провайдер противоположной стороны интеграции</typeparam>
        /// <param name="oppositeSideCode">Код противоположной стороны интеграции</param>
        SentAndWaitIntegrationResultHandler IOrgIntegration.GetSentAndWaitResultHandler<TOppositeSideProvider>(object oppositeSideCode)
        {
            ISentAndWaitIntegrationOppositeSideProvider provider = FindSentAndWaitProvider<TOppositeSideProvider>();
            return provider.ResultHandlerResolve(oppositeSideCode);
        }

        /// <summary>
        /// Создать интеграцию
        /// </summary>
        /// <typeparam name="TOppositeSideProvider">Провайдер противоположной стороны интеграции</typeparam>
        /// <param name="oppositeSideCode">Код противоположной стороны интеграции</param>
        /// <param name="srcData">Исходные данные</param>
        /// <returns>Интеграция</returns>
        SentAndForgotIntegration IOrgIntegration.CreateSentAndForgotIntegration<TOppositeSideProvider>(object oppositeSideCode, object srcData)
        {
            ISentAndForgotIntegrationOppositeSideProvider provider = FindSentAndForgotProvider<TOppositeSideProvider>();
            var integrationOppositeSide = provider.IntegrationOppositeSideResolve(oppositeSideCode);

            return new SentAndForgotIntegration(integrationOppositeSide, srcData, Logger.Create());
        }

        /// <summary>
        /// Возвращает провайдер интеграций и в случае отсутствия добавляет в коллекцию
        /// </summary>
        /// <typeparam name="TOppositeSideProvider">Провайдер противоположной стороны интеграции</typeparam>
        /// <returns>Провайдер интеграций</returns>
        private ISentAndWaitIntegrationOppositeSideProvider FindSentAndWaitProvider<TOppositeSideProvider>()
            where TOppositeSideProvider : ISentAndWaitIntegrationOppositeSideProvider, new()
        {
            return SentAndWaitProvider.GetOrAdd(typeof(TOppositeSideProvider), (key) =>
                (ISentAndWaitIntegrationOppositeSideProvider)Activator.CreateInstance(typeof(TOppositeSideProvider)));
        }

        /// <summary>
        /// Возвращает провайдер интеграций и в случае отсутствия добавляет в коллекцию
        /// </summary>
        /// <typeparam name="TOppositeSideProvider">Провайдер противоположной стороны интеграции</typeparam>
        /// <returns>Провайдер интеграций</returns>
        private ISentAndForgotIntegrationOppositeSideProvider FindSentAndForgotProvider<TOppositeSideProvider>()
            where TOppositeSideProvider : ISentAndForgotIntegrationOppositeSideProvider, new()
        {
            return SentAndForgotProvider.GetOrAdd(typeof(TOppositeSideProvider), (key) =>
                (ISentAndForgotIntegrationOppositeSideProvider)Activator.CreateInstance(typeof(TOppositeSideProvider)));
        }
    }
}
