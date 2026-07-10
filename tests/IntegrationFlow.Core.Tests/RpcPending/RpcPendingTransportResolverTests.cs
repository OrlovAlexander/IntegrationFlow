using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;
using Xunit;

namespace IntegrationFlow.Core.Tests.RpcPending;

public sealed class RpcPendingTransportResolverTests
{
    [Fact]
    public void CreatePublisher_ThrowsWhenProfileMissing()
    {
        var resolver = new RpcPendingTransportResolver();

        var exception = Assert.Throws<InvalidOperationException>(() => resolver.CreatePublisher("MissingProfile"));
        Assert.Contains("MissingProfile", exception.Message);
    }

    [Fact]
    public void CreatePublisher_ThrowsWhenRestProfileIsSync()
    {
        var resolver = new RpcPendingTransportResolver();

        var exception = Assert.Throws<InvalidOperationException>(() => resolver.CreatePublisher("OrdersLookup"));
        Assert.Contains("AsyncOutbox", exception.Message);
    }

    [Fact]
    public void ValidateAsyncOutbox_RequiresResponseWebhookProfile()
    {
        var configuration = new RestRequestReplyConfiguration
        {
            Name = "PaymentAuth",
            BaseAddress = "https://api.partner.example/",
            RequestPath = "/v1/payments/authorize",
            Method = "POST",
            RequestMode = RestRequestReplyRequestMode.AsyncOutbox,
            ResponseCallbackBaseUrl = "https://app.example.com",
        };

        var exception = Assert.Throws<InvalidOperationException>(() => configuration.ValidateAsyncOutbox());
        Assert.Contains("ResponseWebhookProfileName", exception.Message);
    }

    [Fact]
    public void BuildCallbackUrl_CombinesBaseUrlAndWebhookPath()
    {
        var configuration = new RestRequestReplyConfiguration
        {
            ResponseCallbackBaseUrl = "https://app.example.com/",
        };
        var webhookConfiguration = new RestWebhookConfiguration
        {
            Path = "/integrations/rpc-responses/payments",
        };

        var callbackUrl = configuration.BuildCallbackUrl(webhookConfiguration);

        Assert.Equal("https://app.example.com/integrations/rpc-responses/payments", callbackUrl);
    }
}
