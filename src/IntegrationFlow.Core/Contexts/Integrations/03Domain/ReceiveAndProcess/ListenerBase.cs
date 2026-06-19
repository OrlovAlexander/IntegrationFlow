using System;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Cfg;

namespace IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess
{
    /// <summary>
    /// Слушатель входящих сообщений, запросов и т.д.
    /// </summary>
    internal abstract class ListenerBase
    {
        private bool disposed = false; // Для обнаружения избыточных вызовов
        private Thread thread;

        /// <summary>
        /// Конфигурация публикатора, слушателя
        /// </summary>
        protected IConfiguration Configuration { get; private set; }

        /// <summary>
        /// Логгер в рамках интеграций
        /// </summary>
        protected IIntegrationLogger Logger { get; private set; }

        /// <summary>
        /// Публикатор сообщений, запросов и т.п.
        /// </summary>
        protected PublisherBase Publisher { get; private set; }

        /// <summary>
        /// Сторона публикатора сообщений, запросов и т.п.
        /// </summary>
        protected IntegrationPublisherSideBase IntegrationPublisherSide { get; private set; }

        /// <summary>
        /// Ctor
        /// </summary>
        protected ListenerBase()
        {
        }

        /// <summary>
        /// Создать слушателя входящих сообщений, запросов и т.п.
        /// </summary>
        /// <typeparam name="TListener">Тип слушателя</typeparam>
        /// <param name="publisher">Публикатор сообщений, запросов и т.п.</param>
        /// <param name="configuration">Конфигурация публикатора, слушателя</param>
        /// <param name="logger">Логгер в рамках интеграций</param>
        internal static ListenerBase Create<TListener>(PublisherBase publisher, IConfiguration configuration, IIntegrationLogger logger)
        where TListener : ListenerBase, new()
        {
            var listener = (TListener)System.Activator.CreateInstance(typeof(TListener));
            listener.Publisher = publisher;
            listener.Configuration = configuration;
            listener.IntegrationPublisherSide = publisher.IntegrationPublisherSide;
            listener.Logger = logger;
            return listener;
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);

            // Финализатора нет, поэтому не актуально
            // Suppress finalization.
            //GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Запустить слушателя в отдельном потоке
        /// </summary>
        internal void Start(Action postStartAction = null)
        {
            try
            {
                Task.Run(() =>
                    {
                        thread = new Thread(Listen)
                        {
                            IsBackground = true
                        };
                        thread.Start(Configuration);

                        Logger.Log("Поток слушателя запущен '{0}", thread.ManagedThreadId);

                        // Блокируем текущую task, чтобы по завершении вызывать postStartAction
                        thread.Join();
                    })
                    .ContinueWith((task) => postStartAction?.Invoke());
            }
            catch (Exception ex)
            {
                Logger.LogException("Поток слушателя. Ошибка запуска.", ex);
            }
        }

        /// <summary>
        /// Остановить слушателя
        /// </summary>
        internal void Stop()
        {
            try
            {
                Dispose();
                if (thread.ThreadState == ThreadState.Running)
                {
                    thread.Abort();
                    Logger.Log("Поток запускающий слушателя находился в состоянии 'Running'. Был прерван '{0}", thread.ManagedThreadId);
                }
                else
                {
                    Logger.Log("Поток запускающий слушателя '{0} находится в состоянии '{1}'. Не прерван.", thread.ManagedThreadId, thread.ThreadState);
                }
            }
            catch (Exception ex)
            {
                Logger.LogException("Останов потока запускающего слушателя сообщений, запросов и т.п.", ex);
            }
        }

        /// <summary>
        /// Возвращает статус слушателя
        /// </summary>
        internal ListenerStatuses GetStatus()
        {
            var status = ListenerStatuses.NotStarted;
            try
            {
                if (thread.ThreadState == ThreadState.Running)
                {
                    return GetStatusInternal(ListenerStatuses.Started);
                }
            }
            catch (Exception ex)
            {
                Logger.LogException("Статус Слушателя неопрделен. Ошибка. ", ex);
            }
            try
            {
                status = GetStatusInternal(ListenerStatuses.NotStarted);
            }
            catch (Exception ex)
            {
                Logger.LogException("Статус Слушателя неопрделен. Ошибка. ", ex);
            }
            Logger.Log("Статус Слушателя '{0}'", status);
            return status;
        }

        /// <summary>
        /// Освобождение неуправляемых ресурсов
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }
            disposed = true;

            if (disposing)
            {
                DisposeInternal(disposing);
            }
        }

        /// <summary>
        /// Обработать входящее сообщение, запрос и т.п.
        /// </summary>
        protected virtual void ProcessMessage(object message)
        {
            // processor кешируется и по сути singleton
            var processor = IntegrationPublisherSide.GetProcessor(Publisher, Configuration, Logger);
            if (Configuration.Asynchronously)
            {
                ThreadPool.QueueUserWorkItem(processor.Process, message);
            }
            else
            {
                processor.Process(message);
            }
        }

        /// <summary>
        /// Реализация метода слушателя
        /// Реализуется в глобальном модуле
        /// </summary>
        protected abstract void Listen(object configuration);

        /// <summary>
        /// Освобождение ресурсов.
        /// Реализуется в глобальном модуле
        /// </summary>
        /// <param name="disposing">Указывает, откуда осуществляется вызов метода: из метода Dispose (значение true) или из метода завершения (значение false)</param>
        protected abstract void DisposeInternal(bool disposing);

        /// <summary>
        /// Возвращает статус слушателя
        /// </summary>
        /// <param name="listenerStatuses">Состояние слушателя, которое определено в выделенном модуле интеграций.</param>
        /// <returns>Переопределенное в глобальном модуле состояние слушателя</returns>
        protected abstract ListenerStatuses GetStatusInternal(ListenerStatuses listenerStatuses);
    }
}
