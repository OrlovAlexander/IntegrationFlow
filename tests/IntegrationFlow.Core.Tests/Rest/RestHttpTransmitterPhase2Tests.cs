using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Auth;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Connections;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Exceptions;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Health;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.SentAndWait.Cache;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.SentAndWait.Transmitters;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait;
using Xunit;

namespace IntegrationFlow.Core.Tests.Rest;

[Collection("RestHttp")]
public sealed class RestHttpTransmitterPhase2Tests : IDisposable
{
    public void Dispose()
    {
        RestHttpClientProvider.Reset();
        RestClientResponseCacheRegistry.Reset();
        SentAndWaitIntegrationOptions.RetryOnTimeout = false;
        SentAndWaitIntegrationOptions.MaxRetries = 1;
        SentAndWaitIntegrationOptions.RetryDelay = TimeSpan.FromMilliseconds(200);
    }

    [Fact]
    public async Task TransmitAsync_RetriesTransient503_ThenSucceeds()
    {
        var attempts = 0;
        var configuration = CreateConfiguration();
        configuration.MaxTransientRetries = 1;
        var transmitter = CreateTransmitter(configuration, (_, _) =>
        {
            attempts++;
            if (attempts == 1)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("busy", Encoding.UTF8, "text/plain"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok", Encoding.UTF8, "text/plain"),
            });
        });

        var result = await transmitter.TransmitAsync(new TransmitData("payload"), CancellationToken.None);

