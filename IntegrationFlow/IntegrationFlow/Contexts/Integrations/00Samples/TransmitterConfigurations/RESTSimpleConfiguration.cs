using System.Collections.Generic;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Cfg;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.TransmitterConfigurations
{
    /// <summary>
    /// Конфигурация для REST (URL, Method, ContentType)
    /// </summary>
    public abstract class RESTSimpleConfiguration : IConfiguration
    {
        public string Url { get; protected set; }
        public string Method { get; protected set; }
        public string Accept { get; protected set; }
        public string ContentType { get; protected set; }
        public Dictionary<string, string> Headers { get; protected set; }

        /// <summary>
        /// Ctor
        /// </summary>
        public RESTSimpleConfiguration()
        {
            Method = "POST";
            Accept = "application/json";
            ContentType = "application/json";
            Headers = new Dictionary<string, string>();
        }
    }
}
