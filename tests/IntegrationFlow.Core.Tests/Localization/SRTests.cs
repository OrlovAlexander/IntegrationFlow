using System.Globalization;
using IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;
using Xunit;

namespace IntegrationFlow.Tests.Localization;

public sealed class SRTests : IDisposable
{
    public SRTests() => SR.Configure(null);

    public void Dispose() => SR.Configure(null);

    [Fact]
    public void T_WithoutProvider_FormatsWithCurrentCulture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            var result = SR.T("Value: {0}", 42);

            Assert.Equal("Value: 42", result);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void T_WithProvider_DelegatesToProvider()
    {
        var provider = new TestLocalizationProvider((format, args) =>
            $"localized:{string.Format(CultureInfo.InvariantCulture, format, args)}");

        SR.Configure(provider);

        var result = SR.T("Hello {0}", "world");

        Assert.Equal("localized:Hello world", result);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public void Configure_ReplacesProvider()
    {
        var first = new TestLocalizationProvider((_, _) => "first");
        var second = new TestLocalizationProvider((_, _) => "second");

        SR.Configure(first);
        Assert.Equal("first", SR.T("any"));

        SR.Configure(second);
        Assert.Equal("second", SR.T("any"));
    }

    private sealed class TestLocalizationProvider(Func<string, object[], string> translate) : ILocalizationProvider
    {
        public int CallCount { get; private set; }

        public string T(string format, params object[] args)
        {
            CallCount++;
            return translate(format, args);
        }
    }
}
