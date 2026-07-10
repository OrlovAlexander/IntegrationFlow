using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;

/// <summary>
/// Builds merged REST configuration from file, environment variables and optional overlay.
/// </summary>
public static class RestConfigurationComposition
{
    /// <summary>
    /// Optional <see cref="IConfiguration"/> overlay (for example ASP.NET Core host configuration).
    /// </summary>
    public static IConfiguration? OverlayConfiguration { get; set; }

    /// <summary>
    /// Clears overlay configuration (intended for tests).
    /// </summary>
    internal static void ResetOverlayConfiguration()
        => OverlayConfiguration = null;

    /// <summary>
    /// Builds configuration: JSON file → environment variables → <see cref="OverlayConfiguration"/>.
    /// </summary>
    public static IConfigurationRoot BuildFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Configuration file path is required.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"REST configuration file '{filePath}' was not found.", filePath);
        }

        var directory = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException($"Could not resolve directory for configuration file '{filePath}'.");

        var fileName = Path.GetFileName(filePath);

        var builder = new ConfigurationBuilder()
            .SetBasePath(directory)
            .AddJsonFile(fileName, optional: false, reloadOnChange: false)
            .AddEnvironmentVariables();

        var overlay = OverlayConfiguration;
        if (overlay != null)
        {
            builder.AddConfiguration(overlay);
        }

        return builder.Build();
    }
}
