using System;
using System.Collections.Generic;
using IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Cfg;
using IntegrationFlow.Contexts.Integrations._03Domain.Services.ObjectsComparerService;

namespace IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess
{
    /// <summary>
    /// Публикатор сообщений, запросов и т.п.
    /// </summary>
    public abstract class PublisherBase
    {
        /// <summary>
        /// Сторона публикатора сообщений, запросов и т.п.
        /// </summary>
        public IntegrationPublisherSideBase IntegrationPublisherSide { get; private set; }

        /// <summary>
        /// Логгер в рамках интеграций
        /// </summary>
        protected IIntegrationLogger Logger { get; private set; }

        /// <summary>
        /// Optional metrics hooks for message processing.
        /// </summary>
        public IIntegrationFlowMetrics? Metrics { get; set; }

        /// <summary>
        /// Конфигурация слушателя публикатора
        /// </summary>
        protected IConfiguration Configuration { get; set; }

        /// <summary>
        /// Слушатель сообщений, запросов и т.п.
        /// </summary>
        private ListenerBase Listener { get; set; }

        /// <summary>
        /// Ctor
        /// </summary>
        protected PublisherBase()
        {
        }

        /// <summary>
        /// Создать публикатор сообщений, запросов и т.п.
        /// Вызывается из реализации точки расширения IReceiveAndProcessLauncher в глобальном модуле
        /// </summary>
        /// <typeparam name="TPublisherBase">Тип публикатора</typeparam>
        /// <typeparam name="TIntegrationPublisherSideBase">Рабочая среда публикатора сообщений, запросов и т.п.</typeparam>
        /// <param name="logger">Логгер в рамках интеграций</param>
        /// <returns>Публикатор сообщений, запросов и т.п.</returns>
        public static PublisherBase Create<TPublisherBase, TIntegrationPublisherSideBase>(IIntegrationLogger logger)
            where TPublisherBase : PublisherBase, new()
            where TIntegrationPublisherSideBase : IntegrationPublisherSideBase, new()
        {
            var side = new TIntegrationPublisherSideBase();
            var cacheKey = BuildPublisherCacheKey<TPublisherBase>(side);

            return TypeCollection<TPublisherBase>.GetOrAdd(logger, cacheKey, () =>
                CreatePublisher<TPublisherBase>(side, logger));
        }

        /// <summary>
        /// Создать публикатор сообщений, запросов и т.п. с явно переданной стороной интеграции.
        /// </summary>
        /// <typeparam name="TPublisherBase">Тип публикатора</typeparam>
        /// <param name="logger">Логгер в рамках интеграций</param>
        /// <param name="integrationPublisherSide">Сторона публикатора сообщений, запросов и т.п.</param>
        /// <returns>Публикатор сообщений, запросов и т.п.</returns>
        public static PublisherBase Create<TPublisherBase>(
            IIntegrationLogger logger,
            IntegrationPublisherSideBase integrationPublisherSide)
            where TPublisherBase : PublisherBase, new()
        {
            if (integrationPublisherSide == null)
            {
                throw new ArgumentNullException(nameof(integrationPublisherSide));
            }

            var cacheKey = BuildPublisherCacheKey<TPublisherBase>(integrationPublisherSide);

            return TypeCollection<TPublisherBase>.GetOrAdd(logger, cacheKey, () =>
                CreatePublisher<TPublisherBase>(integrationPublisherSide, logger));
        }

        private static TPublisherBase CreatePublisher<TPublisherBase>(
            IntegrationPublisherSideBase side,
            IIntegrationLogger logger)
            where TPublisherBase : PublisherBase, new()
        {
            var publisher = new TPublisherBase
            {
                IntegrationPublisherSide = side,
                Logger = logger
            };

            side.Publisher = publisher;

            logger.Log(SR.T("ReceiveAndProcess.PublisherBase - Create - Создать публикатор сообщений - '{0}'",
                typeof(TPublisherBase).FullName));

            return publisher;
        }

        private static string BuildPublisherCacheKey<TPublisherBase>(IntegrationPublisherSideBase side)
            => $"{typeof(TPublisherBase).AssemblyQualifiedName}|{side.GetPublisherCacheKey()}";

