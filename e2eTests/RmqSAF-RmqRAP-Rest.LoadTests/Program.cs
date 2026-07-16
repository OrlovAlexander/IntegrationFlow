using Microsoft.Extensions.Configuration;
using NBomber.Contracts;
using NBomber.Contracts.Stats;
using NBomber.CSharp;
using RmqSAF_RmqRAP_Rest.LoadTests.Configuration;
using RmqSAF_RmqRAP_Rest.LoadTests.Infrastructure;
using RmqSAF_RmqRAP_Rest.LoadTests.Scenarios;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables(prefix: "LOADTEST_")
    .Build();

var settings = configuration.GetSection(LoadTestSettings.SectionName).Get<LoadTestSettings>() ?? new LoadTestSettings();
settings = ApplyCommandLineOverrides(settings, args);

Console.WriteLine("RmqSAF-RmqRAP-Rest load tests");
Console.WriteLine($"  Sender:   {settings.SenderBaseUrl}");
Console.WriteLine($"  Storage:  {settings.StorageBaseUrl}");
Console.WriteLine($"  Scenario: {settings.Scenario}");
Console.WriteLine($"  Rate:     {settings.InjectRate}/s for {settings.DurationSeconds}s (warmup {settings.WarmupSeconds}s)");
Console.WriteLine();

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

await ServiceHealthChecker.EnsureReadyAsync(
    settings.SenderBaseUrl,
    settings.StorageBaseUrl,
    cancellation.Token).ConfigureAwait(false);

using var senderClient = CreateHttpClient(settings.SenderBaseUrl, settings.HttpTimeoutSeconds);
using var storageClient = CreateHttpClient(settings.StorageBaseUrl, settings.HttpTimeoutSeconds);
using var apiClient = new E2eApiClient(
    senderClient,
    storageClient,
    settings.EventType,
    settings.StoragePollIntervalMs,
    settings.StoragePollTimeoutMs);

var tracker = new CorrelationTracker();
var scenarios = new List<ScenarioProps>();

if (settings.Scenario is LoadTestScenarioKind.PublishOnly or LoadTestScenarioKind.Both)
{
    scenarios.Add(LoadTestScenarios.CreatePublishOnlyScenario(settings, apiClient, tracker));
}

if (settings.Scenario is LoadTestScenarioKind.EndToEnd or LoadTestScenarioKind.Both)
{
    scenarios.Add(LoadTestScenarios.CreateEndToEndScenario(settings, apiClient));
}

var runner = NBomberRunner.RegisterScenarios([.. scenarios]);

var nodeStats = runner.Run();

var hasFailures = nodeStats.ScenarioStats.Any(static s => s.AllFailCount > 0);
var exitCode = 0;

if (settings.Scenario is LoadTestScenarioKind.PublishOnly or LoadTestScenarioKind.Both)
{
    var verification = await DeliveryVerificationRunner.VerifyAsync(settings, tracker, cancellation.Token)
        .ConfigureAwait(false);

    PrintDeliveryVerification(verification, settings.MinDeliverySuccessRate);

    if (settings.FailOnThresholdBreach && !verification.Passed)
    {
        exitCode = 2;
    }
}

PrintScenarioSummary(nodeStats);

if (hasFailures)
{
    exitCode = exitCode == 0 ? 1 : exitCode;
}

Environment.Exit(exitCode);

static HttpClient CreateHttpClient(string baseUrl, int timeoutSeconds)
    => new()
    {
        BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
        Timeout = TimeSpan.FromSeconds(timeoutSeconds),
    };

static LoadTestSettings ApplyCommandLineOverrides(LoadTestSettings settings, string[] args)
{
    for (var index = 0; index < args.Length; index++)
    {
        switch (args[index])
        {
            case "--scenario" when index + 1 < args.Length && Enum.TryParse<LoadTestScenarioKind>(args[++index], true, out var scenario):
                settings = settings with { Scenario = scenario };
                break;
            case "--rate" when index + 1 < args.Length && int.TryParse(args[++index], out var rate):
                settings = settings with { InjectRate = rate };
                break;
            case "--duration" when index + 1 < args.Length && int.TryParse(args[++index], out var duration):
                settings = settings with { DurationSeconds = duration };
                break;
            case "--sender" when index + 1 < args.Length:
                settings = settings with { SenderBaseUrl = args[++index] };
                break;
            case "--storage" when index + 1 < args.Length:
                settings = settings with { StorageBaseUrl = args[++index] };
                break;
        }
    }

    return settings;
}

static void PrintDeliveryVerification(DeliveryVerificationResult verification, double minSuccessRate)
{
    Console.WriteLine("Delivery verification:");
    Console.WriteLine($"  Tracked publishes: {verification.TrackedCount}");
    Console.WriteLine($"  Found in storage:  {verification.DeliveredCount}");
    Console.WriteLine($"  Success rate:      {verification.SuccessRate:P1} (min {minSuccessRate:P1})");
    Console.WriteLine($"  Result:            {(verification.Passed ? "PASS" : "FAIL")}");
}

static void PrintScenarioSummary(NodeStats nodeStats)
{
    Console.WriteLine();
    Console.WriteLine("Scenario summary:");

    foreach (var scenario in nodeStats.ScenarioStats)
    {
        Console.WriteLine($"  {scenario.ScenarioName}: ok={scenario.AllOkCount} fail={scenario.AllFailCount} RPS={scenario.Ok.Request.RPS:F1}");
        Console.WriteLine($"    latency p50={scenario.Ok.Latency.Percent50}ms p95={scenario.Ok.Latency.Percent95}ms p99={scenario.Ok.Latency.Percent99}ms");
    }
}