        Assert.False(result.IsFailed);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task TransmitAsync_DoesNotRetry4xx()
    {
        var attempts = 0;
        var configuration = CreateConfiguration();
        configuration.MaxTransientRetries = 2;
        var transmitter = CreateTransmitter(configuration, (_, _) =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("bad", Encoding.UTF8, "text/plain"),
            });
        });

        var result = await transmitter.TransmitAsync(new TransmitData("payload"), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task TransmitAsync_RetriesTimeout_WhenMessageIdAndRetryOnTimeoutEnabled()
    {
        SentAndWaitIntegrationOptions.RetryOnTimeout = true;
        SentAndWaitIntegrationOptions.MaxRetries = 1;
        SentAndWaitIntegrationOptions.RetryDelay = TimeSpan.FromMilliseconds(10);

        var attempts = 0;
        var configuration = CreateConfiguration(responseTimeoutSeconds: 1);
        configuration.RetryOnTransientErrors = false;
        var transmitter = CreateTransmitter(configuration, async (_, cancellationToken) =>
        {
            attempts++;
            if (attempts == 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok", Encoding.UTF8, "text/plain"),
            };
        });

        var result = await transmitter.TransmitAsync(
            new TransmitData("payload", "retry-key"),
            CancellationToken.None);

        Assert.False(result.IsFailed);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task TransmitAsync_UsesClientCache_ForDuplicateMessageId()
    {
        var attempts = 0;
        var configuration = CreateConfiguration();
        configuration.RetryOnTransientErrors = false;
        RestClientResponseCacheRegistry.Initialize(new InMemoryRestClientResponseCache());
        var transmitter = CreateTransmitter(configuration, (_, _) =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("cached-body", Encoding.UTF8, "text/plain"),
            });
        });

        var transmitData = new TransmitData("payload", "cache-key");
        var first = await transmitter.TransmitAsync(transmitData, CancellationToken.None);
        var second = await transmitter.TransmitAsync(transmitData, CancellationToken.None);

        Assert.Equal("cached-body", first.Data);
        Assert.Equal("cached-body", second.Data);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task TransmitAsync_AppliesBasicAuthHeader()
    {
        AuthenticationHeaderValue? authorization = null;
        var configuration = CreateConfiguration();
        configuration.BasicAuthUser = "user";
        configuration.BasicAuthPassword = "secret";
        var transmitter = CreateTransmitter(configuration, (request, _) =>
        {
            authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok", Encoding.UTF8, "text/plain"),
            });
        });

        await transmitter.TransmitAsync(new TransmitData("payload"), CancellationToken.None);

        Assert.NotNull(authorization);
        Assert.Equal("Basic", authorization!.Scheme);
        Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("user:secret")), authorization.Parameter);
    }

    [Fact]
    public async Task TransmitAsync_AppliesApiKeyHeader()
    {
        string? apiKey = null;
        var configuration = CreateConfiguration();
        configuration.ApiKeyHeaderName = "X-Api-Key";
        configuration.ApiKeyHeaderValue = "top-secret";
        var transmitter = CreateTransmitter(configuration, (request, _) =>
        {
            apiKey = request.Headers.TryGetValues("X-Api-Key", out var values)
                ? string.Join(",", values)
                : null;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok", Encoding.UTF8, "text/plain"),
            });
        });

        await transmitter.TransmitAsync(new TransmitData("payload"), CancellationToken.None);

        Assert.Equal("top-secret", apiKey);
    }

    [Fact]
    public async Task TransmitAsync_RecordsTransportTagInMetrics()
    {
        var metrics = new TestRequestReplyMetrics();
        var configuration = CreateConfiguration();
        configuration.RetryOnTransientErrors = false;
        var transmitter = CreateTransmitter(configuration, (_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ok", Encoding.UTF8, "text/plain"),
            }));
        transmitter.Metrics = metrics;

        await transmitter.TransmitAsync(new TransmitData("payload"), CancellationToken.None);

        Assert.Equal("rest", metrics.LastTransport);
        Assert.Equal(configuration.Name, metrics.LastProfileName);
        Assert.True(metrics.LastSuccess);
    }

    [Fact]
    public void Apply_BearerTakesPriorityOverBasicAndApiKey()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/");
        var configuration = new RestRequestReplyConfiguration
        {
            BearerToken = "bearer-token",
            BasicAuthUser = "user",
            BasicAuthPassword = "pass",
            ApiKeyHeaderName = "X-Api-Key",
            ApiKeyHeaderValue = "key",
        };

        RestHttpAuthentication.Apply(request, configuration);

        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("bearer-token", request.Headers.Authorization?.Parameter);
        Assert.False(request.Headers.Contains("X-Api-Key"));
    }

    [Fact]
    public void SetMonitoredProfiles_IncludesOnlyProfilesWithHealthCheckPath()
    {
        var registry = new RestTransportHealthRegistry();
        registry.SetMonitoredProfiles(new[]
        {
            new RestRequestReplyConfiguration
            {
                Name = "WithHealth",
                BaseAddress = "https://api.example.com/",
                HealthCheckPath = "/health",
            },
            new RestRequestReplyConfiguration
            {
                Name = "WithoutHealth",
                BaseAddress = "https://api.example.com/",
            },
        });

        var profiles = registry.GetMonitoredProfiles();

        Assert.Single(profiles);
        Assert.Equal("WithHealth", profiles[0].Name);
    }

    [Fact]
    public void ReportFailure_TracksConsecutiveFailures()
    {
        var registry = new RestTransportHealthRegistry();

        registry.ReportFailure("OrdersLookup");
        registry.ReportFailure("OrdersLookup");
        registry.ReportSuccess("OrdersLookup");
        registry.ReportFailure("Other");

        Assert.Equal(1, registry.GetMaxConsecutiveFailures());
    }

    private static RestRequestReplyConfiguration CreateConfiguration(int responseTimeoutSeconds = 30)
        => new()
        {
            Name = $"TestProfile-{Guid.NewGuid():N}",
            BaseAddress = "https://api.example.com/",
            RequestPath = "/v1/test",
            Method = "POST",
            ResponseTimeoutSeconds = responseTimeoutSeconds,
        };

    private static RestHttpTransmitter CreateTransmitter(
        RestRequestReplyConfiguration configuration,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        RestHttpClientProvider.Reset();
        var httpClient = new HttpClient(new LambdaHandler(handler));
        RestHttpClientProvider.RegisterTestClient(configuration.Name, httpClient);

        var connection = new RestHttpConnection(configuration);
        return new RestHttpTransmitter(configuration, connection);
    }

    private sealed class LambdaHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler;

        public LambdaHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            this.handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => handler(request, cancellationToken);
    }

    private sealed class TestRequestReplyMetrics : IIntegrationFlowMetrics
    {
        public string? LastProfileName { get; private set; }

        public bool LastSuccess { get; private set; }

        public string? LastTransport { get; private set; }

        public void RecordMessageProcessed(string profileName, TimeSpan duration, bool success)
        {
        }

        public void RecordOutboxRelayPublished(int count)
        {
        }

        public void RecordOutboxRelayFailed(int count)
        {
        }

        public void RecordOutboxRelayAbandoned(int count)
        {
        }

        public void RecordOutboxPending(int count)
        {
        }

        public void RecordRequestReply(
            string profileName,
            TimeSpan duration,
            bool success,
            bool timedOut = false,
            string? transport = null)
        {
            LastProfileName = profileName;
            LastSuccess = success;
            LastTransport = transport;
        }

        public void RecordRequestReplyRetryAfterTimeout(string profileName)
        {
        }

        public void RecordRpcPendingRelayPublished(int count)
        {
        }

        public void RecordRpcPendingRelayFailed(int count)
        {
        }

        public void RecordRpcPendingRelayAbandoned(int count)
        {
        }

        public void RecordRpcPendingAwaiting(int count)
        {
        }

        public void RecordRpcPendingCompleted(string profileName, TimeSpan duration, bool success, bool timedOut = false)
        {
        }

        public void RecordListenerReconnect(string profileName)
        {
        }

        public void RecordListenerShutdownRequeue(string profileName)
        {
        }

        public void RecordConsumerOutcome(string profileName, string reason)
        {
        }

        public void RecordConnectionPoolSize(string kind, int size)
        {
        }

        public void RecordBrokerConnected(string profileName, string kind, bool connected)
        {
        }
    }
}
