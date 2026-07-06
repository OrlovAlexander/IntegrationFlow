using System;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Configurations;

/// <summary>
/// Разрешает ссылки <c>Connection</c> на общие профили из секции <c>RabbitMqConnections</c>.
/// </summary>
public static class RabbitMqConnectionProfileResolver
{
    /// <summary>
    /// Имя секции с общими профилями подключения.
    /// </summary>
    public const string ConnectionsSectionName = "RabbitMqConnections";

    /// <summary>
    /// Имя свойства-ссылки на общий профиль подключения.
    /// </summary>
    public const string ConnectionReferencePropertyName = "Connection";

    /// <summary>
    /// Применяет общий профиль подключения до bind секции сценария (значения сценария переопределяют общие).
    /// </summary>
    public static void ApplySharedConnectionBeforeBind(
        IConfiguration configuration,
        IConfigurationSection profileSection,
        IRabbitMqConnectionConfiguration target)
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
        var sharedProfile = new RabbitMqConnectionProfile();
        sharedSection.Bind(sharedProfile);
        sharedProfile.ApplyTo(target);
    }

    private static IConfigurationSection ResolveSharedSection(IConfiguration configuration, string connectionName)
    {
        var connectionsSection = configuration.GetSection(ConnectionsSectionName);
        if (!connectionsSection.Exists())
        {
            throw new InvalidOperationException(
                $"Shared RabbitMQ connection profile '{connectionName}' not found: " +
                $"section '{ConnectionsSectionName}' is missing.");
        }

        var sharedSection = connectionsSection.GetChildren()
            .FirstOrDefault(child => string.Equals(child.Key, connectionName, StringComparison.OrdinalIgnoreCase));

        if (sharedSection == null || !sharedSection.GetChildren().Any())
        {
            throw new InvalidOperationException(
                $"Shared RabbitMQ connection profile '{connectionName}' not found in section '{ConnectionsSectionName}'.");
        }

        return sharedSection;
    }
}
