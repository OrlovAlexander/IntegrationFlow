using System.Globalization;
using System.Resources;

namespace IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;

/// <summary>
/// Доступ к встроенным строковым ресурсам локализации.
/// </summary>
internal static class IntegrationFlowResources
{
    private static ResourceManager? resourceMan;

    internal static ResourceManager ResourceManager =>
        resourceMan ??= new ResourceManager(
            "IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization.IntegrationFlowResources",
            typeof(IntegrationFlowResources).Assembly);

    internal static CultureInfo? Culture { get; set; }
}
