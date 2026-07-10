using System;
using System.IO;
using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using Xunit;

namespace IntegrationFlow.Core.Tests.Outbox;

[Collection("OutboxTransportResolver")]
public sealed class OutboxTransportResolverTests : IDisposable
{
    private string? restConfigPath;
    private string? rabbitConfigPath;

    public void Dispose()
    {
        if (restConfigPath != null && File.Exists(restConfigPath))
        {
            File.Delete(restConfigPath);
        }

        if (rabbitConfigPath != null && File.Exists(rabbitConfigPath))
        {
            File.Delete(rabbitConfigPath);
        }
    }

    [Fact]
    public void CreatePublisher_ReturnsRestPublisher_WhenRestPublishProfileExists()
    {
        WriteRestConfig(
            """
            {
              "RestPublish": {
                "NotifyWebhook": {
                  "BaseAddress": "https://api.example.com/",
                  "RequestPath": "/v1/events"
                }
              }
            }
            """);

        using var publisher = new OutboxTransportResolver().CreatePublisher("NotifyWebhook");

        Assert.Equal(OutboxTransportKind.Rest, publisher.TransportKind);
    }

    [Fact]
    public void CreatePublisher_ReturnsRabbitMqPublisher_WhenOnlyRabbitProfileExists()
    {
        WriteRabbitConfig(
            """
            {
              "RabbitMqPublish": {
                "OrdersOut": {
                  "HostName": "localhost",
                  "QueueName": "orders.out"
                }
              }
            }
            """);

        using var publisher = new OutboxTransportResolver().CreatePublisher("OrdersOut");

        Assert.Equal(OutboxTransportKind.RabbitMq, publisher.TransportKind);
    }

    [Fact]
    public void CreatePublisher_Throws_WhenProfileMissingInBothTransports()
    {
        WriteRestConfig("{}");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new OutboxTransportResolver().CreatePublisher("MissingProfile"));

        Assert.Contains("MissingProfile", exception.Message);
    }

    private void WriteRestConfig(string json)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "rest.json");
        File.WriteAllText(path, json);
        restConfigPath = path;
    }

    private void WriteRabbitConfig(string json)
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "rabbitmq.json");
        File.WriteAllText(path, json);
        rabbitConfigPath = path;
    }
}

[CollectionDefinition("OutboxTransportResolver", DisableParallelization = true)]
public sealed class OutboxTransportResolverTestCollection;
