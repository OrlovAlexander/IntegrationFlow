using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Connections;

internal static class RestHttpClientHandlerFactory
{
    public static HttpMessageHandler CreateHandler(IRestConnectionConfiguration configuration)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

#if NET5_0_OR_GREATER
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };

        if (!string.IsNullOrWhiteSpace(configuration.ClientCertificatePath))
        {
            var certificate = LoadCertificate(
                configuration.ClientCertificatePath,
                configuration.ClientCertificatePassword);
            handler.SslOptions = new SslClientAuthenticationOptions
            {
                ClientCertificates = new X509CertificateCollection { certificate },
            };

            if (!string.IsNullOrWhiteSpace(configuration.TlsServerName))
            {
                handler.SslOptions.TargetHost = configuration.TlsServerName;
            }
        }
        else if (!string.IsNullOrWhiteSpace(configuration.TlsServerName))
        {
            handler.SslOptions = new SslClientAuthenticationOptions
            {
                TargetHost = configuration.TlsServerName,
            };
        }

        return handler;
#else
        var handler = new HttpClientHandler();
        if (!string.IsNullOrWhiteSpace(configuration.ClientCertificatePath))
        {
            var certificate = LoadCertificate(
                configuration.ClientCertificatePath,
                configuration.ClientCertificatePassword);
            handler.ClientCertificates.Add(certificate);
        }

        return handler;
#endif
    }

    private static X509Certificate2 LoadCertificate(string path, string password)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Client certificate path is required.");
        }

        return string.IsNullOrWhiteSpace(password)
            ? new X509Certificate2(path)
            : new X509Certificate2(path, password);
    }
}
