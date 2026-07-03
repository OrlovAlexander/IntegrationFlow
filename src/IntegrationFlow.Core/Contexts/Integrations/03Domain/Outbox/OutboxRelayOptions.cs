using System;

namespace IntegrationFlow.Contexts.Integrations._03Domain.Outbox
{
    /// <summary>
    /// Настройки фонового relay outbox-сообщений.
    /// </summary>
    public sealed class OutboxRelayOptions
    {
        public int BatchSize { get; set; } = 20;

        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);

        public TimeSpan LockDuration { get; set; } = TimeSpan.FromSeconds(60);

        public int MaxAttempts { get; set; } = 10;

        public TimeSpan RetryBackoffBase { get; set; } = TimeSpan.FromSeconds(5);

        public bool UseExponentialBackoff { get; set; } = true;

        public double BackoffMultiplier { get; set; } = 2.0;

        public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(15);
    }
}
