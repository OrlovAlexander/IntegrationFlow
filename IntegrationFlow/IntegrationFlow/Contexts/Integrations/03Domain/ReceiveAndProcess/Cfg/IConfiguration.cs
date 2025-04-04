namespace IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Cfg
{
    /// <summary>
    /// Конфигурация публикатора, слушателя
    /// </summary>
    public interface IConfiguration
    {
        /// <summary>
        /// Слушатель. Асинхронная обработка входящего сообщения, запроса и т.п.
        /// </summary>
        bool Asynchronously { get; set; }
    }
}
