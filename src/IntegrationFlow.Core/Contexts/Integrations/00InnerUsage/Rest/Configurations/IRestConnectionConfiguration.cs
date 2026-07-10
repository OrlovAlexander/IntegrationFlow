namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;

/// <summary>
/// REST connection settings shared between request-reply and publish profiles.
/// </summary>
public interface IRestConnectionConfiguration
{
    string BaseAddress { get; set; }

    int TimeoutSeconds { get; set; }

    string BearerToken { get; set; }

    string Accept { get; set; }

    string BasicAuthUser { get; set; }

    string BasicAuthPassword { get; set; }

    string ApiKeyHeaderName { get; set; }

    string ApiKeyHeaderValue { get; set; }

    string ClientCertificatePath { get; set; }

    string ClientCertificatePassword { get; set; }

    string TlsServerName { get; set; }

    string HealthCheckPath { get; set; }

    int HealthCheckTimeoutSeconds { get; set; }
}
