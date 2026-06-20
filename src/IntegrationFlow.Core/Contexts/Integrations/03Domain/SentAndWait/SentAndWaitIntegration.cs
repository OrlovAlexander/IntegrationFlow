using System;
using System.Threading;
using IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Cfg;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Connection;

namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait
{
    /// <summary>
    /// Интеграция
    /// </summary>
    public sealed class SentAndWaitIntegration
    {
        /// <summary>
        /// Объект синхронизации
        /// </summary>
        private object configConnectSyncObject = new object();

        /// <summary>
        /// Противоположная сторона интеграции
        /// </summary>
        private SentAndWaitIntegrationOppositeSide oppositeSide;

        /// <summary>
        /// Исходные данные
        /// </summary>
        private object srcData;

        /// <summary>
        /// Передаваемые данные
        /// </summary>
        public TransmitData TransmitData
        {
            get
            {
                return new TransmitData(srcData);
            }
        }

        /// <summary>
        /// Логгер
        /// </summary>
        private IIntegrationLogger logger;


        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="integrationOppositeSide">Противоположная сторона интеграции</param>
        /// <param name="srcData">Исходные данные</param>
        internal SentAndWaitIntegration(SentAndWaitIntegrationOppositeSide integrationOppositeSide, object srcData, IIntegrationLogger logger)
        {
            this.oppositeSide = integrationOppositeSide;
            this.srcData = srcData;
            this.logger = logger;
        }

        /// <summary>
        /// Выполнить интеграцию
        /// </summary>
        /// <param name="integrationResultHandler">Обработчик результата</param>
        public void Integrate(SentAndWaitIntegrationResultHandler integrationResultHandler)
        {
            var sideCode = oppositeSide.IntegrationOppositeSideCode;
            try
            {
                logger.LogInfo(SR.T("SendAndWait - '{0}' - Интеграция запущена", sideCode));

                var logging = oppositeSide.GetLogging(logger);
                logger.LogInfo(SR.T("SendAndWait - '{0}' - Получен логгер интеграции: '{1}'", sideCode, logger.GetType().FullName));

                var destinationData = TransmitData;

                // преобразование исходных данных (подготовка передаваемых данных)
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

                // логировать передаваемые данные
                if (logging != null && logger != null)
                {
                    logging.LogTransmitData(destinationData);
                }

                // Конфигурирование и подключение выполняются атомарно
                var lockTaken = false;
                IConnection connection;
                IConfiguration configuration;
                try
                {
                    logger.LogInfo(SR.T("SendAndWait - '{0}' - Взять блокировку", sideCode));

                    Monitor.Enter(configConnectSyncObject, ref lockTaken);

                    // Конфигурация
                    configuration = oppositeSide.GetTransmitterConfiguration(logger);
                    // отмена интеграции, если конфигурация не передана
                    if (configuration == null)
                    {
                        logger.LogInfo(SR.T("SendAndWait - '{0}' - Конфигурация не предоставлена, отмена интеграции", sideCode));
                        return;
                    }

                    logger.LogInfo(SR.T("SendAndWait - '{0}' - Конфигурация: '{1}'", sideCode, configuration.GetType().FullName));

                    // Подключение
                    connection = oppositeSide.GetConnection(configuration, logger);
                    // отмена интеграции, если ошибка конфигурации
                    if (connection == null)
                    {
                        logger.LogInfo(SR.T("SendAndWait - '{0}' - Подключение не предоставлено, отмена интеграции - ", sideCode));
                        return;
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

                // Обмен данными
                var result = new ObtainedData(null);
                using (connection)
                {
                    if (connection.NeedReconnect())
                    {
                        if (!connection.Reconnect())
                        {
                            logger.LogWarn(SR.T("SendAndWait - '{0}' - Возникла ошибка переподключения.", sideCode));
                            return;
                        }
                    }

                    var transmitter = oppositeSide.GetTransmitter(configuration, connection, logger);
                    if (transmitter != null)
                    {
                        result = transmitter.Transmit(destinationData);
                        logger.LogInfo(SR.T("SendAndWait - '{0}' - Обмен данными состоялся", sideCode));
                    }
                    else
                    {
                        logger.LogInfo(SR.T("SendAndWait - '{0}' - Передатчик для обмена данными не предоставлен", sideCode));
                    }
                } // connection Disposing

                // Валидация полученных данных
                var validator = oppositeSide.GetValidator(configuration, logger);
                if (validator != null)
                {
                    validator.Validate(ref result);
                    logger.LogInfo(SR.T("SendAndWait - '{0}' - Валидация полученных данных", sideCode));
                }
                else
                {
                    logger.LogInfo(SR.T("SendAndWait - '{0}' - Валидатор полученных данных не предоставлен", sideCode));
                }

                if (logging != null && logger != null)
                {
                    logging.LogObtainedData(result);
                }

                // Обработка данных не прошедших проверку
                if (result.IsFailed)
                {
                    logger.LogInfo(SR.T("SendAndWait - '{0}' - Полученные данные не прошли валидацию", sideCode));

                    if (integrationResultHandler != null)
                    {
                        integrationResultHandler.ProcessFailedResult(result);
                        return;
                    }

                    throw new NotImplementedException(
                        SR.T("SendAndWait - '{0}' - Отсутствует обработчик полученных данных не прошедших проверку.", sideCode));
                }

                var integrationResult = result;

                // Валидация данных прошедших проверку
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

                if (logging != null && logger != null)
                {
                    logging.LogIntegrationResultData(result);
                }

                // Обработка данных прошедших проверку
                if (integrationResultHandler == null)
                {
                    throw new NotImplementedException(
                        SR.T("SendAndWait - '{0}' - Отсутствует обработчик полученных данных прошедших проверку.", sideCode));
                }
                integrationResultHandler.ProcessResult(integrationResult);
            }
            catch (Exception ex)
            {
                logger.LogException(SR.T("SendAndWait - '{0}'", sideCode), ex);
            }
            finally
            {
                logger.LogInfo(SR.T("SendAndWait - '{0}' - Интеграция завершена", sideCode));
            }
        }
    }
}
