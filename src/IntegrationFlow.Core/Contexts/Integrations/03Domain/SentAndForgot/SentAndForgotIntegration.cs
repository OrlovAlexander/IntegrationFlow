using System;
using System.Threading;
using IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Cfg;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Connection;

namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot
{
    /// <summary>
    /// Интеграция
    /// </summary>
    public sealed class SentAndForgotIntegration
    {
        /// <summary>
        /// Объект синхронизации
        /// </summary>
        private object configConnectSyncObject = new object();

        /// <summary>
        /// Противоположная сторона интеграции
        /// </summary>
        private readonly SentAndForgotIntegrationOppositeSide oppositeSide;

        /// <summary>
        /// Исходные данные
        /// </summary>
        private readonly object srcData;

        /// <summary>
        /// Передаваемые данные
        /// </summary>
        private TransmitData TransmitData
        {
            get
            {
                return new TransmitData(srcData);
            }
        }

        /// <summary>
        /// Логгер в рамках интеграций
        /// </summary>
        private readonly IIntegrationLogger logger;

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="oppositeSide">Противоположная сторона интеграции</param>
        /// <param name="srcData">Исходные данные</param>
        /// <param name="logger">Логгер в рамках интеграций</param>
        internal SentAndForgotIntegration(SentAndForgotIntegrationOppositeSide oppositeSide, object srcData, IIntegrationLogger logger)
        {
            this.oppositeSide = oppositeSide;
            this.srcData = srcData;
            this.logger = logger;
        }

        /// <summary>
        /// Выполнить интеграцию
        /// </summary>
        public void Integrate()
        {
            var sideCode = oppositeSide.IntegrationOppositeSideCode;
            try
            {
                logger.LogInfo(SR.T("SendAndForgot - '{0}' - Интеграция запущена", sideCode));

                var logging = oppositeSide.GetLogging(logger);
                logger.LogInfo(SR.T("SendAndForgot - '{0}' - Получен логгер интеграции: '{1}'", sideCode, logger.GetType().FullName));

                var destinationData = TransmitData;

                // преобразование исходных данных (подготовка передаваемых данных)
                var formatterSourceData = oppositeSide.GetFormatterSourceData(logger);
                if (formatterSourceData != null)
                {
                    logger.LogInfo(SR.T("SendAndForgot - '{0}' - Получен форматировщик передаваемых данных: '{1}'", sideCode, formatterSourceData.GetType().FullName));
                    destinationData = formatterSourceData.FormatData(TransmitData);
                }
                else
                {
                    logger.LogInfo(SR.T("SendAndForgot - '{0}' - Форматировщик передаваемых данных не задан.", sideCode));
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
                    logger.LogInfo(SR.T("SendAndForgot - '{0}' - Взять блокировку", sideCode));

                    Monitor.Enter(configConnectSyncObject, ref lockTaken);

                    // Конфигурация
                    configuration = oppositeSide.GetTransmitterConfiguration(logger);
                    // отмена интеграции, если конфигурация не передана
                    if (configuration == null)
                    {
                        logger.LogInfo(SR.T("SendAndForgot - '{0}' - Конфигурация не предоставлена, отмена интеграции", sideCode));
                        return;
                    }

                    logger.LogInfo(SR.T("SendAndForgot - '{0}' - Конфигурация: '{1}'", sideCode, configuration.GetType().FullName));

                    // Подключение
                    connection = oppositeSide.GetConnection(configuration, logger);
                    // отмена интеграции, если ошибка конфигурации
                    if (connection == null)
                    {
                        logger.LogInfo(SR.T("SendAndForgot - '{0}' - Подключение не предоставлено, отмена интеграции - ", sideCode));
                        return;
                    }

                    logger.LogInfo(SR.T("SendAndForgot - '{0}' - Подключение: '{1}'", sideCode, connection.GetType().FullName));
                }
                finally
                {
                    if (lockTaken)
                    {
                        Monitor.Exit(configConnectSyncObject);
                        logger.LogInfo(SR.T("SendAndForgot - '{0}' - Блокировка освобождена", sideCode));
                    }
                    else
                    {
                        logger.LogInfo(SR.T("SendAndForgot - '{0}' - Блокировка не была получена", sideCode));
                    }
                }

                // Обмен данными
                using (connection = oppositeSide.GetConnection(configuration, logger))
                {
                    if (connection.NeedReconnect())
                    {
                        if (!connection.Reconnect())
                        {
                            logger.LogWarn(SR.T("SendAndForgot - '{0}' - Возникла ошибка переподключения.", sideCode));
                            return;
                        }
                    }

                    // Transmit(configuration, connection, TransmitData);
                    var transmitter = oppositeSide.GetTransmitter(configuration, connection, logger);
                    if (transmitter != null)
                    {
                        transmitter.Transmit(destinationData);
                        logger.LogInfo(SR.T("SendAndForgot - '{0}' - Обмен данными состоялся", sideCode));
                    }
                    else
                    {
                        logger.LogInfo(SR.T("SendAndForgot - '{0}' - Передатчик для обмена данными не предоставлен", sideCode));
                    }

                } // connection Disposing
            }
            catch (Exception ex)
            {
                logger.LogException(SR.T("SendAndForgot - '{0}'", sideCode), ex);
            }
            finally
            {
                logger.LogInfo(SR.T("SendAndForgot - '{0}' - Интеграция завершена", sideCode));
            }
        }
    }
}
