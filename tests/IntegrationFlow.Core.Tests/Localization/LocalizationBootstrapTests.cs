using System.Globalization;
using IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;
using IntegrationFlow.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationFlow.Tests.Localization;

[Collection(LocalizationTestCollection.Name)]
public sealed class LocalizationBootstrapTests : IDisposable
{
    private readonly CultureInfo previousCulture;

    public LocalizationBootstrapTests()
    {
        previousCulture = CultureInfo.CurrentCulture;
        SR.Configure(null);
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = previousCulture;
        SR.Configure(null);
    }

    [Fact]
    public void UseEmbeddedResources_ConfiguresResourceLocalizationProvider()
    {
        LocalizationBootstrap.UseEmbeddedResources();
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");

        var result = SR.T("Поток слушателя. Ошибка запуска.");

        Assert.Equal("Listener thread. Start error.", result);
    }

    [Fact]
    public void AddIntegrationFlow_ConfiguresEmbeddedResources()
    {
        new ServiceCollection().AddIntegrationFlow();
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");

        var result = SR.T("SendAndWait - '{0}' - Интеграция запущена", "TEST");

        Assert.Equal("SendAndWait - 'TEST' - Integration started", result);
    }
}
