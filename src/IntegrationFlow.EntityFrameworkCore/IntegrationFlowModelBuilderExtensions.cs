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

        return modelBuilder;
    }
}
