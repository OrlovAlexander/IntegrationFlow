# Актуальный backlog IntegrationFlow (после P4-7)

**Статус:** актуально  
**Создан:** 2026-07-06 15:19 (UTC+3)  
**Обновлён:** 2026-07-10 22:10 (UTC+3)  
**Основание:** [`2026-07-05_1455-integrationflow-full-analysis.md`](2026-07-05_1455-integrationflow-full-analysis.md), [`2026-07-06_1617-rabbitmq-p2-implementation-status.md`](2026-07-06_1617-rabbitmq-p2-implementation-status.md), [`plans/2026-07-10_0853-rest-implementation.md`](plans/2026-07-10_0853-rest-implementation.md), [`2026-07-10_1507-rest-implementation-status.md`](2026-07-10_1507-rest-implementation-status.md)  
**Детальный backlog:** [`plans/2026-07-06_1645-rabbitmq-implementation-backlog.md`](plans/2026-07-06_1645-rabbitmq-implementation-backlog.md)  
**P3 status:** [`2026-07-06_1753-rabbitmq-p3-ops-implementation-status.md`](2026-07-06_1753-rabbitmq-p3-ops-implementation-status.md)  
**P4 status (epic P4-1 … P4-7):** [`2026-07-07_2103-rabbitmq-p4-implementation-status.md`](2026-07-07_2103-rabbitmq-p4-implementation-status.md)  
**Следующий план:** NuGet publish v1.0.1 — [`plans/2026-07-06_1645-rabbitmq-implementation-backlog.md`](plans/2026-07-06_1645-rabbitmq-implementation-backlog.md)

---

## Закрыто

| Блок | Статус | Документ |
|------|--------|----------|
| P1 transport G1–G5 | ✅ | [`2026-07-06_1456-rabbitmq-g1-g5-implementation-status.md`](2026-07-06_1456-rabbitmq-g1-g5-implementation-status.md) |
| P2 resilience core (F, D, RPC-C/M, PF) | ✅ | [`2026-07-06_1617-rabbitmq-p2-implementation-status.md`](2026-07-06_1617-rabbitmq-p2-implementation-status.md) |
| P3 ops P3-1 … P3-7 (full epic) | ✅ | [`2026-07-06_1753-rabbitmq-p3-ops-implementation-status.md`](2026-07-06_1753-rabbitmq-p3-ops-implementation-status.md) |
| P3/P4 RPC (sync + AsyncOutbox) | ✅ | [`2026-07-04_2301-sentandwait-rpc-implementation-status.md`](2026-07-04_2301-sentandwait-rpc-implementation-status.md) |
| P4-1 … P4-3 (shared profiles, headers, RPC sample) | ✅ | коммиты `dfcd9b0` … `c2dd5b5` |
| P4-4 (hosted listener netstandard2.0) | ✅ | коммит `732a08f` — [`2026-07-07_2103-rabbitmq-p4-implementation-status.md`](2026-07-07_2103-rabbitmq-p4-implementation-status.md) |
| P4-5 (AMQP priority / expiration) | ✅ | коммит `4d0d977` — то же |
| P4-6 (topology helpers / DeclareTopologyOnStartup) | ✅ | `647d810`, `3a2cce7` — [`2026-07-07_2103-rabbitmq-p4-implementation-status.md`](2026-07-07_2103-rabbitmq-p4-implementation-status.md) |
| P4-7 (Consumer tag / exclusive) | ✅ | `3dcb385` — [`2026-07-07_2103-rabbitmq-p4-implementation-status.md`](2026-07-07_2103-rabbitmq-p4-implementation-status.md) |
| A-1 … A-4 (adoption runbooks) | ✅ | [`runbooks/2026-07-07_2137-rabbitmq-dlq-topology.md`](runbooks/2026-07-07_2137-rabbitmq-dlq-topology.md) и др. |
| T-1, T-3 (chaos restart, DLQ E2E) | ✅ | `RabbitMqListenerHostedEndToEndTests`, `RabbitMqDeadLetterEndToEndTests` |
| T-7 (P3 health check unit tests) | ✅ | `RabbitMqHealthCheckTests`, `RabbitMqTransportHealthRegistryTests` |
| T-4 (RPC load test, MaxConcurrentRequests &gt; 1) | ✅ | `RabbitMqRequestReplyLoadTests` |
| v1.0 adoption/docs (runbooks, metrics, pools PF1) | ✅ | [`plans/2026-07-04_2130-remaining-risks-mitigation.md`](plans/2026-07-04_2130-remaining-risks-mitigation.md) |
| **REST фазы 1–5** (SentAndWait, hardening, outbox HTTP, webhooks, AsyncOutbox) | ✅ | [`2026-07-10_1507-rest-implementation-status.md`](2026-07-10_1507-rest-implementation-status.md) |

