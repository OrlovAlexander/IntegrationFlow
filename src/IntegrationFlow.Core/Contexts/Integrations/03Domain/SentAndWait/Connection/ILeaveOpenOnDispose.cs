namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Connection
{
    /// <summary>
    /// Подключение, которое не должно закрываться после одного вызова интеграции (pooled/scoped).
    /// </summary>
    internal interface ILeaveOpenOnDispose
    {
        /// <summary>
        /// Не вызывать <see cref="System.IDisposable.Dispose"/> после интеграции.
        /// </summary>
        bool LeaveOpenOnDispose { get; }
    }
}
