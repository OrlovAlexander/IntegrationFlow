using System;
using IntegrationFlow.Contexts.Integrations._03Domain;

namespace IntegrationFlow.Contexts.Integrations._01Infrastructure
{
    /// <summary>
    /// Логгер
    /// </summary>
    internal class Logger : ILogger
    {
        /// <summary>
        /// Создать Логгер
        /// </summary>
        internal static ILogger Create()
        {
            return new Logger();
        }

        /// <inheritdoc />
        void ILogger.LogException(string message, Exception ex)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void LogWarn(string message)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        void ILogger.Log(string message)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        void ILogger.Log(string format, params object[] args)
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
