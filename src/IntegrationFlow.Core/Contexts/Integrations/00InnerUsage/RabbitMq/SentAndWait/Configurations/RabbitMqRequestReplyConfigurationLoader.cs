using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndWait.Configurations
{
    /// <summary>
    /// Загрузка настроек request-reply RabbitMQ из JSON-файла.
    /// </summary>
    public static class RabbitMqRequestReplyConfigurationLoader
    {
        public const string DefaultFileName = "rabbitmq.json";
        public const string ConfigurationSectionName = "RabbitMqRequestReply";
        public const string DefaultProfileName = "Default";

        private static readonly HashSet<string> ConnectionPropertyNames = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(RabbitMqRequestReplyConfiguration.HostName),
            nameof(RabbitMqRequestReplyConfiguration.Port),
            nameof(RabbitMqRequestReplyConfiguration.UserName),
            nameof(RabbitMqRequestReplyConfiguration.Password),
            nameof(RabbitMqRequestReplyConfiguration.VirtualHost),
            nameof(RabbitMqRequestReplyConfiguration.QueueName),
            nameof(RabbitMqRequestReplyConfiguration.Exchange),
            nameof(RabbitMqRequestReplyConfiguration.RoutingKey),
            nameof(RabbitMqRequestReplyConfiguration.RequestTarget),
            nameof(RabbitMqRequestReplyConfiguration.ReplyMode),
            nameof(RabbitMqRequestReplyConfiguration.ResponseTimeoutSeconds),
            nameof(RabbitMqRequestReplyConfiguration.ContentType),
            nameof(RabbitMqRequestReplyConfiguration.Persistent),
            nameof(RabbitMqRequestReplyConfiguration.Mandatory),
            nameof(RabbitMqRequestReplyConfiguration.ValidateTopology),
            nameof(RabbitMqRequestReplyConfiguration.AutomaticRecoveryEnabled),
            nameof(RabbitMqRequestReplyConfiguration.ClientProvidedName),
            nameof(RabbitMqRequestReplyConfiguration.MaxConcurrentRequests),
            nameof(RabbitMqRequestReplyConfiguration.ReuseConnection),
            nameof(RabbitMqRequestReplyConfiguration.ReuseReplyConnection),
            nameof(RabbitMqRequestReplyConfiguration.SslEnabled),
            nameof(RabbitMqRequestReplyConfiguration.SslServerName),
            nameof(RabbitMqRequestReplyConfiguration.RequestMode),
            nameof(RabbitMqRequestReplyConfiguration.ResponseQueueName),
            nameof(RabbitMqRequestReplyConfiguration.PendingTimeoutSeconds),
        };

        public static RabbitMqRequestReplyConfiguration Load()
            => LoadSingle(ResolveConfigFilePath());

        public static RabbitMqRequestReplyConfiguration LoadSingle(string filePath)
            => LoadSingleProfile(filePath);

        public static RabbitMqRequestReplyConfiguration LoadFromFile(string filePath)
        {
            var configuration = BuildConfiguration(filePath);
            var section = configuration.GetSection(ConfigurationSectionName);

            if (!IsLegacyFlatFormat(section))
            {
                throw new InvalidOperationException(
                    $"Файл '{filePath}' содержит именованные профили RabbitMQ request-reply. Используйте LoadProfile или LoadAll.");
            }

            var requestReplyConfiguration = BindSection(section);
            requestReplyConfiguration.Name = DefaultProfileName;
            requestReplyConfiguration.Validate();
            return requestReplyConfiguration;
        }

        public static RabbitMqRequestReplyConfiguration LoadProfile(string profileName)
            => LoadProfile(profileName, ResolveConfigFilePath());

        public static RabbitMqRequestReplyConfiguration LoadProfile(string profileName, string filePath)
        {
            if (string.IsNullOrWhiteSpace(profileName))
            {
                throw new ArgumentException("Имя профиля RabbitMQ request-reply не задано.", nameof(profileName));
            }

            var profile = ResolveProfiles(BuildConfiguration(filePath))
                .FirstOrDefault(item => string.Equals(item.Name, profileName, StringComparison.OrdinalIgnoreCase));

            if (profile == null)
            {
                throw new InvalidOperationException(
                    $"Профиль RabbitMQ request-reply '{profileName}' не найден в файле '{filePath}'.");
            }

            return profile;
        }

        public static IReadOnlyList<RabbitMqRequestReplyConfiguration> LoadAll()
            => LoadAll(ResolveConfigFilePath());

        public static IReadOnlyList<RabbitMqRequestReplyConfiguration> LoadAll(string filePath)
            => ResolveProfiles(BuildConfiguration(filePath));

        public static void Populate(RabbitMqRequestReplyConfiguration target)
            => PopulateSingleProfile(target, ResolveConfigFilePath());

        public static void PopulateFromFile(RabbitMqRequestReplyConfiguration target, string filePath)
        {
            ValidateTarget(target);
            Copy(LoadFromFile(filePath), target);
        }

        public static void PopulateProfile(RabbitMqRequestReplyConfiguration target, string profileName)
            => PopulateProfile(target, profileName, ResolveConfigFilePath());

        public static void PopulateProfile(RabbitMqRequestReplyConfiguration target, string profileName, string filePath)
        {
            ValidateTarget(target);
            Copy(LoadProfile(profileName, filePath), target);
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
                $"Не найден конфигурационный файл RabbitMQ '{DefaultFileName}'. " +
                $"Ожидается в '{AppContext.BaseDirectory}' или '{Directory.GetCurrentDirectory()}'.");
        }

        private static RabbitMqRequestReplyConfiguration LoadSingleProfile(string filePath)
        {
            var profiles = ResolveProfiles(BuildConfiguration(filePath));
            if (profiles.Count == 1)
            {
                return profiles[0];
            }

            throw new InvalidOperationException(
                $"Файл '{filePath}' содержит {profiles.Count} профилей RabbitMQ request-reply. Используйте LoadProfile(name) или LoadAll().");
        }

        private static void PopulateSingleProfile(RabbitMqRequestReplyConfiguration target, string filePath)
        {
            ValidateTarget(target);
            Copy(LoadSingleProfile(filePath), target);
        }

        private static IReadOnlyList<RabbitMqRequestReplyConfiguration> ResolveProfiles(IConfigurationRoot configuration)
        {
            var section = configuration.GetSection(ConfigurationSectionName);
            if (!section.Exists())
            {
                throw new InvalidOperationException(
                    $"В конфигурационном файле отсутствует секция '{ConfigurationSectionName}'.");
            }

            if (IsLegacyFlatFormat(section))
            {
                var requestReplyConfiguration = BindSection(section);
                requestReplyConfiguration.Name = DefaultProfileName;
                requestReplyConfiguration.Validate();
                return new[] { requestReplyConfiguration };
            }

            var profiles = new List<RabbitMqRequestReplyConfiguration>();
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
                    $"В секции '{ConfigurationSectionName}' не найдено ни одного профиля RabbitMQ request-reply.");
            }

            return profiles;
        }

        private static bool IsLegacyFlatFormat(IConfigurationSection section)
        {
            return section.GetChildren()
                .Any(child => ConnectionPropertyNames.Contains(child.Key));
        }

        private static RabbitMqRequestReplyConfiguration BindSection(IConfiguration section)
        {
            var requestReplyConfiguration = new RabbitMqRequestReplyConfiguration();
            section.Bind(requestReplyConfiguration);
            return requestReplyConfiguration;
        }

        private static IConfigurationRoot BuildConfiguration(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Не найден конфигурационный файл RabbitMQ '{filePath}'.", filePath);
            }

            var directory = Path.GetDirectoryName(filePath)
                ?? throw new InvalidOperationException($"Не удалось определить каталог конфигурационного файла '{filePath}'.");

            var fileName = Path.GetFileName(filePath);

            return new ConfigurationBuilder()
                .SetBasePath(directory)
                .AddJsonFile(fileName, optional: false, reloadOnChange: false)
                .Build();
        }

        private static void ValidateTarget(RabbitMqRequestReplyConfiguration target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
        }

        private static void Copy(RabbitMqRequestReplyConfiguration source, RabbitMqRequestReplyConfiguration target)
        {
            target.Name = source.Name;
            target.HostName = source.HostName;
            target.Port = source.Port;
            target.UserName = source.UserName;
            target.Password = source.Password;
            target.VirtualHost = source.VirtualHost;
            target.AutomaticRecoveryEnabled = source.AutomaticRecoveryEnabled;
            target.ClientProvidedName = source.ClientProvidedName;
            target.RequestTarget = source.RequestTarget;
            target.QueueName = source.QueueName;
            target.Exchange = source.Exchange;
            target.RoutingKey = source.RoutingKey;
            target.ReplyMode = source.ReplyMode;
            target.ResponseTimeoutSeconds = source.ResponseTimeoutSeconds;
            target.ContentType = source.ContentType;
            target.Persistent = source.Persistent;
            target.Mandatory = source.Mandatory;
            target.ValidateTopology = source.ValidateTopology;
            target.MaxConcurrentRequests = source.MaxConcurrentRequests;
            target.ReuseConnection = source.ReuseConnection;
            target.ReuseReplyConnection = source.ReuseReplyConnection;
            target.ManualReplyAck = source.ManualReplyAck;
            target.SslEnabled = source.SslEnabled;
            target.SslServerName = source.SslServerName;
        }
    }
}
