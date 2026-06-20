using System.Globalization;

namespace IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;

/// <summary>
/// Провайдер локализации через встроенные ресурсы (<c>.resx</c>).
/// Ключ перевода — исходная строка (как в ELMA Orchard Localizer).
/// </summary>
public sealed class ResourceLocalizationProvider : ILocalizationProvider
{
    /// <inheritdoc />
    public string T(string format, params object[] args)
    {
        var culture = IntegrationFlowResources.Culture ?? CultureInfo.CurrentCulture;
        var template = IntegrationFlowResources.ResourceManager.GetString(format, culture) ?? format;
        return string.Format(culture, template, args);
    }
}
