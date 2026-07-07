using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using IntegrationFlow.Contexts.Integrations._03Domain;
using RabbitMQ.Client;

namespace IntegrationFlow.Contexts.Integrations._00InnerUsage.RabbitMq;

/// <summary>
/// Passive or active RabbitMQ topology ensure helpers for dev/prod profiles.
/// </summary>
internal static class RabbitMqTopologyHelper
{
    private static readonly ConcurrentDictionary<string, byte> DeclaredTopologyWarnings = new(StringComparer.OrdinalIgnoreCase);

    internal interface IRabbitMqTopologyChannel
    {
        void QueueDeclare(
            string queue,
            bool durable,
            bool exclusive,
            bool autoDelete,
            IDictionary<string, object>? arguments);

        void QueueDeclarePassive(string queue);

        void ExchangeDeclare(
            string exchange,
            string type,
            bool durable,
            bool autoDelete,
            IDictionary<string, object>? arguments);

        void ExchangeDeclarePassive(string exchange);
    }

    internal sealed class TopologyOptions
    {
        public bool ValidateTopology { get; set; } = true;

        public bool DeclareTopologyOnStartup { get; set; }

        public bool Durable { get; set; } = true;

        public string ExchangeType { get; set; } = RabbitMQ.Client.ExchangeType.Direct;
    }

    internal static void EnsureQueue(
        IModel channel,
        string queueName,
        TopologyOptions options,
        IIntegrationLogger? logger = null,
        string? profileName = null)
    {
        if (channel == null)
        {
            throw new ArgumentNullException(nameof(channel));
        }

        EnsureQueue(new ModelTopologyChannel(channel), queueName, options, logger, profileName);
    }

    internal static void EnsureQueue(
        IRabbitMqTopologyChannel channel,
        string queueName,
        TopologyOptions options,
        IIntegrationLogger? logger = null,
        string? profileName = null)
    {
        if (channel == null)
        {
            throw new ArgumentNullException(nameof(channel));
        }

        if (!options.ValidateTopology || string.IsNullOrWhiteSpace(queueName))
        {
            return;
        }

        if (options.DeclareTopologyOnStartup)
        {
            LogDeclareWarningIfNeeded(logger, profileName, queueName);
            channel.QueueDeclare(
                queue: queueName,
                durable: options.Durable,
                exclusive: false,
                autoDelete: false,
                arguments: null);
            return;
        }

        channel.QueueDeclarePassive(queueName);
    }

    internal static void EnsureExchange(
        IModel channel,
        string exchangeName,
        TopologyOptions options,
        IIntegrationLogger? logger = null,
        string? profileName = null)
    {
        if (channel == null)
        {
            throw new ArgumentNullException(nameof(channel));
        }

        EnsureExchange(new ModelTopologyChannel(channel), exchangeName, options, logger, profileName);
    }

    internal static void EnsureExchange(
        IRabbitMqTopologyChannel channel,
        string exchangeName,
        TopologyOptions options,
        IIntegrationLogger? logger = null,
        string? profileName = null)
    {
        if (channel == null)
        {
            throw new ArgumentNullException(nameof(channel));
        }

        if (!options.ValidateTopology || string.IsNullOrWhiteSpace(exchangeName))
        {
            return;
        }

        if (options.DeclareTopologyOnStartup)
        {
            LogDeclareWarningIfNeeded(logger, profileName, exchangeName);
            channel.ExchangeDeclare(
                exchange: exchangeName,
                type: string.IsNullOrWhiteSpace(options.ExchangeType) ? RabbitMQ.Client.ExchangeType.Direct : options.ExchangeType,
                durable: options.Durable,
                autoDelete: false,
                arguments: null);
            return;
        }

        channel.ExchangeDeclarePassive(exchangeName);
    }

    private sealed class ModelTopologyChannel : IRabbitMqTopologyChannel
    {
        private readonly IModel _channel;

        public ModelTopologyChannel(IModel channel)
        {
            _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        }

        public void QueueDeclare(
            string queue,
            bool durable,
            bool exclusive,
            bool autoDelete,
            IDictionary<string, object>? arguments)
        {
            _channel.QueueDeclare(queue, durable, exclusive, autoDelete, arguments);
        }

        public void QueueDeclarePassive(string queue)
        {
            _channel.QueueDeclarePassive(queue);
        }

        public void ExchangeDeclare(
            string exchange,
            string type,
            bool durable,
            bool autoDelete,
            IDictionary<string, object>? arguments)
        {
            _channel.ExchangeDeclare(exchange, type, durable, autoDelete, arguments);
        }

        public void ExchangeDeclarePassive(string exchange)
        {
            _channel.ExchangeDeclarePassive(exchange);
        }
    }

    private static void LogDeclareWarningIfNeeded(
        IIntegrationLogger? logger,
        string? profileName,
        string targetName)
    {
        if (logger == null)
        {
            return;
        }

        var warningKey = $"{profileName ?? "default"}:{targetName}";
        if (!DeclaredTopologyWarnings.TryAdd(warningKey, 0))
        {
            return;
        }

        logger.LogWarn(
            $"DeclareTopologyOnStartup is enabled for '{targetName}' (profile '{profileName ?? "default"}'). " +
            "Use only in local/dev; in production provision topology via IaC and keep passive declare.");
    }
}
