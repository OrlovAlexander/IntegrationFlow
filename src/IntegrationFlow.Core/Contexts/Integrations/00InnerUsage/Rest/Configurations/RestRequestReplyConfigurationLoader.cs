using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;

/// <summary>
/// Loads REST request-reply settings from rest.json.
/// </summary>
public static class RestRequestReplyConfigurationLoader
{
    public const string DefaultFileName = "rest.json";
    public const string ConfigurationSectionName = "RestRequestReply";
    public const string DefaultProfileName = "Default";

    private static readonly HashSet<string> ConnectionPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(RestRequestReplyConfiguration.BaseAddress),
        nameof(RestRequestReplyConfiguration.RequestPath),
        nameof(RestRequestReplyConfiguration.Method),
        nameof(RestRequestReplyConfiguration.ContentType),
        nameof(RestRequestReplyConfiguration.Accept),
        nameof(RestRequestReplyConfiguration.ResponseTimeoutSeconds),
        nameof(RestRequestReplyConfiguration.TimeoutSeconds),
        nameof(RestRequestReplyConfiguration.MaxConcurrentRequests),
        nameof(RestRequestReplyConfiguration.IdempotencyHeaderName),
        nameof(RestRequestReplyConfiguration.BearerToken),
        nameof(RestRequestReplyConfiguration.RetryOnTransientErrors),
        nameof(RestRequestReplyConfiguration.MaxTransientRetries),
    };

    public static RestRequestReplyConfiguration Load()
        => LoadSingle(ResolveConfigFilePath());

    public static RestRequestReplyConfiguration LoadSingle(string filePath)
        => LoadSingleProfile(filePath);

    public static RestRequestReplyConfiguration LoadFromFile(string filePath)
    {
        var configuration = BuildConfiguration(filePath);
        var section = configuration.GetSection(ConfigurationSectionName);

        if (!IsLegacyFlatFormat(section))
        {
            throw new InvalidOperationException(
                $"File '{filePath}' contains named REST request-reply profiles. Use LoadProfile or LoadAll.");
        }

        var requestReplyConfiguration = BindSection(configuration, section);
        requestReplyConfiguration.Name = DefaultProfileName;
        requestReplyConfiguration.Validate();
        return requestReplyConfiguration;
    }

    public static RestRequestReplyConfiguration LoadProfile(string profileName)
        => LoadProfile(profileName, ResolveConfigFilePath());

    public static RestRequestReplyConfiguration LoadProfile(string profileName, string filePath)
        => LoadProfile(profileName, BuildConfiguration(filePath));

    public static RestRequestReplyConfiguration LoadProfile(string profileName, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("REST request-reply profile name is required.", nameof(profileName));
        }

        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        var profile = ResolveProfiles(configuration)
            .FirstOrDefault(item => string.Equals(item.Name, profileName, StringComparison.OrdinalIgnoreCase));

        if (profile == null)
        {
            throw new InvalidOperationException(
                $"REST request-reply profile '{profileName}' was not found in configuration.");
        }

        return profile;
    }

    public static IReadOnlyList<RestRequestReplyConfiguration> LoadAll()
        => LoadAll(ResolveConfigFilePath());

    public static IReadOnlyList<RestRequestReplyConfiguration> LoadAll(string filePath)
        => LoadAll(BuildConfiguration(filePath));

    public static IReadOnlyList<RestRequestReplyConfiguration> LoadAll(IConfiguration configuration)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        return ResolveProfiles(configuration);
    }

    public static string ResolveConfigFilePath(string? filePath = null)
    {
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            return Path.GetFullPath(filePath);
        }

        var baseDirectoryPath = Path.Combine(AppContext.BaseDirectory, DefaultFileName);
        if (File.Exists(baseDirectoryPath))
        {
            return baseDirectoryPath;
        }

        var currentDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), DefaultFileName);
        if (File.Exists(currentDirectoryPath))
        {
            return currentDirectoryPath;
        }

        throw new FileNotFoundException(
            $"REST configuration file '{DefaultFileName}' was not found. " +
            $"Expected in '{AppContext.BaseDirectory}' or '{Directory.GetCurrentDirectory()}'.");
    }

    private static RestRequestReplyConfiguration LoadSingleProfile(string filePath)
    {
        var profiles = ResolveProfiles(BuildConfiguration(filePath));
        if (profiles.Count == 1)
        {
            return profiles[0];
        }

        throw new InvalidOperationException(
            $"File '{filePath}' contains {profiles.Count} REST request-reply profiles. Use LoadProfile(name) or LoadAll().");
    }

    private static IReadOnlyList<RestRequestReplyConfiguration> ResolveProfiles(IConfiguration configuration)
    {
        var section = configuration.GetSection(ConfigurationSectionName);
        if (!section.Exists())
        {
            throw new InvalidOperationException(
                $"Configuration section '{ConfigurationSectionName}' is missing.");
        }

        if (IsLegacyFlatFormat(section))
        {
            var requestReplyConfiguration = BindSection(configuration, section);
            requestReplyConfiguration.Name = DefaultProfileName;
            requestReplyConfiguration.Validate();
            return new[] { requestReplyConfiguration };
        }

        var profiles = new List<RestRequestReplyConfiguration>();
        foreach (var child in section.GetChildren())
        {
            var profile = BindSection(configuration, child);
            profile.Name = child.Key;
            profile.Validate();
            profiles.Add(profile);
        }

        if (profiles.Count == 0)
        {
            throw new InvalidOperationException(
                $"No REST request-reply profiles found in section '{ConfigurationSectionName}'.");
        }

        return profiles;
    }

    private static bool IsLegacyFlatFormat(IConfigurationSection section)
        => section.GetChildren().Any(child => ConnectionPropertyNames.Contains(child.Key));

    private static RestRequestReplyConfiguration BindSection(IConfiguration configuration, IConfigurationSection section)
    {
        var requestReplyConfiguration = new RestRequestReplyConfiguration();
        RestConnectionProfileResolver.ApplySharedConnectionBeforeBind(configuration, section, requestReplyConfiguration);
        section.Bind(requestReplyConfiguration);
        return requestReplyConfiguration;
    }

    private static IConfigurationRoot BuildConfiguration(string filePath)
        => RestConfigurationComposition.BuildFromFile(filePath);
}
