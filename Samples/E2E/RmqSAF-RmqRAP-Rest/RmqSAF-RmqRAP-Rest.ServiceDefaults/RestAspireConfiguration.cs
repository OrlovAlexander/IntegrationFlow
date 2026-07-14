using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.Hosting;

public static class RestAspireConfiguration
{
    private const string RestConnectionsSection = "RestConnections";

    /// <summary>
    /// Maps an Aspire <c>WithReference</c> HTTP service URL to IntegrationFlow <c>RestConnections</c> profiles.
    /// No-op when the service reference is not present (Docker Compose, local overrides).
    /// </summary>
    public static void ApplyRestServiceReference(
        ConfigurationManager configuration,
        string serviceReferenceName,
        params string[] connectionProfileNames)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        if (string.IsNullOrWhiteSpace(serviceReferenceName))
        {
            throw new ArgumentException("Service reference name is required.", nameof(serviceReferenceName));
        }

        if (connectionProfileNames == null || connectionProfileNames.Length == 0)
        {
            throw new ArgumentException("At least one REST connection profile name is required.", nameof(connectionProfileNames));
        }

        if (!TryResolveServiceBaseAddress(configuration, serviceReferenceName, out var baseAddress))
        {
            return;
        }

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var profileName in connectionProfileNames)
        {
            if (string.IsNullOrWhiteSpace(profileName))
            {
                continue;
            }

            values[$"{RestConnectionsSection}:{profileName}:BaseAddress"] = baseAddress;
        }

        if (values.Count > 0)
        {
            configuration.AddInMemoryCollection(values);
        }
    }

    private static bool TryResolveServiceBaseAddress(
        IConfiguration configuration,
        string serviceReferenceName,
        out string baseAddress)
    {
        baseAddress = string.Empty;

        var httpEndpoint = configuration[$"services:{serviceReferenceName}:http:0"];
        if (TryNormalizeBaseAddress(httpEndpoint, out baseAddress))
        {
            return true;
        }

        var httpsEndpoint = configuration[$"services:{serviceReferenceName}:https:0"];
        if (TryNormalizeBaseAddress(httpsEndpoint, out baseAddress))
        {
            return true;
        }

        var connectionString = configuration.GetConnectionString(serviceReferenceName);
        return TryNormalizeBaseAddress(connectionString, out baseAddress);
    }

    private static bool TryNormalizeBaseAddress(string? value, out string baseAddress)
    {
        baseAddress = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        var authority = uri.GetLeftPart(UriPartial.Authority);
        baseAddress = authority.TrimEnd('/');

        return true;
    }
}
