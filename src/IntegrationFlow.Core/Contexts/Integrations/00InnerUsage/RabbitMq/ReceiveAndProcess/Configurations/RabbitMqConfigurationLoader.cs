using System;
using System.IO;
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
        /// Загружает конфигурацию RabbitMQ из файла по умолчанию.
        /// </summary>
        public static RabbitMqConfiguration Load()
            => LoadFromFile(ResolveConfigFilePath());

        /// <summary>
        /// Загружает конфигурацию RabbitMQ из указанного JSON-файла.
        /// </summary>
        /// <param name="filePath">Путь к JSON-файлу конфигурации.</param>
        public static RabbitMqConfiguration LoadFromFile(string filePath)
        {
            var configuration = BuildConfiguration(filePath);
            var rabbitMqConfiguration = new RabbitMqConfiguration();
            configuration.GetSection(ConfigurationSectionName).Bind(rabbitMqConfiguration);
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
        /// Заполняет существующий экземпляр конфигурации значениями из файла по умолчанию.
        /// </summary>
        public static void Populate(RabbitMqConfiguration target)
            => Populate(target, ResolveConfigFilePath());

        /// <summary>
        /// Заполняет существующий экземпляр конфигурации значениями из указанного JSON-файла.
        /// </summary>
        /// <param name="target">Экземпляр конфигурации для заполнения.</param>
        /// <param name="filePath">Путь к JSON-файлу конфигурации.</param>
        public static void Populate(RabbitMqConfiguration target, string filePath)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var loaded = LoadFromFile(filePath);
            Copy(loaded, target);
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

        private static void Copy(RabbitMqConfiguration source, RabbitMqConfiguration target)
        {
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
        }
    }
}
