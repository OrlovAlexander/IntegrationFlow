using System.Globalization;

namespace IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;

/// <summary>
/// Фасад локализации, совместимый с API ELMA (<c>EleWise.ELMA.SR.T</c>).
/// </summary>
public static class SR
{
    private static ILocalizationProvider? _provider;

    /// <summary>
    /// Регистрирует провайдер локализации (вызывается из host-приложения).
    /// </summary>
    public static void Configure(ILocalizationProvider? provider) => _provider = provider;

    /// <summary>
    /// Возвращает локализованную строку или форматирует исходный текст, если провайдер не настроен.
    /// </summary>
    public static string T(string format, params object[] args) =>
        _provider?.T(format, args)
        ?? string.Format(CultureInfo.CurrentCulture, format, args);
}
