namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Formatter
{
    /// <summary>
    /// Форматировщик передаваемых данных
    /// </summary>
    public interface IFormatterTransmitData
    {
        /// <summary>
        /// Форматировать передаваемые данные
        /// </summary>
        /// <param name="srcData">Передаваемые данные</param>
        /// <returns>Данные в нужном формате и готовые для передачи на принимающую сторону</returns>
        TransmitData FormatData(TransmitData srcData);
    }
}
