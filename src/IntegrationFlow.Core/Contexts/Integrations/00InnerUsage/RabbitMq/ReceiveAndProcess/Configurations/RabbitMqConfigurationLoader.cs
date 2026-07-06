using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq.ReceiveAndProcess.Configurations
{
    /// <summary>
    /// Загрузка настроек подключения к RabbitMQ из JSON-файла.
    /// </summary>
    public static class RabbitMqConfigurationLoader
    {
        /// <summary>
        /// Имя конфигурационного файла по умолчанию.
        /// </summary>
        public const string DefaultFileName = "rabbitmq.json";

        /// <summary>
        /// Имя секции в JSON-файле.
        /// </summary>
        public const string ConfigurationSectionName = "RabbitMq";

        /// <summary>
        /// Имя профиля для legacy flat-конфигурации.
        /// </summary>
        public const string DefaultProfileName = "Default";

        private static readonly HashSet<string> ConnectionPropertyNames = new(StringComparer.OrdinalIgnoreCase)
        {
            nameof(RabbitMqConfiguration.HostName),
            nameof(RabbitMqConfiguration.Port),
            nameof(RabbitMqConfiguration.UserName),
            nameof(RabbitMqConfiguration.Password),
            nameof(RabbitMqConfiguration.VirtualHost),
            nameof(RabbitMqConfiguration.QueueName),
            nameof(RabbitMqConfiguration.PrefetchCount),
            nameof(RabbitMqConfiguration.Asynchronously),
            nameof(RabbitMqConfiguration.AutomaticRecoveryEnabled),
            nameof(RabbitMqConfiguration.ClientProvidedName),
        };

        /// <summary>
        /// Загружает единственный профиль RabbitMQ из файла по умолчанию.
        /// </summary>
        public static RabbitMqConfiguration Load()
            => LoadSingle(ResolveConfigFilePath());

        /// <summary>
        /// Загружает единственный профиль RabbitMQ из указанного JSON-файла.
        /// </summary>
        /// <param name="filePath">Путь к JSON-файлу конфигурации.</param>
        public static RabbitMqConfiguration LoadSingle(string filePath)
            => LoadSingleProfile(filePath);

        /// <summary>
        /// Загружает legacy flat-конфигурацию RabbitMQ из указанного JSON-файла.
        /// </summary>
        /// <param name="filePath">Путь к JSON-файлу конфигурации.</param>
        public static RabbitMqConfiguration LoadFromFile(string filePath)
        {
            var configuration = BuildConfiguration(filePath);
            var section = configuration.GetSection(ConfigurationSectionName);

            if (!IsLegacyFlatFormat(section))
            {
                throw new InvalidOperationException(
                    $"Файл '{filePath}' содержит именованные профили RabbitMQ. Используйте LoadProfile или LoadAll.");
            }

            var rabbitMqConfiguration = BindSection(section);
            rabbitMqConfiguration.Name = DefaultProfileName;
            return rabbitMqConfiguration;
        }

        /// <summary>
        /// Загружает конфигурацию RabbitMQ из указанного JSON-файла.
        /// </summary>
        /// <param name="filePath">Путь к JSON-файлу конфигурации.</param>
        [Obsolete("Используйте LoadFromFile(string filePath). Метод будет удалён после добавления LoadProfile(string profileName).")]
        public static RabbitMqConfiguration Load(string filePath)
            => LoadFromFile(filePath);

        /// <summary>
        /// Загружает профиль RabbitMQ по имени из файла по умолчанию.
        /// </summary>
        /// <param name="profileName">Имя профиля.</param>
        public static RabbitMqConfiguration LoadProfile(string profileName)
            => LoadProfile(profileName, ResolveConfigFilePath());

        /// <summary>
        /// Загружает профиль RabbitMQ по имени из указанного JSON-файла.
        /// </summary>
        /// <param name="profileName">Имя профиля.</param>
        /// <param name="filePath">Путь к JSON-файлу конфигурации.</param>
        public static RabbitMqConfiguration LoadProfile(string profileName, string filePath)
        {
            if (string.IsNullOrWhiteSpace(profileName))
            {
                throw new ArgumentException("Имя профиля RabbitMQ не задано.", nameof(profileName));
            }

            var profile = ResolveProfiles(BuildConfiguration(filePath))
                .FirstOrDefault(item => string.Equals(item.Name, profileName, StringComparison.OrdinalIgnoreCase));

            if (profile == null)
            {
                throw new InvalidOperationException(
                    $"Профиль RabbitMQ '{profileName}' не найден в файле '{filePath}'.");
            }

            return profile;
        }

        /// <summary>
        /// Загружает все профили RabbitMQ из файла по умолчанию.
        /// </summary>
        public static IReadOnlyList<RabbitMqConfiguration> LoadAll()
            => LoadAll(ResolveConfigFilePath());

        /// <summary>
        /// Загружает все профили RabbitMQ из указанного JSON-файла.
        /// </summary>
        /// <param name="filePath">Путь к JSON-файлу конфигурации.</param>
        public static IReadOnlyList<RabbitMqConfiguration> LoadAll(string filePath)
            => ResolveProfiles(BuildConfiguration(filePath));

        /// <summary>
        /// Заполняет существующий экземпляр единственным профилем из файла по умолчанию.
        /// </summary>
        public static void Populate(RabbitMqConfiguration target)
            => PopulateSingleProfile(target, ResolveConfigFilePath());

        /// <summary>
        /// Заполняет существующий экземпляр legacy flat-конфигурацией из указанного JSON-файла.
        /// </summary>
        /// <param name="target">Экземпляр конфигурации для заполнения.</param>
        /// <param name="filePath">Путь к JSON-файлу конфигурации.</param>
        public static void PopulateFromFile(RabbitMqConfiguration target, string filePath)
        {
            ValidateTarget(target);
            Copy(LoadFromFile(filePath), target);
        }

        /// <summary>
        /// Заполняет существующий экземпляр конфигурации значениями из указанного JSON-файла.
        /// </summary>
        /// <param name="target">Экземпляр конфигурации для заполнения.</param>
        /// <param name="filePath">Путь к JSON-файлу конфигурации.</param>
        [Obsolete("Используйте PopulateFromFile(RabbitMqConfiguration target, string filePath).")]
        public static void Populate(RabbitMqConfiguration target, string filePath)
            => PopulateFromFile(target, filePath);

        /// <summary>
        /// Заполняет существующий экземпляр профилем по имени из файла по умолчанию.
        /// </summary>
        /// <param name="target">Экземпляр конфигурации для заполнения.</param>
        /// <param name="profileName">Имя профиля.</param>
        public static void PopulateProfile(RabbitMqConfiguration target, string profileName)
            => PopulateProfile(target, profileName, ResolveConfigFilePath());

        /// <summary>
        /// Заполняет существующий экземпляр профилем по имени из указанного JSON-файла.
        /// </summary>
        /// <param name="target">Экземпляр конфигурации для заполнения.</param>
        /// <param name="profileName">Имя профиля.</param>
        /// <param name="filePath">Путь к JSON-файлу конфигурации.</param>
        public static void PopulateProfile(RabbitMqConfiguration target, string profileName, string filePath)
        {
            ValidateTarget(target);
            Copy(LoadProfile(profileName, filePath), target);
        }

        /// <summary>
        /// Возвращает путь к конфигурационному файлу RabbitMQ.
        /// </summary>
        /// <param name="filePath">Явно указанный путь или <c>null</c> для поиска файла по умолчанию.</param>
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

        private static RabbitMqConfiguration LoadSingleProfile(string filePath)
        {
            var profiles = ResolveProfiles(BuildConfiguration(filePath));
            if (profiles.Count == 1)
            {
                return profiles[0];
            }

            throw new InvalidOperationException(
                $"Файл '{filePath}' содержит {profiles.Count} профилей RabbitMQ. Используйте LoadProfile(name) или LoadAll().");
        }

        private static void PopulateSingleProfile(RabbitMqConfiguration target, string filePath)
        {
            ValidateTarget(target);
            Copy(LoadSingleProfile(filePath), target);
        }

        private static IReadOnlyList<RabbitMqConfiguration> ResolveProfiles(IConfigurationRoot configuration)
        {
            var section = configuration.GetSection(ConfigurationSectionName);
            if (!section.Exists())
            {
                throw new InvalidOperationException(
                    $"В конфигурационном файле отсутствует секция '{ConfigurationSectionName}'.");
            }

            if (IsLegacyFlatFormat(section))
            {
                var rabbitMqConfiguration = BindSection(section);
                rabbitMqConfiguration.Name = DefaultProfileName;
                return new[] { rabbitMqConfiguration };
            }

            var profiles = new List<RabbitMqConfiguration>();
            foreach (var child in section.GetChildren())
            {
                var profile = BindSection(child);
                profile.Name = child.Key;
                profiles.Add(profile);
            }

            if (profiles.Count == 0)
            {
                throw new InvalidOperationException(
                    $"В секции '{ConfigurationSectionName}' не найдено ни одного профиля RabbitMQ.");
            }

            return profiles;
        }

        private static bool IsLegacyFlatFormat(IConfigurationSection rabbitMqSection)
        {
            return rabbitMqSection.GetChildren()
                .Any(child => ConnectionPropertyNames.Contains(child.Key));
        }

        private static RabbitMqConfiguration BindSection(IConfiguration section)
        {
            var rabbitMqConfiguration = new RabbitMqConfiguration();
            section.Bind(rabbitMqConfiguration);
            return rabbitMqConfiguration;
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

        private static void ValidateTarget(RabbitMqConfiguration target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
        }

        private static void Copy(RabbitMqConfiguration source, RabbitMqConfiguration target)
        {
            target.Name = source.Name;
            target.Asynchronously = source.Asynchronously;
            target.HostName = source.HostName;
            target.Port = source.Port;
            target.UserName = source.UserName;
            target.Password = source.Password;
            target.VirtualHost = source.VirtualHost;
            target.QueueName = source.QueueName;
            target.PrefetchCount = source.PrefetchCount;
            target.AutomaticRecoveryEnabled = source.AutomaticRecoveryEnabled;
            target.ClientProvidedName = source.ClientProvidedName;
            target.RequeueOnFailure = source.RequeueOnFailure;
            target.MaxRetryCount = source.MaxRetryCount;
            target.SslEnabled = source.SslEnabled;
            target.SslServerName = source.SslServerName;
        }
    }
}
