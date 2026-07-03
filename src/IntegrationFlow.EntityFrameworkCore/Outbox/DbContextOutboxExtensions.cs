using System;
using IntegrationFlow.Contexts.Integrations._03Domain.Outbox;
using Microsoft.EntityFrameworkCore;

namespace IntegrationFlow.EntityFrameworkCore.Outbox;

/// <summary>
/// Расширения DbContext для transactional outbox.
/// </summary>
public static class DbContextOutboxExtensions
{
    /// <summary>
    /// Подготовить outbox-сообщение к сохранению в текущем DbContext (без SaveChanges).
    /// </summary>
    public static void EnqueueOutboxMessage(this DbContext context, OutboxMessage message)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (message == null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        context.Set<OutboxMessageEntity>().Add(EfOutboxMapper.ToEntity(message));
    }
}
