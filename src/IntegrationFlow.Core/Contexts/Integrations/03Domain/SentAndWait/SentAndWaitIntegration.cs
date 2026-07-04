using System;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Cfg;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Connection;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Transmitter;

namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait
{
    /// <summary>
    /// Интеграция
    /// </summary>
    public sealed class SentAndWaitIntegration
    {
        private readonly object configConnectSyncObject = new();
        private readonly SentAndWaitIntegrationOppositeSide oppositeSide;
        private readonly object srcData;
        private readonly IIntegrationLogger logger;

        internal SentAndWaitIntegration(
            SentAndWaitIntegrationOppositeSide integrationOppositeSide,
            object srcData,
            IIntegrationLogger logger)
        {
            oppositeSide = integrationOppositeSide;
            this.srcData = srcData;
            this.logger = logger;
        }

        /// <summary>
        /// Передаваемые данные
        /// </summary>
        public TransmitData TransmitData => new(srcData);

        /// <summary>
        /// Выполнить интеграцию
        /// </summary>
        public void Integrate(SentAndWaitIntegrationResultHandler integrationResultHandler)
        {
            IntegrateAsync(integrationResultHandler, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Выполнить интеграцию и вернуть результат.
        /// </summary>
        public SentAndWaitIntegrationResult IntegrateWithResult()
            => IntegrateWithResultAsync(CancellationToken.None).GetAwaiter().GetResult();

        /// <summary>
        /// Асинхронно выполнить интеграцию с callback-обработчиком.
        /// </summary>
        public async Task IntegrateAsync(
            SentAndWaitIntegrationResultHandler integrationResultHandler,
            CancellationToken cancellationToken = default)
        {
            if (integrationResultHandler == null)
            {
                throw new ArgumentNullException(nameof(integrationResultHandler));
            }

            var result = await IntegrateWithResultAsync(cancellationToken).ConfigureAwait(false);
            await DispatchResultAsync(result, integrationResultHandler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Асинхронно выполнить интеграцию с async-обработчиком результата.
        /// </summary>
        public async Task IntegrateAsync(
            AsyncSentAndWaitIntegrationResultHandler integrationResultHandler,
            CancellationToken cancellationToken = default)
        {
            if (integrationResultHandler == null)
            {
                throw new ArgumentNullException(nameof(integrationResultHandler));
            }

            var result = await IntegrateWithResultAsync(cancellationToken).ConfigureAwait(false);
            await DispatchResultAsync(result, integrationResultHandler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Асинхронно выполнить интеграцию и вернуть результат.
        /// </summary>
        public async Task<SentAndWaitIntegrationResult> IntegrateWithResultAsync(
            CancellationToken cancellationToken = default)
        {
            var sideCode = oppositeSide.IntegrationOppositeSideCode;
            try
            {
                logger.LogInfo(SR.T("SendAndWait - '{0}' - Интеграция запущена", sideCode));

                var logging = oppositeSide.GetLogging(logger);
                logger.LogInfo(SR.T("SendAndWait - '{0}' - Получен логгер интеграции: '{1}'", sideCode, logger.GetType().FullName));

                var destinationData = TransmitData;

                var formatterSourceData = oppositeSide.GetFormatterSourceData(logger);
                if (formatterSourceData != null)
                {
                    logger.LogInfo(SR.T("SendAndWait - '{0}' - Получен форматировщик передаваемых данных: '{1}'", sideCode, formatterSourceData.GetType().FullName));
                    destinationData = formatterSourceData.FormatData(TransmitData);
                }
                else
                {
                    logger.LogInfo(SR.T("SendAndWait - '{0}' - Форматировщик передаваемых данных не задан.", sideCode));
                }

                if (logging != null)
                {
                    logging.LogTransmitData(destinationData);
                }

                IConnection? connection = null;
                IConfiguration? configuration = null;
                var lockTaken = false;
                try
                {
                    logger.LogInfo(SR.T("SendAndWait - '{0}' - Взять блокировку", sideCode));
                    Monitor.Enter(configConnectSyncObject, ref lockTaken);

                    configuration = oppositeSide.GetTransmitterConfiguration(logger);
                    if (configuration == null)
                    {
                        logger.LogInfo(SR.T("SendAndWait - '{0}' - Конфигурация не предоставлена, отмена интеграции", sideCode));
                        return SentAndWaitIntegrationResult.Failed("Configuration was not provided.");
                    }

                    logger.LogInfo(SR.T("SendAndWait - '{0}' - Конфигурация: '{1}'", sideCode, configuration.GetType().FullName));

                    connection = oppositeSide.GetConnection(configuration, logger);
                    if (connection == null)
                    {
                        logger.LogInfo(SR.T("SendAndWait - '{0}' - Подключение не предоставлено, отмена интеграции - ", sideCode));
                        return SentAndWaitIntegrationResult.Failed("Connection was not provided.");
                    }

                    logger.LogInfo(SR.T("SendAndWait - '{0}' - Подключение: '{1}'", sideCode, connection.GetType().FullName));
                }
                finally
                {
                    if (lockTaken)
                    {
                        Monitor.Exit(configConnectSyncObject);
                        logger.LogInfo(SR.T("SendAndWait - '{0}' - Блокировка освобождена", sideCode));
                    }
                    else
                    {
                        logger.LogInfo(SR.T("SendAndWait - '{0}' - Блокировка не была получена", sideCode));
                    }
                }

                var leaveOpen = connection is ILeaveOpenOnDispose { LeaveOpenOnDispose: true };
                try
                {
                    if (connection.NeedReconnect())
                    {
                        if (!connection.Reconnect())
                        {
                            logger.LogWarn(SR.T("SendAndWait - '{0}' - Возникла ошибка переподключения.", sideCode));
                            return SentAndWaitIntegrationResult.Failed("Reconnect failed.");
                        }
                    }

                    var transmitter = oppositeSide.GetTransmitter(configuration!, connection, logger);
                    if (transmitter == null)
                    {
                        logger.LogInfo(SR.T("SendAndWait - '{0}' - Передатчик для обмена данными не предоставлен", sideCode));
                        return SentAndWaitIntegrationResult.Failed("Transmitter was not provided.");
                    }

                    var result = await TransmitAsync(transmitter, destinationData, cancellationToken).ConfigureAwait(false);
                    logger.LogInfo(SR.T("SendAndWait - '{0}' - Обмен данными состоялся", sideCode));

                    var validator = oppositeSide.GetValidator(configuration!, logger);
                    if (validator != null)
                    {
                        validator.Validate(ref result);
                        logger.LogInfo(SR.T("SendAndWait - '{0}' - Валидация полученных данных", sideCode));
                    }
                    else
                    {
                        logger.LogInfo(SR.T("SendAndWait - '{0}' - Валидатор полученных данных не предоставлен", sideCode));
                    }

                    if (logging != null)
                    {
                        logging.LogObtainedData(result);
                    }

                    if (result.IsFailed)
                    {
                        logger.LogInfo(SR.T("SendAndWait - '{0}' - Полученные данные не прошли валидацию", sideCode));
                        return SentAndWaitIntegrationResult.Failed("Received data failed validation.");
                    }

                    var integrationResult = result;
                    var formatterObtainedData = oppositeSide.GetFormatterObtainedData(logger);
                    if (formatterObtainedData != null)
                    {
                        integrationResult = formatterObtainedData.FormatData(result);
                        logger.LogInfo(SR.T("SendAndWait - '{0}' - Форматировщик полученных данных: '{1}'", sideCode, formatterObtainedData.GetType().FullName));
                    }
                    else
                    {
                        logger.LogInfo(SR.T("SendAndWait - '{0}' - Форматировщик полученных данных не предоставлен", sideCode));
                    }

                    if (logging != null)
                    {
                        logging.LogIntegrationResultData(result);
                    }

                    return SentAndWaitIntegrationResult.Succeeded(integrationResult);
                }
                finally
                {
                    if (!leaveOpen)
                    {
                        connection.Dispose();
                    }
                }
            }
            catch (SentAndWaitTimeoutException ex)
            {
                logger.LogException(SR.T("SendAndWait - '{0}'", sideCode), ex);
                return SentAndWaitIntegrationResult.Timeout(ex.Message, ex);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogException(SR.T("SendAndWait - '{0}'", sideCode), ex);
                return SentAndWaitIntegrationResult.Failed("Integration was cancelled.", ex);
            }
            catch (Exception ex)
            {
                logger.LogException(SR.T("SendAndWait - '{0}'", sideCode), ex);

                if (SentAndWaitIntegrationOptions.ThrowOnFailure)
                {
                    throw;
                }

                return SentAndWaitIntegrationResult.Failed(ex.Message, ex);
            }
            finally
            {
                logger.LogInfo(SR.T("SendAndWait - '{0}' - Интеграция завершена", sideCode));
            }
        }

        private static async Task<ObtainedData> TransmitAsync(
            ITransmitter transmitter,
            TransmitData destinationData,
            CancellationToken cancellationToken)
        {
            if (transmitter is ITransmitterAsync asyncTransmitter)
            {
                return await asyncTransmitter.TransmitAsync(destinationData, cancellationToken).ConfigureAwait(false);
            }

            return await Task.Run(() => transmitter.Transmit(destinationData), cancellationToken).ConfigureAwait(false);
        }

        private static async Task DispatchResultAsync(
            SentAndWaitIntegrationResult result,
            SentAndWaitIntegrationResultHandler integrationResultHandler,
            CancellationToken cancellationToken)
        {
            if (result.Success)
            {
                integrationResultHandler.ProcessResult(result.Data);
                return;
            }

            if (result.Data.IsFailed || !result.Success)
            {
                integrationResultHandler.ProcessFailedResult(result.Data);
            }

            if (SentAndWaitIntegrationOptions.ThrowOnFailure)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (result.TimedOut)
                {
                    throw result.Exception ?? new SentAndWaitTimeoutException(result.FailureReason);
                }

                if (result.Exception != null)
                {
                    throw result.Exception;
                }

                throw new InvalidOperationException(result.FailureReason);
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private static async Task DispatchResultAsync(
            SentAndWaitIntegrationResult result,
            AsyncSentAndWaitIntegrationResultHandler integrationResultHandler,
            CancellationToken cancellationToken)
        {
            if (result.Success)
            {
                await integrationResultHandler.ProcessResultAsync(result.Data, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (result.Data.IsFailed || !result.Success)
            {
                await integrationResultHandler.ProcessFailedResultAsync(result.Data, cancellationToken).ConfigureAwait(false);
            }

            if (SentAndWaitIntegrationOptions.ThrowOnFailure)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (result.TimedOut)
                {
                    throw result.Exception ?? new SentAndWaitTimeoutException(result.FailureReason);
                }

                if (result.Exception != null)
                {
                    throw result.Exception;
                }

                throw new InvalidOperationException(result.FailureReason);
            }
        }
    }
}
