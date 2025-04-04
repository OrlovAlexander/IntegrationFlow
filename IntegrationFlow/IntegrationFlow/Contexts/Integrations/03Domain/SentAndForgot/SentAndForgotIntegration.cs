using System;
using System.Threading;
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
        private readonly ILogger logger;

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="oppositeSide">Противоположная сторона интеграции</param>
        /// <param name="srcData">Исходные данные</param>
        /// <param name="logger">Логгер в рамках интеграций</param>
        internal SentAndForgotIntegration(SentAndForgotIntegrationOppositeSide oppositeSide, object srcData, ILogger logger)
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
            try
            {
                logger.LogInfo($"SendAndForgot - '{oppositeSide.IntegrationOppositeSideCode}'" +
                               $" - Интеграция запущена");

                var logging = oppositeSide.GetLogging(logger);
                logger.LogInfo($"SendAndForgot - '{oppositeSide.IntegrationOppositeSideCode}'" +
                               $" - Получен логгер интеграции: '{logger.GetType().FullName}'");

                var destinationData = TransmitData;

                // преобразование исходных данных (подготовка передаваемых данных)
                var formatterSourceData = oppositeSide.GetFormatterSourceData(logger);
                if (formatterSourceData != null)
                {
                    logger.LogInfo($"SendAndForgot - '{oppositeSide.IntegrationOppositeSideCode}'" +
                                   $" - Получен форматировщик передаваемых данных: '{formatterSourceData.GetType().FullName}'");
                    destinationData = formatterSourceData.FormatData(TransmitData);
                }
                else
                {
                    logger.LogInfo($"SendAndForgot - '{oppositeSide.IntegrationOppositeSideCode}'" +
                                   $" - Форматировщик передаваемых данных не задан.");
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
                    logger.LogInfo($"SendAndForgot - '{oppositeSide.IntegrationOppositeSideCode}'" +
                                   $" - Взять блокировку");

                    Monitor.Enter(configConnectSyncObject, ref lockTaken);

                    // Конфигурация
                    configuration = oppositeSide.GetTransmitterConfiguration(logger);
                    // отмена интеграции, если конфигурация не передана
                    if (configuration == null)
                    {
                        logger.LogInfo(
                            $"SendAndForgot - '{oppositeSide.IntegrationOppositeSideCode}'" +
                            $" - Конфигурация не предоставлена, отмена интеграции");
                        return;
                    }

                    logger.LogInfo($"SendAndForgot - '{oppositeSide.IntegrationOppositeSideCode}'" +
                                   $" - Конфигурация: '{configuration.GetType().FullName}'");

                    // Подключение
                    connection = oppositeSide.GetConnection(configuration, logger);
                    // отмена интеграции, если ошибка конфигурации
                    if (connection == null)
                    {
                        logger.LogInfo(
                            $"SendAndForgot - '{oppositeSide.IntegrationOppositeSideCode}'" +
                            $" - Подключение не предоставлено, отмена интеграции - ");
                        return;
                    }

                    logger.LogInfo($"SendAndForgot - '{oppositeSide.IntegrationOppositeSideCode}'" +
                                   $" - Подключение: '{connection.GetType().FullName}'");
                }
                finally
                {
                    if (lockTaken)
                    {
                        Monitor.Exit(configConnectSyncObject);
                        logger.LogInfo(
                            $"SendAndForgot - '{oppositeSide.IntegrationOppositeSideCode}'" +
                            $" - Блокировка освобождена");
                    }
                    else
                    {
                        logger.LogInfo(
                            $"SendAndForgot - '{oppositeSide.IntegrationOppositeSideCode}'" +
                            $" - Блокировка не была получена");
                    }
                }

                // Обмен данными
                using (connection = oppositeSide.GetConnection(configuration, logger))
                {
                    if (connection.NeedReconnect())
                    {
                        if (!connection.Reconnect())
                        {
                            logger.LogWarn(
                                $"SendAndForgot - '{oppositeSide.IntegrationOppositeSideCode}'" +
                                $" - Возникла ошибка переподключения.");
                            return;
                        }
                    }

                    // Transmit(configuration, connection, TransmitData);
                    var transmitter = oppositeSide.GetTransmitter(configuration, connection, logger);
                    if (transmitter != null)
                    {
                        transmitter.Transmit(destinationData);
                        logger.LogInfo(
                            $"SendAndForgot - '{oppositeSide.IntegrationOppositeSideCode}'" +
                            $" - Обмен данными состоялся");
                    }
                    else
                    {
                        logger.LogInfo(
                            $"SendAndForgot - '{oppositeSide.IntegrationOppositeSideCode}'" +
                            $" - Передатчик для обмена данными не предоставлен");
                    }

                } // connection Disposing
            }
            catch (Exception ex)
            {
                logger.LogException($"SendAndForgot - '{oppositeSide.IntegrationOppositeSideCode}'", ex);
            }
            finally
            {
                logger.LogInfo($"SendAndForgot - '{oppositeSide.IntegrationOppositeSideCode}'" +
                               $" - Интеграция завершена");
            }
        }
    }
}
