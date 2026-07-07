# Актуальный backlog IntegrationFlow (после P4-6)

**Статус:** актуально  
**Создан:** 2026-07-06 15:19 (UTC+3)  
**Обновлён:** 2026-07-07 21:30 (UTC+3)  
**Основание:** [`2026-07-05_1455-integrationflow-full-analysis.md`](2026-07-05_1455-integrationflow-full-analysis.md), [`2026-07-06_1617-rabbitmq-p2-implementation-status.md`](2026-07-06_1617-rabbitmq-p2-implementation-status.md)  
**Детальный backlog:** [`plans/2026-07-06_1645-rabbitmq-implementation-backlog.md`](plans/2026-07-06_1645-rabbitmq-implementation-backlog.md)  
**P3 status:** [`2026-07-06_1753-rabbitmq-p3-ops-implementation-status.md`](2026-07-06_1753-rabbitmq-p3-ops-implementation-status.md)  
**P4 status (P4-4 … P4-6):** [`2026-07-07_2103-rabbitmq-p4-implementation-status.md`](2026-07-07_2103-rabbitmq-p4-implementation-status.md)  
**Следующий план:** P4-7 — [`plans/2026-07-06_1645-rabbitmq-implementation-backlog.md`](plans/2026-07-06_1645-rabbitmq-implementation-backlog.md)

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
| P4-6 (topology helpers / DeclareTopologyOnStartup) | ✅ | незакоммичено — [`2026-07-07_2103-rabbitmq-p4-implementation-status.md`](2026-07-07_2103-rabbitmq-p4-implementation-status.md) |
| T-1, T-3 (chaos restart, DLQ E2E) | ✅ | `RabbitMqListenerHostedEndToEndTests`, `RabbitMqDeadLetterEndToEndTests` |
| v1.0 adoption/docs (runbooks, metrics, pools PF1) | ✅ | [`plans/2026-07-04_2130-remaining-risks-mitigation.md`](plans/2026-07-04_2130-remaining-risks-mitigation.md) |

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

## Следующий кодовый приоритет: P4 (DX) — backlog

| P | Задача | Статус |
|---|--------|--------|
| P4-1 | Shared connection profiles | ✅ |
| P4-2 | Headers API | ✅ |
| P4-3 | Sample hosted RPC server | ✅ |
| P4-4 | Hosted listener netstandard2.0 | ✅ |
| P4-5 | AMQP priority / expiration | ✅ |
| T-1 | Chaos E2E broker restart | ✅ |
| T-3 | E2E DLQ topology | ✅ |
| P4-6 | Topology helpers (dev) | ✅ |
| P4-7 | Consumer tag / exclusive | backlog |

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

**P1 + P2 core + P3 + P4-1…P4-6 + T-1/T-3 закрыты.** До публичного release — **NuGet publish**. Следующая волна DX — **P4-7**. Статус P4: [`2026-07-07_2103-rabbitmq-p4-implementation-status.md`](2026-07-07_2103-rabbitmq-p4-implementation-status.md). Runbook: [`runbooks/2026-07-04_0845-metrics-and-alerting.md`](runbooks/2026-07-04_0845-metrics-and-alerting.md).
