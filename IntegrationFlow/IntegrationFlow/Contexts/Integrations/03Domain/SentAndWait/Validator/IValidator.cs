namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Validator
{
    /// <summary>
    /// Валидатор результата интеграции
    /// </summary>
    public interface IValidator
    {
        /// <summary>
        /// Проверить результат интеграции
        /// </summary>
        /// <param name="obtainedData">Результат интеграции</param>
        void Validate(ref ObtainedData obtainedData);
    }
}
