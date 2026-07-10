using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Rest.Configurations;

/// <summary>
/// Loads REST inbound webhook settings from rest.json.
/// </summary>
public static class RestWebhookConfigurationLoader
{
    public const string ConfigurationSectionName = "RestWebhooks";
    public const string DefaultProfileName = "Default";

    private static readonly HashSet<string> ProfilePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(RestWebhookConfiguration.Path),
        nameof(RestWebhookConfiguration.MessageIdHeaderName),
        nameof(RestWebhookConfiguration.CorrelationIdHeaderName),
        nameof(RestWebhookConfiguration.MaxBodyBytes),
        nameof(RestWebhookConfiguration.AllowedMethods),
        nameof(RestWebhookConfiguration.RequireMessageId),
        nameof(RestWebhookConfiguration.Asynchronously),
    };

    public static RestWebhookConfiguration LoadProfile(string profileName)
        => LoadProfile(profileName, ResolveConfigFilePath());

    public static RestWebhookConfiguration LoadProfile(string profileName, string filePath)
        => LoadProfile(profileName, BuildConfiguration(filePath));

    public static RestWebhookConfiguration LoadProfile(string profileName, IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            throw new ArgumentException("REST webhook profile name is required.", nameof(profileName));
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
                $"REST webhook profile '{profileName}' was not found in configuration.");
        }

        return profile;
    }

    public static bool TryLoadProfile(string profileName, out RestWebhookConfiguration configuration)
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

    public static bool TryLoadProfile(
        string profileName,
        IConfiguration hostConfiguration,
        out RestWebhookConfiguration configuration)
    {
        configuration = null!;
        try
        {
            var fileConfiguration = BuildConfiguration(ResolveConfigFilePath());
            var profile = ResolveProfiles(fileConfiguration)
                .FirstOrDefault(item => string.Equals(item.Name, profileName, StringComparison.OrdinalIgnoreCase));
            if (profile == null)
            {
                return TryLoadProfileFromConfiguration(profileName, hostConfiguration, out configuration);
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

    public static IReadOnlyList<RestWebhookConfiguration> LoadAll()
        => LoadAll(ResolveConfigFilePath());

    public static IReadOnlyList<RestWebhookConfiguration> LoadAll(string filePath)
        => LoadAll(BuildConfiguration(filePath));

    public static IReadOnlyList<RestWebhookConfiguration> LoadAll(IConfiguration configuration)
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
        out RestWebhookConfiguration configuration)
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

    private static IReadOnlyList<RestWebhookConfiguration> ResolveProfiles(IConfiguration configuration)
    {
        var section = configuration.GetSection(ConfigurationSectionName);
        if (!section.Exists())
        {
            throw new InvalidOperationException(
                $"Configuration section '{ConfigurationSectionName}' is missing.");
        }

        if (IsLegacyFlatFormat(section))
        {
            var webhookConfiguration = BindSection(section);
            webhookConfiguration.Name = DefaultProfileName;
            webhookConfiguration.Validate();
            return new[] { webhookConfiguration };
        }

        var profiles = new List<RestWebhookConfiguration>();
        foreach (var child in section.GetChildren())
        {
            var profile = BindSection(child);
            profile.Name = child.Key;
            profile.Validate();
            profiles.Add(profile);
        }

        if (profiles.Count == 0)
        {
            throw new InvalidOperationException(
                $"No REST webhook profiles found in section '{ConfigurationSectionName}'.");
        }

        return profiles;
    }

    private static bool IsLegacyFlatFormat(IConfigurationSection section)
        => section.GetChildren().Any(child => ProfilePropertyNames.Contains(child.Key));

    private static RestWebhookConfiguration BindSection(IConfigurationSection section)
    {
        var webhookConfiguration = new RestWebhookConfiguration();
        section.Bind(webhookConfiguration);
        return webhookConfiguration;
    }

    private static IConfigurationRoot BuildConfiguration(string filePath)
        => RestConfigurationComposition.BuildFromFile(filePath);
}
