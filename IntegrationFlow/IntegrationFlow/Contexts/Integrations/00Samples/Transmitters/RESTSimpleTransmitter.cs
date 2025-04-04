using System;
using System.IO;
using System.Net;
using System.Text;
using IntegrationFlow.Contexts.Integrations._00InnerUsage.TransmitterConfigurations;
using IntegrationFlow.Contexts.Integrations._03Domain;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Cfg;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Transmitter;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.Transmitters
{
    /// <summary>
    /// Способ обращения к противоположной стороне интеграции
    /// </summary>
	public class RESTSimpleTransmitter : ITransmitter
    {
        private ILogger Logger { get; set; }
        private RESTSimpleConfiguration Configuration { get; set; }

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="configuration"></param>
        /// <param name="logger"></param>
        internal RESTSimpleTransmitter(IConfiguration configuration, ILogger logger)
        {
            Logger = logger;
            Configuration = (RESTSimpleConfiguration)configuration;
        }

        /// <summary>
        /// Обратиться к противоположной стороне интеграции
        /// </summary>
        /// <param name="transmitData">Передаваемые данные</param>
        /// <returns>Результат обращения</returns>
		public ObtainedData Transmit(TransmitData transmitData)
        {
            Logger.Log("RESTSimpleTrnasmiter - Trnasmit");

            try
            {
                var myUri = new Uri(Configuration.Url);
                var req = WebRequest.Create(myUri) as HttpWebRequest;
                req.Method = Configuration.Method;
                foreach (var header in Configuration.Headers)
                {
                    req.Headers.Add(header.Key, header.Value);
                }
                req.Accept = Configuration.Accept;
                req.ContentType = Configuration.ContentType;

                //var myProxy = new WebProxy();
                //var newUri = new Uri("http://127.0.0.1:8888");
                //myProxy.Address = newUri;
                //myProxy.BypassProxyOnLocal = false;
                //req.Proxy = myProxy;

                byte[] byteArray = Encoding.UTF8.GetBytes(((string)transmitData.Data));
                req.ContentLength = byteArray.Length;

                using (Stream sendStream = req.GetRequestStream())
                {
                    sendStream.Write(byteArray, 0, byteArray.Length);
                    sendStream.Flush();
                }
                using (var response = (HttpWebResponse)req.GetResponse())
                {
                    using (var resStream = response.GetResponseStream())
                    {
                        using (var sr = new StreamReader(resStream, Encoding.UTF8))
                        {
                            var responseString = sr.ReadToEnd();
                            if (string.IsNullOrEmpty(responseString))
                            {
                                throw new ArgumentNullException(responseString, EleWise.ELMA.SR.T("Получен пустой ответ при запросе {0}", Configuration.Url));
                            }
                            Logger.Log("Ответ: {0}", responseString);
                            return new ObtainedData(responseString);
                        }
                    }
                }
            }
            catch (WebException e)
            {
                Logger.Log("WebException");
                Logger.LogException("", e);
                if (e.Status == WebExceptionStatus.ProtocolError)
                {
                    Logger.Log("The server returned protocol error ");
                    // Get HttpWebResponse so that you can check the HTTP status code.
                    var httpResponse = (HttpWebResponse)e.Response;
                    Logger.Log((int)httpResponse.StatusCode + " - " + httpResponse.StatusCode);
                }
                return new ObtainedData(EmptyIntegrationResult.JsonObject());
            }
            catch (Exception ex)
            {
                Logger.Log("Exception");
                Logger.LogException(EleWise.ELMA.SR.T("{0}({1})", "Произошла ошибка в плагине 'Интеграция' ", Configuration.Url, ex), ex);
                return new ObtainedData(EmptyIntegrationResult.JsonObject());
            }
        }
    }
}
