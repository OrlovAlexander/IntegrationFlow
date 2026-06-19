namespace IntegrationFlow.ExtensionsPoints
{
    /// <summary>
    /// Запускает интеграцию типа "Получить и обработать"
    /// </summary>
    public interface IReceiveAndProcessLauncher
    {
        /// <summary>
        /// Запустить интеграцию типа "Получить и обработать"
        /// </summary>
        void Run();
        /* Пример реализации
         {
            var publisher = Publisher.Create<BrmsPublisher>(new BrmsIntegrationPublisherSide(), Logger.Create());
			publisher.BeginReceiving();
         }
         */
    }
}
