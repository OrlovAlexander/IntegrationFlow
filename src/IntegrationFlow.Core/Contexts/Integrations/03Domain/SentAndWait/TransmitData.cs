namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait
{
    /// <summary>
    /// Передаваемые данные
    /// </summary>
    public struct TransmitData
    {
        public object Data { get; private set; }

        public TransmitData(object data)
        {
            Data = data;
        }
    }
}
