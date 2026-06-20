namespace IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;

/// <summary>
/// Bootstrap-хелпер для регистрации локализации в standalone host-приложениях.
/// </summary>
public static class LocalizationBootstrap
{
    /// <summary>
    /// Подключает переводы из встроенных <c>.resx</c>-ресурсов.
    /// </summary>
    public static void UseEmbeddedResources() =>
        SR.Configure(new ResourceLocalizationProvider());
}
