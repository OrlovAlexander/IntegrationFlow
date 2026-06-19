using System;
using System.Threading;
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
            try
            {
                logger.LogInfo($"SendAndWait - '{oppositeSide.IntegrationOppositeSideCode}'" +
                               $" - Интеграция запущена");

                var logging = oppositeSide.GetLogging(logger);
                logger.LogInfo($"SendAndWait - '{oppositeSide.IntegrationOppositeSideCode}'" +
                               $" - Получен логгер интеграции: '{logger.GetType().FullName}'");

                var destinationData = TransmitData;

                // преобразование исходных данных (подготовка передаваемых данных)
                var formatterSourceData = oppositeSide.GetFormatterSourceData(logger);
                if (formatterSourceData != null)
                {
                    logger.LogInfo($"SendAndWait - '{oppositeSide.IntegrationOppositeSideCode}'" +
                                   $" - Получен форматировщик передаваемых данных: '{formatterSourceData.GetType().FullName}'");
                    destinationData = formatterSourceData.FormatData(TransmitData);
                }
                else
                {
                    logger.LogInfo($"SendAndWait - '{oppositeSide.IntegrationOppositeSideCode}'" +
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
                    logger.LogInfo($"SendAndWait - '{oppositeSide.IntegrationOppositeSideCode}'" +
                                   $" - Взять блокировку");

                    Monitor.Enter(configConnectSyncObject, ref lockTaken);

                    // Конфигурация
                    configuration = oppositeSide.GetTransmitterConfiguration(logger);
                    // отмена интеграции, если конфигурация не передана
                    if (configuration == null)
                    {
                        logger.LogInfo(
                            $"SendAndWait - '{oppositeSide.IntegrationOppositeSideCode}'" +
                            $" - Конфигурация не предоставлена, отмена интеграции");
                        return;
                    }

                    logger.LogInfo($"SendAndWait - '{oppositeSide.IntegrationOppositeSideCode}'" +
                                   $" - Конфигурация: '{configuration.GetType().FullName}'");

                    // Подключение
                    connection = oppositeSide.GetConnection(configuration, logger);
                    // отмена интеграции, если ошибка конфигурации
                    if (connection == null)
                    {
                        logger.LogInfo(
                            $"SendAndWait - '{oppositeSide.IntegrationOppositeSideCode}'" +
                            $" - Подключение не предоставлено, отмена интеграции - ");
                        return;
                    }

                    logger.LogInfo($"SendAndWait - '{oppositeSide.IntegrationOppositeSideCode}'" +
                                   $" - Подключение: '{connection.GetType().FullName}'");
                }
                finally
                {
                    if (lockTaken)
                    {
                        Monitor.Exit(configConnectSyncObject);
                        logger.LogInfo(
                            $"SendAndWait - '{oppositeSide.IntegrationOppositeSideCode}'" +
                            $" - Блокировка освобождена");
                    }
                    else
                    {
                        logger.LogInfo(
                            $"SendAndWait - '{oppositeSide.IntegrationOppositeSideCode}'" +
                            $" - Блокировка не была получена");
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
                            logger.LogWarn(
                                $"SendAndWait - '{oppositeSide.IntegrationOppositeSideCode}'" +
                                $" - Возникла ошибка переподключения.");
                            return;
                        }
                    }

                    var transmitter = oppositeSide.GetTransmitter(configuration, connection, logger);
                    if (transmitter != null)
                    {
                        result = transmitter.Transmit(destinationData);
                        logger.LogInfo(
                            $"SendAndWait - '{oppositeSide.IntegrationOppositeSideCode}'" +
                            $" - Обмен данными состоялся");
                    }
                    else
                    {
                        logger.LogInfo(
                            $"SendAndWait - '{oppositeSide.IntegrationOppositeSideCode}'" +
                            $" - Передатчик для обмена данными не предоставлен");
                    }
                } // connection Disposing

                // Валидация полученных данных
                var validator = oppositeSide.GetValidator(configuration, logger);
                if (validator != null)
                {
                    validator.Validate(ref result);
                    logger.LogInfo($"SendAndWait - '{oppositeSide.IntegrationOppositeSideCode}'" +
                                   $" - Валидация полученных данных");
                }
                else
                {
                    logger.LogInfo($"SendAndWait - '{oppositeSide.IntegrationOppositeSideCode}'" +
                                   $" - Валидатор полученных данных не предоставлен");
                }

                if (logging != null && logger != null)
                {
                    logging.LogObtainedData(result);
                }

                // Обработка данных не прошедших проверку
                if (result.IsFailed)
                {
                    logger.LogInfo($"SendAndWait - '{oppositeSide.IntegrationOppositeSideCode}'" +
                                   $" - Полученные данные не прошли валидацию");

                    if (integrationResultHandler != null)
                    {
                        integrationResultHandler.ProcessFailedResult(result);
                        return;
                    }

                    throw new NotImplementedException(
                        $"SendAndWait - '{oppositeSide.IntegrationOppositeSideCode}'" +
                        $" - Отсутствует обработчик полученных данных не прошедших проверку.");
                }

                var integrationResult = result;

                // Валидация данных прошедших проверку
                var formatterObtainedData = oppositeSide.GetFormatterObtainedData(logger);
                if (formatterObtainedData != null)
                {
                    integrationResult = formatterObtainedData.FormatData(result);
                    logger.LogInfo($"SendAndWait - '{oppositeSide.IntegrationOppositeSideCode}'" +
                                   $" - Форматировщик полученных данных: '{formatterObtainedData.GetType().FullName}'");
                }
                else
                {
                    logger.LogInfo($"SendAndWait - '{oppositeSide.IntegrationOppositeSideCode}'" +
                                   $" - Форматировщик полученных данных не предоставлен");
                }

                if (logging != null && logger != null)
                {
                    logging.LogIntegrationResultData(result);
                }

                // Обработка данных прошедших проверку
                if (integrationResultHandler == null)
                {
                    throw new NotImplementedException(
                        $"SendAndWait - '{oppositeSide.IntegrationOppositeSideCode}'" +
                        $" - Отсутствует обработчик полученных данных прошедших проверку.");
                }
                integrationResultHandler.ProcessResult(integrationResult);
            }
            catch (Exception ex)
            {
                logger.LogException(string.Format("SendAndWait - '{0}'",
                        oppositeSide.IntegrationOppositeSideCode.ToString()), ex);
            }
            finally
            {
                logger.LogInfo($"SendAndWait - '{oppositeSide.IntegrationOppositeSideCode}'" +
                               $" - Интеграция завершена");
            }
        }
    }
}
