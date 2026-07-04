namespace IntegrationFlow.Contexts.Integrations._03Domain.RpcPending
{
    /// <summary>
    /// Staging async RPC-запроса без commit — для участия в TX вызывающего DbContext.
    /// </summary>
    public interface IRpcPendingEnqueue
    {
        /// <summary>
        /// Подготовить запрос к сохранению (без SaveChanges).
        /// </summary>
        void Stage(RpcPendingRequest request);
    }
}
