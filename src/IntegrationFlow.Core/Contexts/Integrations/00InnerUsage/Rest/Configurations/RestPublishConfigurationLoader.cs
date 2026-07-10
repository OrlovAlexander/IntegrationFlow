using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;

/// <summary>
/// Loads REST publish settings from rest.json.
/// </summary>
public static class RestPublishConfigurationLoader
{
    public const string ConfigurationSectionName = "RestPublish";
    public const string DefaultProfileName = "Default";

    private static readonly HashSet<string> ConnectionPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(RestPublishConfiguration.BaseAddress),
        nameof(RestPublishConfiguration.RequestPath),
        nameof(RestPublishConfiguration.Method),
        nameof(RestPublishConfiguration.ContentType),
        nameof(RestPublishConfiguration.Accept),
        nameof(RestPublishConfiguration.PublishTimeoutSeconds),
        nameof(RestPublishConfiguration.TimeoutSeconds),
        nameof(RestPublishConfiguration.IdempotencyHeaderName),
        nameof(RestPublishConfiguration.ExpectedStatusCodes),
        nameof(RestPublishConfiguration.BearerToken),
        nameof(RestPublishConfiguration.RetryOnTransientErrors),
        nameof(RestPublishConfiguration.MaxTransientRetries),
    };

    public static RestPublishConfiguration LoadProfile(string profileName)
        => LoadProfile(profileName, ResolveConfigFilePath());

    public static RestPublishConfiguration LoadProfile(string profileName, string filePath)
        => LoadProfile(profileName, BuildConfiguration(filePath));

    public static RestPublishConfiguration LoadProfile(string profileName, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("REST publish profile name is required.", nameof(profileName));
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
                $"REST publish profile '{profileName}' was not found in configuration.");
        }

        return profile;
    }

    public static bool TryLoadProfile(string profileName, out RestPublishConfiguration configuration)
    {
        configuration = null!;
        try
        {
            configuration = LoadProfile(profileName);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    public static bool TryLoadProfile(string profileName, IConfiguration hostConfiguration, out RestPublishConfiguration configuration)
    {
        configuration = null!;
        try
        {
            var fileConfiguration = BuildConfiguration(ResolveConfigFilePath());
            var profile = ResolveProfiles(fileConfiguration)
                .FirstOrDefault(item => string.Equals(item.Name, profileName, StringComparison.OrdinalIgnoreCase));
            if (profile == null)
            {
                return false;
            }

            configuration = profile;
            return true;
        }
        catch (FileNotFoundException)
        {
            return TryLoadProfileFromConfiguration(profileName, hostConfiguration, out configuration);
        }
        catch (InvalidOperationException)
        {
            return TryLoadProfileFromConfiguration(profileName, hostConfiguration, out configuration);
        }
    }

    public static IReadOnlyList<RestPublishConfiguration> LoadAll()
        => LoadAll(ResolveConfigFilePath());

    public static IReadOnlyList<RestPublishConfiguration> LoadAll(string filePath)
        => LoadAll(BuildConfiguration(filePath));

    public static IReadOnlyList<RestPublishConfiguration> LoadAll(IConfiguration configuration)
    {
        if (configuration == null)
        {
            throw new ArgumentNullException(nameof(configuration));
        }

        return ResolveProfiles(configuration);
    }

    public static string ResolveConfigFilePath(string? filePath = null)
        => RestRequestReplyConfigurationLoader.ResolveConfigFilePath(filePath);

    private static bool TryLoadProfileFromConfiguration(
        string profileName,
        IConfiguration hostConfiguration,
        out RestPublishConfiguration configuration)
    {
        configuration = null!;
        if (hostConfiguration == null)
        {
            return false;
        }

        try
        {
            var profiles = ResolveProfiles(hostConfiguration);
            configuration = profiles.FirstOrDefault(item =>
                string.Equals(item.Name, profileName, StringComparison.OrdinalIgnoreCase))!;
            return configuration != null;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static IReadOnlyList<RestPublishConfiguration> ResolveProfiles(IConfiguration configuration)
    {
        var section = configuration.GetSection(ConfigurationSectionName);
        if (!section.Exists())
        {
            throw new InvalidOperationException(
                $"Configuration section '{ConfigurationSectionName}' is missing.");
        }

        if (IsLegacyFlatFormat(section))
        {
            var publishConfiguration = BindSection(configuration, section);
            publishConfiguration.Name = DefaultProfileName;
            publishConfiguration.Validate();
            return new[] { publishConfiguration };
        }

        var profiles = new List<RestPublishConfiguration>();
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
                $"No REST publish profiles found in section '{ConfigurationSectionName}'.");
        }

        return profiles;
    }

    private static bool IsLegacyFlatFormat(IConfigurationSection section)
        => section.GetChildren().Any(child => ConnectionPropertyNames.Contains(child.Key));

    private static RestPublishConfiguration BindSection(IConfiguration configuration, IConfigurationSection section)
    {
        var publishConfiguration = new RestPublishConfiguration();
        RestConnectionProfileResolver.ApplySharedConnectionBeforeBind(configuration, section, publishConfiguration);
        section.Bind(publishConfiguration);
        return publishConfiguration;
    }

    private static IConfigurationRoot BuildConfiguration(string filePath)
        => RestConfigurationComposition.BuildFromFile(filePath);
}
