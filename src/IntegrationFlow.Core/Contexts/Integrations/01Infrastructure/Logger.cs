using System;
using IntegrationFlow.Contexts.Integrations._03Domain;

namespace IntegrationFlow.Contexts.Integrations._01Infrastructure
{
    /// <summary>
    /// Логгер
    /// </summary>
    internal class Logger : IIntegrationLogger
    {
        /// <summary>
        /// Создать Логгер
        /// </summary>
        internal static IIntegrationLogger Create()
        {
            return new Logger();
        }

        /// <inheritdoc />
        void IIntegrationLogger.LogException(string message, Exception ex)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void LogWarn(string message)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        void IIntegrationLogger.Log(string message)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        void IIntegrationLogger.Log(string format, params object[] args)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void LogInfo(string message)
        {
            throw new NotImplementedException();
        }
    }
}
