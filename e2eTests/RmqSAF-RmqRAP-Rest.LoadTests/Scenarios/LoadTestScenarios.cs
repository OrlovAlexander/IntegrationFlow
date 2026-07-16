using NBomber.Contracts;
using NBomber.CSharp;
using RmqSAF_RmqRAP_Rest.LoadTests.Configuration;
using RmqSAF_RmqRAP_Rest.LoadTests.Infrastructure;

namespace RmqSAF_RmqRAP_Rest.LoadTests.Scenarios;

public static class LoadTestScenarios
{
    public static ScenarioProps CreatePublishOnlyScenario(LoadTestSettings settings, E2eApiClient apiClient, CorrelationTracker tracker)
    {
        return Scenario.Create("publish_only", async context =>
            {
                var result = await apiClient
                    .PublishAsync(context.InvocationNumber, context.ScenarioInfo.InstanceId, context.ScenarioCancellationToken)
                    .ConfigureAwait(false);

                if (!result.Success)
                {
                    return Response.Fail(message: result.Error ?? "Publish failed.");
                }

                tracker.Track(result.CorrelationId!);
                return Response.Ok(sizeBytes: result.PayloadSizeBytes);
            })
            .WithWarmUpDuration(TimeSpan.FromSeconds(settings.WarmupSeconds))
            .WithLoadSimulations(
                Simulation.Inject(
                    rate: settings.InjectRate,
                    interval: TimeSpan.FromSeconds(1),
                    during: TimeSpan.FromSeconds(settings.DurationSeconds)));
    }

    public static ScenarioProps CreateEndToEndScenario(LoadTestSettings settings, E2eApiClient apiClient)
    {
        return Scenario.Create("end_to_end", async context =>
            {
                var publish = await apiClient
                    .PublishAsync(context.InvocationNumber, context.ScenarioInfo.InstanceId, context.ScenarioCancellationToken)
                    .ConfigureAwait(false);

                if (!publish.Success)
                {
                    return Response.Fail(message: publish.Error ?? "Publish failed.");
                }

                var delivered = await apiClient
                    .WaitForDeliveryAsync(publish.CorrelationId!, context.ScenarioCancellationToken)
                    .ConfigureAwait(false);

                return delivered
                    ? Response.Ok(sizeBytes: publish.PayloadSizeBytes)
                    : Response.Fail(message: "Payload was not stored before timeout.");
            })
            .WithWarmUpDuration(TimeSpan.FromSeconds(settings.WarmupSeconds))
            .WithLoadSimulations(
                Simulation.Inject(
                    rate: Math.Max(1, settings.InjectRate / 2),
                    interval: TimeSpan.FromSeconds(1),
                    during: TimeSpan.FromSeconds(settings.DurationSeconds)));
    }
}
