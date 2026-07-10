namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;

/// <summary>
/// Reusable REST connection profile (section <c>RestConnections</c>).
/// </summary>
public sealed class RestConnectionProfile
{
    public string BaseAddress { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 30;

    public string BearerToken { get; set; } = string.Empty;

    public string Accept { get; set; } = "application/json";

    public string BasicAuthUser { get; set; } = string.Empty;

    public string BasicAuthPassword { get; set; } = string.Empty;

    public string ApiKeyHeaderName { get; set; } = string.Empty;

    public string ApiKeyHeaderValue { get; set; } = string.Empty;

    public string ClientCertificatePath { get; set; } = string.Empty;

    public string ClientCertificatePassword { get; set; } = string.Empty;

    public string TlsServerName { get; set; } = string.Empty;

    public string HealthCheckPath { get; set; } = string.Empty;

    public int HealthCheckTimeoutSeconds { get; set; } = 5;

    internal void ApplyTo(IRestConnectionConfiguration target)
    {
        if (!string.IsNullOrWhiteSpace(BaseAddress))
        {
            target.BaseAddress = BaseAddress;
        }

        if (TimeoutSeconds > 0)
        {
            target.TimeoutSeconds = TimeoutSeconds;
        }

        if (!string.IsNullOrWhiteSpace(BearerToken))
        {
            target.BearerToken = BearerToken;
        }

        if (!string.IsNullOrWhiteSpace(Accept))
        {
            target.Accept = Accept;
        }

        if (!string.IsNullOrWhiteSpace(BasicAuthUser))
        {
            target.BasicAuthUser = BasicAuthUser;
        }

        if (!string.IsNullOrWhiteSpace(BasicAuthPassword))
        {
            target.BasicAuthPassword = BasicAuthPassword;
        }

        if (!string.IsNullOrWhiteSpace(ApiKeyHeaderName))
        {
            target.ApiKeyHeaderName = ApiKeyHeaderName;
        }

        if (!string.IsNullOrWhiteSpace(ApiKeyHeaderValue))
        {
            target.ApiKeyHeaderValue = ApiKeyHeaderValue;
        }

        if (!string.IsNullOrWhiteSpace(ClientCertificatePath))
        {
            target.ClientCertificatePath = ClientCertificatePath;
        }

        if (!string.IsNullOrWhiteSpace(ClientCertificatePassword))
        {
            target.ClientCertificatePassword = ClientCertificatePassword;
        }

        if (!string.IsNullOrWhiteSpace(TlsServerName))
        {
            target.TlsServerName = TlsServerName;
        }

        if (!string.IsNullOrWhiteSpace(HealthCheckPath))
        {
            target.HealthCheckPath = HealthCheckPath;
        }

        if (HealthCheckTimeoutSeconds > 0)
        {
            target.HealthCheckTimeoutSeconds = HealthCheckTimeoutSeconds;
        }
    }
}
