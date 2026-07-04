namespace IntegrationFlow.Contexts.Integrations._03Domain.RpcPending
{
    /// <summary>
    /// Статус async RPC-запроса (request outbox + ожидание response).
    /// </summary>
    public enum RpcPendingStatus
    {
        Pending,
        InFlight,
        AwaitingResponse,
        Completed,
        Failed,
        TimedOut
    }
}
