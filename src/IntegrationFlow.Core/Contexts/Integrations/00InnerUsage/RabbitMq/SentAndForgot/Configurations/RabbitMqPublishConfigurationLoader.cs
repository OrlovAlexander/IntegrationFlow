using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.Configurations;
using Microsoft.Extensions.Configuration;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.SentAndForgot.Configurations
{
    /// <summary>
    /// Загрузка настроек публикации в RabbitMQ из JSON-файла.
    /// </summary>
    public static class RabbitMqPublishConfigurationLoader
    {
        /// <summary>
        /// Имя конфигурационного файла по умолчанию.
        /// </summary>
        public const string DefaultFileName = "rabbitmq.json";

        /// <summary>
        /// Имя секции в JSON-файле.
        /// </summary>
        public const string ConfigurationSectionName = "RabbitMqPublish";

        /// <summary>
        /// Имя профиля для legacy flat-конфигурации.
        /// </summary>
        public const string DefaultProfileName = "Default";

        private static readonly HashSet<string> ConnectionPropertyNames = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(RabbitMqPublishConfiguration.HostName),
            nameof(RabbitMqPublishConfiguration.Port),
            nameof(RabbitMqPublishConfiguration.UserName),
            nameof(RabbitMqPublishConfiguration.Password),
            nameof(RabbitMqPublishConfiguration.VirtualHost),
            nameof(RabbitMqPublishConfiguration.QueueName),
            nameof(RabbitMqPublishConfiguration.Exchange),
            nameof(RabbitMqPublishConfiguration.RoutingKey),
            nameof(RabbitMqPublishConfiguration.PublishTarget),
            nameof(RabbitMqPublishConfiguration.ContentType),
            nameof(RabbitMqPublishConfiguration.Persistent),
            nameof(RabbitMqPublishConfiguration.Mandatory),
            nameof(RabbitMqPublishConfiguration.ValidateTopology),
            nameof(RabbitMqPublishConfiguration.AutomaticRecoveryEnabled),
            nameof(RabbitMqPublishConfiguration.ClientProvidedName),
        };

        /// <summary>
        /// Загружает единственный профиль из файла по умолчанию.
        /// </summary>
        public static RabbitMqPublishConfiguration Load()
            => LoadSingle(ResolveConfigFilePath());

        /// <summary>
        /// Загружает единственный профиль из указанного JSON-файла.
        /// </summary>
        public static RabbitMqPublishConfiguration LoadSingle(string filePath)
            => LoadSingleProfile(filePath);

        /// <summary>
        /// Загружает legacy flat-конфигурацию из указанного JSON-файла.
        /// </summary>
        public static RabbitMqPublishConfiguration LoadFromFile(string filePath)
        {
            var configuration = BuildConfiguration(filePath);
            var section = configuration.GetSection(ConfigurationSectionName);

            if (!IsLegacyFlatFormat(section))
            {
                throw new InvalidOperationException(
                    $"Файл '{filePath}' содержит именованные профили RabbitMQ publish. Используйте LoadProfile или LoadAll.");
            }

            var publishConfiguration = BindSection(configuration, section);
            publishConfiguration.Name = DefaultProfileName;
            publishConfiguration.Validate();
            return publishConfiguration;
        }

        /// <summary>
        /// Загружает профиль по имени из файла по умолчанию.
        /// </summary>
        public static RabbitMqPublishConfiguration LoadProfile(string profileName)
            => LoadProfile(profileName, ResolveConfigFilePath());

        /// <summary>
        /// Загружает профиль по имени из указанного JSON-файла.
        /// </summary>
        public static RabbitMqPublishConfiguration LoadProfile(string profileName, string filePath)
            => LoadProfile(profileName, BuildConfiguration(filePath));

        /// <summary>
        /// Загружает профиль по имени из <see cref="IConfiguration"/>.
        /// </summary>
        public static RabbitMqPublishConfiguration LoadProfile(string profileName, IConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(profileName))
            {
                throw new ArgumentException("Имя профиля RabbitMQ publish не задано.", nameof(profileName));
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
                    $"Профиль RabbitMQ publish '{profileName}' не найден в конфигурации.");
            }

            return profile;
        }

        /// <summary>
        /// Загружает все профили из файла по умолчанию.
        /// </summary>
        public static IReadOnlyList<RabbitMqPublishConfiguration> LoadAll()
            => LoadAll(ResolveConfigFilePath());

        /// <summary>
        /// Загружает все профили из указанного JSON-файла.
        /// </summary>
        public static IReadOnlyList<RabbitMqPublishConfiguration> LoadAll(string filePath)
            => LoadAll(BuildConfiguration(filePath));

        /// <summary>
        /// Загружает все профили из <see cref="IConfiguration"/>.
        /// </summary>
        public static IReadOnlyList<RabbitMqPublishConfiguration> LoadAll(IConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            return ResolveProfiles(configuration);
        }

        /// <summary>
        /// Заполняет существующий экземпляр единственным профилем из файла по умолчанию.
        /// </summary>
        public static void Populate(RabbitMqPublishConfiguration target)
            => PopulateSingleProfile(target, ResolveConfigFilePath());

        /// <summary>
        /// Заполняет существующий экземпляр legacy flat-конфигурацией из указанного JSON-файла.
        /// </summary>
        public static void PopulateFromFile(RabbitMqPublishConfiguration target, string filePath)
        {
            ValidateTarget(target);
            Copy(LoadFromFile(filePath), target);
        }

        /// <summary>
        /// Заполняет существующий экземпляр профилем по имени из файла по умолчанию.
        /// </summary>
        public static void PopulateProfile(RabbitMqPublishConfiguration target, string profileName)
            => PopulateProfile(target, profileName, ResolveConfigFilePath());

        /// <summary>
        /// Заполняет существующий экземпляр профилем по имени из указанного JSON-файла.
        /// </summary>
        public static void PopulateProfile(RabbitMqPublishConfiguration target, string profileName, string filePath)
            => PopulateProfile(target, profileName, BuildConfiguration(filePath));

        /// <summary>
        /// Заполняет существующий экземпляр профилем по имени из <see cref="IConfiguration"/>.
        /// </summary>
        public static void PopulateProfile(
            RabbitMqPublishConfiguration target,
            string profileName,
            IConfiguration configuration)
        {
            ValidateTarget(target);
            Copy(LoadProfile(profileName, configuration), target);
        }

        /// <summary>
        /// Возвращает путь к конфигурационному файлу RabbitMQ.
        /// </summary>
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

        private static RabbitMqPublishConfiguration LoadSingleProfile(string filePath)
        {
            var profiles = ResolveProfiles(BuildConfiguration(filePath));
            if (profiles.Count == 1)
            {
                return profiles[0];
            }

            throw new InvalidOperationException(
                $"Файл '{filePath}' содержит {profiles.Count} профилей RabbitMQ publish. Используйте LoadProfile(name) или LoadAll().");
        }

        private static void PopulateSingleProfile(RabbitMqPublishConfiguration target, string filePath)
        {
            ValidateTarget(target);
            Copy(LoadSingleProfile(filePath), target);
        }

        private static IReadOnlyList<RabbitMqPublishConfiguration> ResolveProfiles(IConfiguration configuration)
        {
            var section = configuration.GetSection(ConfigurationSectionName);
            if (!section.Exists())
            {
                throw new InvalidOperationException(
                    $"В конфигурационном файле отсутствует секция '{ConfigurationSectionName}'.");
            }

            if (IsLegacyFlatFormat(section))
            {
                var publishConfiguration = BindSection(configuration, section);
                publishConfiguration.Name = DefaultProfileName;
                publishConfiguration.Validate();
                return new[] { publishConfiguration };
            }

            var profiles = new List<RabbitMqPublishConfiguration>();
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
                    $"В секции '{ConfigurationSectionName}' не найдено ни одного профиля RabbitMQ publish.");
            }

            return profiles;
        }

        private static bool IsLegacyFlatFormat(IConfigurationSection publishSection)
        {
            return publishSection.GetChildren()
                .Any(child => ConnectionPropertyNames.Contains(child.Key));
        }

        private static RabbitMqPublishConfiguration BindSection(IConfiguration configuration, IConfigurationSection section)
        {
            var publishConfiguration = new RabbitMqPublishConfiguration();
            RabbitMqConnectionProfileResolver.ApplySharedConnectionBeforeBind(configuration, section, publishConfiguration);
            section.Bind(publishConfiguration);
            return publishConfiguration;
        }

        private static IConfigurationRoot BuildConfiguration(string filePath)
            => RabbitMqConfigurationComposition.BuildFromFile(filePath);

        private static void ValidateTarget(RabbitMqPublishConfiguration target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
        }

        private static void Copy(RabbitMqPublishConfiguration source, RabbitMqPublishConfiguration target)
        {
            target.Name = source.Name;
            target.HostName = source.HostName;
            target.Port = source.Port;
            target.UserName = source.UserName;
            target.Password = source.Password;
            target.VirtualHost = source.VirtualHost;
            target.AutomaticRecoveryEnabled = source.AutomaticRecoveryEnabled;
            target.ClientProvidedName = source.ClientProvidedName;
            target.PublishTarget = source.PublishTarget;
            target.QueueName = source.QueueName;
            target.Exchange = source.Exchange;
            target.RoutingKey = source.RoutingKey;
            target.ContentType = source.ContentType;
            target.Persistent = source.Persistent;
            target.Mandatory = source.Mandatory;
            target.ValidateTopology = source.ValidateTopology;
            target.PublisherConfirmsEnabled = source.PublisherConfirmsEnabled;
            target.ConfirmTimeoutSeconds = source.ConfirmTimeoutSeconds;
            target.ReuseConnection = source.ReuseConnection;
            target.SslEnabled = source.SslEnabled;
            target.SslServerName = source.SslServerName;
        }
    }
}
