using System.Globalization;
using IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;
using Xunit;

namespace IntegrationFlow.Tests.Localization;

[Collection(LocalizationTestCollection.Name)]
public sealed class ResourceLocalizationProviderTests : IDisposable
{
    private readonly CultureInfo previousCulture;

    public ResourceLocalizationProviderTests()
    {
        previousCulture = CultureInfo.CurrentCulture;
        SR.Configure(new ResourceLocalizationProvider());
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = previousCulture;
        SR.Configure(null);
    }

    [Fact]
    public void T_WithEnglishCulture_ReturnsTranslatedString()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");

        var result = SR.T("Поток слушателя. Ошибка запуска.");

        Assert.Equal("Listener thread. Start error.", result);
    }

    [Fact]
    public void T_WithFormatArgs_AppliesTranslationAndFormatting()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en");

        var result = SR.T("SendAndWait - '{0}' - Интеграция запущена", "TEST");

        Assert.Equal("SendAndWait - 'TEST' - Integration started", result);
    }

    [Fact]
    public void T_WhenTranslationMissing_FallsBackToFormat()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        var result = SR.T("Unknown key {0}", 42);

        Assert.Equal("Unknown key 42", result);
    }
}
