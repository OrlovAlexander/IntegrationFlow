using System;
using System.Text;
using IntegrationFlow.Contexts.Integrations._03Domain.RpcPending;
using Microsoft.EntityFrameworkCore;

namespace IntegrationFlow.EntityFrameworkCore.RpcPending;

/// <summary>
/// Extension methods for staging async RPC pending requests in DbContext TX.
/// </summary>
public static class DbContextRpcPendingExtensions
{
    /// <summary>
    /// Stages async RPC request without SaveChanges.
    /// </summary>
    public static RpcPendingRequest EnqueueRpcRequest(
        this DbContext context,
        string profileName,
        object payload,
        string contentType = "application/json",
        Guid? id = null)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var request = new RpcPendingRequest(
            id ?? Guid.NewGuid(),
            profileName,
            SerializePayload(payload),
            contentType,
            DateTimeOffset.UtcNow);

        context.Set<RpcPendingRequestEntity>().Add(EfRpcPendingMapper.ToEntity(request));
        return request;
    }

    private static byte[] SerializePayload(object payload)
    {
        if (payload == null)
        {
            return Array.Empty<byte>();
        }

        return payload switch
        {
            byte[] bytes => bytes,
            string text => Encoding.UTF8.GetBytes(text),
            _ => Encoding.UTF8.GetBytes(payload.ToString() ?? string.Empty)
        };
    }
}
