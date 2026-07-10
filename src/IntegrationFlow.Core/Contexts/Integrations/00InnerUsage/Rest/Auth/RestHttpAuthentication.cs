using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Auth;

internal static class RestHttpAuthentication
{
    public static void Apply(HttpRequestMessage request, IRestConnectionConfiguration configuration)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        if (!string.IsNullOrWhiteSpace(configuration.BearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuration.BearerToken);
            return;
        }

        if (!string.IsNullOrWhiteSpace(configuration.BasicAuthUser))
        {
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{configuration.BasicAuthUser}:{configuration.BasicAuthPassword}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            return;
        }

        if (!string.IsNullOrWhiteSpace(configuration.ApiKeyHeaderName) &&
            !string.IsNullOrWhiteSpace(configuration.ApiKeyHeaderValue))
        {
            request.Headers.TryAddWithoutValidation(configuration.ApiKeyHeaderName, configuration.ApiKeyHeaderValue);
        }
    }
}
