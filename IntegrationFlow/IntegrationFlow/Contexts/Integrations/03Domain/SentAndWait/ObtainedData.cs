namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait
{
    /// <summary>
    /// Результат интеграции
    /// </summary>
    public struct ObtainedData
    {
        public object Data { get; private set; }
        
        public bool IsFailed { get; private set; }

        public ObtainedData(object data)
        {
            Data = data;
            IsFailed = false;
        }

        public ObtainedData(object data, bool isFailed)
        {
            Data = data;
            IsFailed = isFailed;
        }

        public void SetFailed()
        {
            IsFailed = true;
        }
    }
}
