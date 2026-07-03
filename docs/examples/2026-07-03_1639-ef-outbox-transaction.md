# Transactional outbox в одной TX с бизнес-данными

```csharp
await using var db = await dbContextFactory.CreateDbContextAsync(ct);
await using var tx = await db.Database.BeginTransactionAsync(ct);

await orderRepository.CreateAsync(order, ct);
db.EnqueueOutboxMessage(new OutboxMessage(
    Guid.NewGuid(),
    profileName: "OrdersOut",
    payload: serializedPayload,
    contentType: "application/json",
    createdAt: DateTimeOffset.UtcNow,
    attemptCount: 0));

await db.SaveChangesAsync(ct);
await tx.CommitAsync(ct);
```

Альтернатива через DI:

```csharp
services.AddDbContext<MyDbContext>(...);
services.AddIntegrationFlowEfOutbox<MyDbContext>();

// В handler:
outboxEnqueue.Stage(outboxMessage);
await db.SaveChangesAsync(ct);
```

> `OutboxTransmitter` при использовании `IOutboxEnqueue` только вызывает `Stage` — `SaveChanges` выполняет приложение.
