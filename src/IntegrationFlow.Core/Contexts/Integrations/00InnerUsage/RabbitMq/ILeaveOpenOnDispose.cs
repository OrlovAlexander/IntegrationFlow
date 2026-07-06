namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;

/// <summary>
/// Connection that must stay open after integration completes (pooled reuse).
/// </summary>
internal interface ILeaveOpenOnDispose
{
    /// <summary>
    /// Skip <see cref="System.IDisposable.Dispose"/> after a single integration call.
    /// </summary>
    bool LeaveOpenOnDispose { get; }
}
