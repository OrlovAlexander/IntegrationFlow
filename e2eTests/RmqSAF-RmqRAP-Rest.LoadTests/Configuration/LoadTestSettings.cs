namespace RmqSAF_RmqRAP_Rest.LoadTests.Configuration;

public enum LoadTestScenarioKind
{
    PublishOnly,
    EndToEnd,
    Both,
}

public sealed record LoadTestSettings
{
    public const string SectionName = "LoadTest";

    public string SenderBaseUrl { get; init; } = "http://localhost:8080";

    public string StorageBaseUrl { get; init; } = "http://localhost:8081";

    public LoadTestScenarioKind Scenario { get; init; } = LoadTestScenarioKind.Both;

    public int InjectRate { get; init; } = 10;

    public int DurationSeconds { get; init; } = 60;

    public int WarmupSeconds { get; init; } = 5;

    public int StoragePollIntervalMs { get; init; } = 100;

    public int StoragePollTimeoutMs { get; init; } = 30_000;

    public int DeliveryVerificationDelaySeconds { get; init; } = 15;

    public double MinDeliverySuccessRate { get; init; } = 0.95;

    public int HttpTimeoutSeconds { get; init; } = 30;

    public string EventType { get; init; } = "LoadTestEvent";

    public bool FailOnThresholdBreach { get; init; } = true;
}
