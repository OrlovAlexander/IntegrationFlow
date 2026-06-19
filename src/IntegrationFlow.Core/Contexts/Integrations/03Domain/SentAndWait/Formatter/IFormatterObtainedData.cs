namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Formatter
{
    /// <summary>
    /// Форматировщик полученных данных
    /// </summary>
    public interface IFormatterObtainedData
    {
        /// <summary>
        /// Форматировать исходные результаты интеграции
        /// </summary>
        /// <param name="obtainedData">Исходные результаты интеграции</param>
        /// <returns>Преобразованные результаты интеграции</returns>
        ObtainedData FormatData(ObtainedData obtainedData);
    }
}
