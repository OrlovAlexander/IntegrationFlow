namespace IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;

/// <summary>
/// Провайдер локализованных строк для host-приложения.
/// </summary>
public interface ILocalizationProvider
{
    /// <summary>
    /// Возвращает локализованную строку по формату и аргументам.
    /// </summary>
    string T(string format, params object[] args);
}
