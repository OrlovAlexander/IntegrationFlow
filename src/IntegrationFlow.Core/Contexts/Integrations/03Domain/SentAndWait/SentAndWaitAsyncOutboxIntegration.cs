using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IntegrationFlow.Contexts.Integrations._01Infrastructure.Localization;
using IntegrationFlow.Contexts.Integrations._03Domain.Metrics;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait.Cfg;

namespace IntegrationFlow.Contexts.Integrations._03Domain.SentAndWait
{
    /// <summary>
    /// SentAndWait через AsyncOutbox: stage pending в TX приложения, ожидание ответа через <see cref="IRpcPendingStore"/>.
    /// </summary>
    public sealed class SentAndWaitAsyncOutboxIntegration
    {
        private static readonly TimeSpan DefaultWaitTimeout = TimeSpan.FromMinutes(5);

        private readonly SentAndWaitIntegrationOppositeSide oppositeSide;
        private readonly object srcData;
        private readonly IRpcPendingStore pendingStore;
        private readonly IIntegrationLogger logger;
        private readonly IIntegrationFlowMetrics metrics;
        private readonly TimeSpan defaultWaitTimeout;

        internal SentAndWaitAsyncOutboxIntegration(
            SentAndWaitIntegrationOppositeSide integrationOppositeSide,
            object srcData,
            IRpcPendingStore pendingStore,
            IIntegrationLogger logger,
            IIntegrationFlowMetrics? metrics = null,
            TimeSpan? defaultWaitTimeout = null)
        {
            oppositeSide = integrationOppositeSide ?? throw new ArgumentNullException(nameof(integrationOppositeSide));
            this.srcData = srcData;
            this.pendingStore = pendingStore ?? throw new ArgumentNullException(nameof(pendingStore));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.metrics = metrics ?? NullIntegrationFlowMetrics.Instance;
            this.defaultWaitTimeout = defaultWaitTimeout ?? DefaultWaitTimeout;
        }

        /// <summary>
        /// Создаёт pending request для staging в текущей TX (без SaveChanges).
        /// </summary>
        public RpcPendingRequest CreatePendingRequest(Guid? id = null, string contentType = "application/json")
        {
            var transmitData = FormatSourceData();
            var profileName = oppositeSide.GetRpcPendingProfileName(logger);
            if (string.IsNullOrWhiteSpace(profileName))
            {
                throw new InvalidOperationException("Rpc pending profile name is not configured.");
            }

            return new RpcPendingRequest(
                id ?? Guid.NewGuid(),
                profileName,
                SerializePayload(transmitData.Data, contentType),
                contentType,
                DateTimeOffset.UtcNow);
        }

        /// <summary>
        /// Ожидает завершения staged pending и возвращает результат интеграции.
        /// </summary>
        public SentAndWaitIntegrationResult IntegrateWithResult(Guid pendingId, TimeSpan? waitTimeout = null)
            => IntegrateWithResultAsync(pendingId, waitTimeout, CancellationToken.None).GetAwaiter().GetResult();

        /// <summary>
        /// Асинхронно ожидает завершения staged pending и возвращает результат интеграции.
        /// </summary>
        public async Task<SentAndWaitIntegrationResult> IntegrateWithResultAsync(
            Guid pendingId,
            TimeSpan? waitTimeout = null,
            CancellationToken cancellationToken = default)
        {
            var sideCode = oppositeSide.IntegrationOppositeSideCode;
            var profileName = oppositeSide.GetRpcPendingProfileName(logger);
            var timeout = waitTimeout ?? defaultWaitTimeout;

            try
            {
                logger.LogInfo(SR.T("SendAndWait AsyncOutbox - '{0}' - Ожидание pending '{1}'", sideCode, pendingId));

                var pending = await pendingStore
                    .WaitForCompletionAsync(pendingId, timeout, cancellationToken)
                    .ConfigureAwait(false);

                if (pending == null)
                {
                    return SentAndWaitIntegrationResult.Failed($"Pending request '{pendingId}' was not found.");
                }

                if (pending.Status == RpcPendingStatus.TimedOut)
                {
                    metrics.RecordRpcPendingCompleted(profileName, DateTimeOffset.UtcNow - pending.CreatedAt, success: false, timedOut: true);
                    return SentAndWaitIntegrationResult.Timeout(pending.LastError);
                }

                if (pending.Status == RpcPendingStatus.Failed)
                {
                    metrics.RecordRpcPendingCompleted(profileName, DateTimeOffset.UtcNow - pending.CreatedAt, success: false);
                    return SentAndWaitIntegrationResult.Failed(pending.LastError);
                }

                if (pending.Status != RpcPendingStatus.Completed || pending.ResponsePayload == null)
                {
                    return SentAndWaitIntegrationResult.Failed($"Pending request '{pendingId}' did not complete successfully.");
                }

                var obtainedData = CreateObtainedData(pending.ResponsePayload, pending.ContentType);
                var configuration = oppositeSide.GetTransmitterConfiguration(logger);
                if (configuration != null)
                {
                    var validator = oppositeSide.GetValidator(configuration, logger);
                    if (validator != null)
                    {
                        validator.Validate(ref obtainedData);
                    }
                }

                if (obtainedData.IsFailed)
                {
                    metrics.RecordRpcPendingCompleted(profileName, pending.CompletedAt!.Value - pending.CreatedAt, success: false);
                    return SentAndWaitIntegrationResult.Failed("Received data failed validation.");
                }

                var integrationResult = obtainedData;
                var formatterObtainedData = oppositeSide.GetFormatterObtainedData(logger);
                if (formatterObtainedData != null)
                {
                    integrationResult = formatterObtainedData.FormatData(obtainedData);
                }

                metrics.RecordRpcPendingCompleted(
                    profileName,
                    pending.CompletedAt!.Value - pending.CreatedAt,
                    success: true);

                return SentAndWaitIntegrationResult.Succeeded(integrationResult);
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogException(SR.T("SendAndWait AsyncOutbox - '{0}'", sideCode), ex);
                return SentAndWaitIntegrationResult.Failed("Integration was cancelled.", ex);
            }
            catch (Exception ex)
            {
                logger.LogException(SR.T("SendAndWait AsyncOutbox - '{0}'", sideCode), ex);

                if (SentAndWaitIntegrationOptions.ThrowOnFailure)
                {
                    throw;
                }

                return SentAndWaitIntegrationResult.Failed(ex.Message, ex);
            }
            finally
            {
                logger.LogInfo(SR.T("SendAndWait AsyncOutbox - '{0}' - Ожидание завершено", sideCode));
            }
        }

        private TransmitData FormatSourceData()
        {
            var transmitData = new TransmitData(srcData);
            var formatterSourceData = oppositeSide.GetFormatterSourceData(logger);
            return formatterSourceData == null
                ? transmitData
                : formatterSourceData.FormatData(transmitData);
        }

        private static byte[] SerializePayload(object payload, string contentType)
        {
            if (payload == null)
            {
                return Array.Empty<byte>();
            }

            if (payload is byte[] bytes)
            {
                return bytes;
            }

            if (payload is string text)
            {
                return Encoding.UTF8.GetBytes(text);
            }

            return Encoding.UTF8.GetBytes(payload.ToString() ?? string.Empty);
        }

        private static ObtainedData CreateObtainedData(byte[] responsePayload, string contentType)
        {
            if (string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase) ||
                contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
            {
                return new ObtainedData(Encoding.UTF8.GetString(responsePayload));
            }

            return new ObtainedData(responsePayload);
        }
    }
}