---

## Ops-blocker v1.0 / v1.0.1

| Задача | Действие |
|--------|----------|
| **NuGet publish v1.0.0** | `NUGET_API_KEY` → tag `v1.0.0` → push |
| **Release v1.0.1** | Tag `v1.0.1` (P2 + P3 для production hardening) |

Runbook: [`runbooks/2026-07-04_0845-nuget-release.md`](runbooks/2026-07-04_0845-nuget-release.md)

---

## Adoption (не код — runbooks)

| ID | Риск | Mitigation |
|----|------|------------|
| R3 | `ThrowOnFailure=false` default | `ThrowOnFailure = true` или `IntegrateWithResult()` — runbook: [`runbooks/2026-07-07_2137-throwonfailure-adoption.md`](runbooks/2026-07-07_2137-throwonfailure-adoption.md) |
| R4 | Server ack до reply | Reply до return |
| R1/R2 | Sync RPC timeout | `MessageId` + cache + `RetryOnTimeout`; critical → AsyncOutbox |
| A3 | Sync `Integrate()` в ASP.NET | `IntegrateAsync()` |

---

## P4 (DX) — закрыт

| P | Задача | Статус |
|---|--------|--------|
| P4-1 | Shared connection profiles | ✅ |
| P4-2 | Headers API | ✅ |
| P4-3 | Sample hosted RPC server | ✅ |
| P4-4 | Hosted listener netstandard2.0 | ✅ |
| P4-5 | AMQP priority / expiration | ✅ |
| P4-6 | Topology helpers (dev) | ✅ |
| P4-7 | Consumer tag / exclusive | ✅ |
| T-1 | Chaos E2E broker restart | ✅ |
| T-3 | E2E DLQ topology | ✅ |

## Следующий кодовый приоритет

| ID | Задача | План |
|----|--------|------|
| T-2/T-5/T-6 | Chaos/TLS/CI gate (optional) | [`plans/2026-07-06_1645-rabbitmq-implementation-backlog.md`](plans/2026-07-06_1645-rabbitmq-implementation-backlog.md) |

---

## Optional / v1.1 (не блокер)

| ID | Задача | План |
|----|--------|------|
| P2-CB | Circuit breaker publish/consume | [`plans/2026-07-06_1519-rabbitmq-p2-resilience-hardening.md`](plans/2026-07-06_1519-rabbitmq-p2-resilience-hardening.md) волна 4 |
| P2-HA | Cluster endpoints | то же |
| P2-PF+ | Pool idle eviction / max lifetime | v1.1 |
| Tech debt | RabbitMQ.Client v7, T-2/T-4/T-5 | roadmap |

---

## Итог

**P1 + P2 core + P3 + P4 (полностью) + T-1/T-3/T-4/T-7 + REST фазы 1–5 закрыты.** До публичного release — **NuGet publish v1.0.1**. Epic P4 и REST plan закрыты. Runbooks: [`runbooks/2026-07-10_2130-rest-asyncoutbox-adoption.md`](runbooks/2026-07-10_2130-rest-asyncoutbox-adoption.md), [`runbooks/2026-07-04_0845-metrics-and-alerting.md`](runbooks/2026-07-04_0845-metrics-and-alerting.md).
