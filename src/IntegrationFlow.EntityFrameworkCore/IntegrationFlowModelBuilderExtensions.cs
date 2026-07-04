using System;
using IntegrationFlow.EntityFrameworkCore.Outbox;
using Microsoft.EntityFrameworkCore;

namespace IntegrationFlow.EntityFrameworkCore;

/// <summary>
/// Расширения модели EF Core для IntegrationFlow.
/// </summary>
public static class IntegrationFlowModelBuilderExtensions
{
    /// <summary>
    /// Регистрирует outbox и dedup-сущности.
    /// </summary>
    public static ModelBuilder ConfigureIntegrationFlow(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessageEntity>(entity =>
        {
            entity.ToTable("IntegrationFlowOutboxMessages");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.ProfileName).HasMaxLength(128).IsRequired();
            entity.Property(message => message.ContentType).HasMaxLength(128).IsRequired();
            entity.Property(message => message.Payload).IsRequired();
            entity.Property(message => message.Status).HasConversion<int>();
            entity.Property(message => message.LockedBy).HasMaxLength(128);
            entity.Property(message => message.LastError).HasMaxLength(2048);
            entity.HasIndex(message => new { message.Status, message.RetryAfter, message.CreatedAt });
        });

        modelBuilder.Entity<Deduplication.ProcessedMessageEntity>(entity =>
        {
            entity.ToTable("IntegrationFlowProcessedMessages");
            entity.HasKey(message => message.MessageId);
            entity.Property(message => message.MessageId).HasMaxLength(256);
            entity.Property(message => message.State).HasConversion<int>();
            entity.HasIndex(message => message.ExpiresAt);
        });

        modelBuilder.Entity<ResponseCache.RpcResponseCacheEntity>(entity =>
        {
            entity.ToTable("IntegrationFlowRpcResponseCache");
            entity.HasKey(entry => entry.MessageId);
            entity.Property(entry => entry.MessageId).HasMaxLength(256);
            entity.Property(entry => entry.State).HasConversion<int>();
            entity.Property(entry => entry.ResponseBody).IsRequired();
            entity.HasIndex(entry => entry.ExpiresAt);
        });

        modelBuilder.Entity<RpcPending.RpcPendingRequestEntity>(entity =>
        {
            entity.ToTable("IntegrationFlowRpcPendingRequests");
            entity.HasKey(request => request.Id);
            entity.Property(request => request.ProfileName).HasMaxLength(128).IsRequired();
            entity.Property(request => request.ContentType).HasMaxLength(128).IsRequired();
            entity.Property(request => request.RequestPayload).IsRequired();
            entity.Property(request => request.Status).HasConversion<int>();
            entity.Property(request => request.LockedBy).HasMaxLength(128);
            entity.Property(request => request.LastError).HasMaxLength(2048);
            entity.HasIndex(request => new { request.Status, request.RetryAfter, request.CreatedAt });
        });

        return modelBuilder;
    }
}
