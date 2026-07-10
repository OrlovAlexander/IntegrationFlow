using System;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;

/// <summary>
/// Resolves <c>Connection</c> references to shared profiles from <c>RestConnections</c>.
/// </summary>
public static class RestConnectionProfileResolver
{
    /// <summary>
    /// Shared connection profiles section name.
    /// </summary>
    public const string ConnectionsSectionName = "RestConnections";

    /// <summary>
    /// Connection reference property name.
    /// </summary>
    public const string ConnectionReferencePropertyName = "Connection";

    /// <summary>
    /// Applies shared connection profile before binding scenario-specific settings.
    /// </summary>
    public static void ApplySharedConnectionBeforeBind(
        IConfiguration configuration,
        IConfigurationSection profileSection,
        IRestConnectionConfiguration target)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        if (profileSection == null)
        {
            throw new ArgumentNullException(nameof(profileSection));
        }

        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        var connectionName = profileSection[ConnectionReferencePropertyName];
        if (string.IsNullOrWhiteSpace(connectionName))
        {
            return;
        }

        var sharedSection = ResolveSharedSection(configuration, connectionName);
        var sharedProfile = new RestConnectionProfile();
        sharedSection.Bind(sharedProfile);
        sharedProfile.ApplyTo(target);
    }

    private static IConfigurationSection ResolveSharedSection(IConfiguration configuration, string connectionName)
    {
        var connectionsSection = configuration.GetSection(ConnectionsSectionName);
        if (!connectionsSection.Exists())
        {
            throw new InvalidOperationException(
                $"Shared REST connection profile '{connectionName}' not found: " +
                $"section '{ConnectionsSectionName}' is missing.");
        }

        var sharedSection = connectionsSection.GetChildren()
            .FirstOrDefault(child => string.Equals(child.Key, connectionName, StringComparison.OrdinalIgnoreCase));

        if (sharedSection == null || !sharedSection.GetChildren().Any())
        {
            throw new InvalidOperationException(
                $"Shared REST connection profile '{connectionName}' not found in section '{ConnectionsSectionName}'.");
        }

        return sharedSection;
    }
}
