using System;

namespace IntegrationFlow.Contexts.Integrations._03Domain
{
    /// <summary>
    /// Логгер в рамках интеграций
    /// </summary>
    internal interface ILogger
    {
        void LogException(string message, Exception ex);
        void LogWarn(string message);
        void Log(string message);
        void Log(string format, params object[] args);
        void LogInfo(string message);
    }

}
