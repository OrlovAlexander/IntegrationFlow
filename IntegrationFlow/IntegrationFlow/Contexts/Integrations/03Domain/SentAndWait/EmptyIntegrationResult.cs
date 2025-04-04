namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait
{
    /// <summary>
    /// Пустой результат интеграции
    /// </summary>
    public static class EmptyIntegrationResult
    {
        public static string JsonObject()
        {
            return "{}";
        }

        public static string JsonArray()
        {
            return "[]";
        }

        public static byte[] Stream()
        {
            return new byte[0];
        }

        public static object Object()
        {
            return null;
        }
    }
}
