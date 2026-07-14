using IntegrationFlow.Contexts.Integrations._01Infrastructure;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot;
using IntegrationFlow.Contexts.Integrations._03Domain.SentAndForgot.Transmitter;
using RmqSAF_RmqRAP_Rest.Contracts;
using Sender.Api.Infrastructure.IntegrationFlow;

namespace Sender.Api.Domain;

public interface IIntegrationPublisher
{
    PublishResult Publish(PayloadEnvelope payload, CancellationToken cancellationToken);
}

public sealed record PublishResult(string MessageId, string CorrelationId, bool Success, string? FailureReason);
