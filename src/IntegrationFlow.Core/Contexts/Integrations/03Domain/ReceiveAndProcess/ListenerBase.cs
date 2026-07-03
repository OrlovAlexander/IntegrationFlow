using System;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;
using IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess.Cfg;

namespace IntegrationFlow.Contexts.Integrations._03Domain.ReceiveAndProcess
{
    /// <summary>
    /// Слушатель входящих сообщений, запросов и т.д.
    /// </summary>
    internal abstract class ListenerBase
    {
        private bool disposed;
        private CancellationTokenSource? runCts;
        private Task? runTask;

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
        internal static ListenerBase Create<TListener>(PublisherBase publisher, IConfiguration configuration, IIntegrationLogger logger)
            where TListener : ListenerBase, new()
        {
            var listener = (TListener)Activator.CreateInstance(typeof(TListener))!;
            listener.Publisher = publisher;
            listener.Configuration = configuration;
            listener.IntegrationPublisherSide = publisher.IntegrationPublisherSide;
            listener.Logger = logger;
            return listener;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Запустить слушателя в фоновой задаче.
        /// </summary>
        internal void Start(Action? postStartAction = null)
        {
            try
            {
                runCts?.Cancel();
                runCts?.Dispose();
                runCts = new CancellationTokenSource();
                runTask = RunHostedAsync(runCts.Token, postStartAction);
            }
            catch (Exception ex)
            {
                Logger.LogException(SR.T("Поток слушателя. Ошибка запуска."), ex);
            }
        }

        /// <summary>
        /// Остановить слушателя.
        /// </summary>
        internal void Stop()
        {
            try
            {
                StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Logger.LogException(
                    SR.T("Останов потока запускающего слушателя сообщений, запросов и т.п."),
                    ex);
            }
        }

        /// <summary>
        /// Возвращает статус слушателя
        /// </summary>
        internal ListenerStatuses GetStatus()
        {
            try
            {
                var status = GetStatusInternal(ListenerStatuses.NotStarted);
                Logger.Log(SR.T("Статус Слушателя '{0}'", status));
                return status;
            }
            catch (Exception ex)
            {
                Logger.LogException(SR.T("Статус Слушателя неопределен. Ошибка."), ex);
                return ListenerStatuses.NotStarted;
            }
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
        protected virtual Task ProcessMessageAsync(object message, CancellationToken cancellationToken)
        {
            var processor = IntegrationPublisherSide.GetProcessor(Publisher, Configuration, Logger);
            return processor.ProcessMessageAsync(message, cancellationToken);
        }

        /// <summary>
        /// Реализация async lifecycle слушателя.
        /// </summary>
        protected abstract Task RunAsync(CancellationToken cancellationToken, Action? postStartAction);

        /// <summary>
        /// Освобождение ресурсов.
        /// </summary>
        protected abstract void DisposeInternal(bool disposing);

        /// <summary>
        /// Возвращает статус слушателя
        /// </summary>
        protected abstract ListenerStatuses GetStatusInternal(ListenerStatuses listenerStatuses);

        private async Task RunHostedAsync(CancellationToken cancellationToken, Action? postStartAction)
        {
            try
            {
                await RunAsync(cancellationToken, postStartAction).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Logger.LogException(SR.T("Поток слушателя. Ошибка выполнения."), ex);
            }
        }

        private async Task StopAsync()
        {
            if (runCts != null)
            {
                runCts.Cancel();
            }

            if (runTask != null)
            {
#if NET8_0_OR_GREATER
                await runTask.WaitAsync(TimeSpan.FromSeconds(30)).ConfigureAwait(false);
#else
                var completed = await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(30)))
                    .ConfigureAwait(false);
                if (completed != runTask)
                {
                    Logger.LogWarn(SR.T("Слушатель не завершился за 30 секунд."));
                }
#endif
            }

            Dispose();
        }
    }
}