        /// <summary>
        /// Начать получать сообщения, запросы и т.п. от публикатора сообщений
        /// Вызывается из реализации точки расширения IReceiveAndProcessLauncher в глобальном модуле
        /// </summary>
#if NET8_0_OR_GREATER
        [Obsolete("Use AddIntegrationFlowRabbitMqListener() and IHost instead.")]
#endif
        public virtual void BeginReceiving(Action postStartAction = null)
        {
            try
            {
                var configuration = IntegrationPublisherSide.GetConfiguration(this, Logger);

                if (Listener == null)
                {
                    Logger.Log(SR.T("ReceiveAndProcess.PublisherBase - BeginReceiving - Первый запуск с заданной конфигурацией подключения."));
                    // Новая конфигурация
                    Configuration = configuration;
                    // Новая конфигурация передается в Слушатель
                    Listener = IntegrationPublisherSide.GetListener(this, configuration, Logger);
                    Listener.Start(postStartAction);
                    return;
                }

                var status = Listener.GetStatus();

                // Проверка валидности конфигурации
                var configurationChanged = ConfigurationChanged(configuration);
                if (!configurationChanged.HasValue)
                {
                    // Прерываем запуск интеграции
                    Logger.LogWarn(SR.T("ReceiveAndProcess.PublisherBase - BeginReceiving - Отмена запуска"));
                    return;
                }

                // Слушатель есть, запущен и есть изменения в конфигурации
                if (status == ListenerStatuses.Started && configurationChanged.Value)
                {
                    Logger.Log(SR.T("ReceiveAndProcess.PublisherBase - BeginReceiving - Перезапуск запущенного слушателя публикатора с новой конфигурацией подключения."));
                    // Остановить Слушатель с прежней конфигурацией
                    Listener.Stop();
                    // Новая конфигурация передается в Слушатель
                    Listener = IntegrationPublisherSide.GetListener(this, configuration, Logger);
                    Listener.Start(postStartAction);
                }
                // Слушатель есть, но не запущен и есть изменения в конфигурации
                if (status == ListenerStatuses.NotStarted && configurationChanged.Value)
                {
                    Logger.Log(SR.T("ReceiveAndProcess.PublisherBase - BeginReceiving - Перезапуск остановленного слушателя публикатора с новой конфигурацией подключения."));
                    // Остановить Слушатель с прежней конфигурацией
                    Listener.Stop();
                    // Новая конфигурация передается в Слушатель
                    Listener = IntegrationPublisherSide.GetListener(this, configuration, Logger);
                    Listener.Start(postStartAction);
                }
                //// Слушатель есть, но не запущен и нет изменений в конфигурации
                //if (status == ListenerStatuses.NotStarted && !WasConfigurationChanged(configuration))
                //{
                //    Logger.Log("Публикатор. Перезапуск остановленного слушателя публикатора с прежней конфигурацией подключения.");
                //    // Остановить Слушатель с прежней конфигурацией
                //    Listener.Stop();
                //    // Прежняя конфигурация передается в Слушатель
                //    Listener = IntegrationPublisherSide.GetListener(this, configuration, Logger);
                //    Listener.Start();
                //}
                //// Слушатель есть, запущен и нет изменений в конфигурации
                //if (status == ListenerStatuses.Started && !WasConfigurationChanged(configuration))
                //{
                //    return;
                //}
            }
            catch (Exception ex)
            {
                Logger.LogException(SR.T("ReceiveAndProcess.PublisherBase - BeginReceiving - Запуск/перезапуск слушателя."), ex);
            }
        }

        /// <summary>
        /// Остановить прослушивание входящих сообщений (legacy path).
        /// </summary>
        public virtual void StopReceiving()
        {
            try
            {
                Listener?.Stop();
                Listener = null;
            }
            catch (Exception ex)
            {
                Logger.LogException(SR.T("ReceiveAndProcess.PublisherBase - StopReceiving - Остановка слушателя."), ex);
            }
        }

        /// <summary>
        /// Изменилась-ли конфигурация слушателя публикатора
        /// </summary>
        /// <param name="configuration">Новая конфигурация</param>
        /// <returns>True - конфигурация изменилась</returns>
        protected virtual bool? ConfigurationChanged(IConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new InvalidOperationException(SR.T("ReceiveAndProcess.PublisherBase - ConfigurationChanged - Конфигурация не задана."));
            }

            if (Configuration != null && Configuration.GetType() != configuration.GetType())
            {
                Logger.Log(SR.T("ReceiveAndProcess.PublisherBase - ConfigurationChanged - Ошибка конфигурирования"));
                return null;
            }

            var type = configuration.GetType();
            var comparer = new Comparer();
            IEnumerable<Difference> differences;
            if (!comparer.Compare(type, Configuration, configuration, out differences))
            {
                Logger.Log(SR.T("ReceiveAndProcess.PublisherBase - ConfigurationChanged - В конфигурации подключения есть изменения."));

                foreach (var difference in differences)
                {
                    Logger.Log(SR.T(" - Что изменилось '{0}'. Исходное значение '{1}'. Новое значение '{2}'.",
                        difference.MemberPath, difference.Value1, difference.Value2));
                }

                Configuration = configuration;
                return true;
            }

            Logger.Log(SR.T("ReceiveAndProcess.PublisherBase - ConfigurationChanged - Конфигурация подключения осталась прежней."));

            return false;
        }
    }
}
