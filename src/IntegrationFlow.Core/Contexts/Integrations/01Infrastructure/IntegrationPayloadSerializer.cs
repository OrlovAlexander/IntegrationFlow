using System;
using System.Text;
using System.Text.Json;

namespace IntegrationFlow.Contexts.Integrations._01Infrastructure;

/// <summary>
/// Serializes integration payloads for transport and outbox storage.
/// </summary>
public static class IntegrationPayloadSerializer
{
    public static byte[] SerializeToBytes(object? data)
        => data switch
        {
            null => Array.Empty<byte>(),
            byte[] bytes => bytes,
            ReadOnlyMemory<byte> memory => memory.ToArray(),
            Memory<byte> memory => memory.ToArray(),
            string text => Encoding.UTF8.GetBytes(text),
            _ => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data)),
        };

    public static string SerializeToString(object? data)
        => data switch
        {
            null => string.Empty,
            string text => text,
            byte[] bytes => Encoding.UTF8.GetString(bytes),
            ReadOnlyMemory<byte> memory => Encoding.UTF8.GetString(memory.ToArray()),
            Memory<byte> memory => Encoding.UTF8.GetString(memory.ToArray()),
            _ => JsonSerializer.Serialize(data),
        };
}
