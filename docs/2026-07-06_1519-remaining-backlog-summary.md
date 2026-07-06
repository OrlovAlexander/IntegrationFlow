# Актуальный backlog IntegrationFlow (после P2 core)

**Статус:** актуально  
**Создан:** 2026-07-06 15:19 (UTC+3)  
**Обновлён:** 2026-07-06 16:17 (UTC+3)  
**Основание:** [`2026-07-05_1455-integrationflow-full-analysis.md`](2026-07-05_1455-integrationflow-full-analysis.md), [`2026-07-06_1617-rabbitmq-p2-implementation-status.md`](2026-07-06_1617-rabbitmq-p2-implementation-status.md)  
**Следующий план:** P3 — [`plans/2026-07-04_0930-post-analysis-roadmap.md`](plans/2026-07-04_0930-post-analysis-roadmap.md)

---

## Закрыто

| Блок | Статус | Документ |
|------|--------|----------|
| P1 transport G1–G5 | ✅ | [`2026-07-06_1456-rabbitmq-g1-g5-implementation-status.md`](2026-07-06_1456-rabbitmq-g1-g5-implementation-status.md) |
| P2 resilience core (F, D, RPC-C/M, PF) | ✅ | [`2026-07-06_1617-rabbitmq-p2-implementation-status.md`](2026-07-06_1617-rabbitmq-p2-implementation-status.md) |
| P3/P4 RPC (sync + AsyncOutbox) | ✅ | [`2026-07-04_2301-sentandwait-rpc-implementation-status.md`](2026-07-04_2301-sentandwait-rpc-implementation-status.md) |
| v1.0 adoption/docs (runbooks, metrics, pools PF1) | ✅ | [`plans/2026-07-04_2130-remaining-risks-mitigation.md`](plans/2026-07-04_2130-remaining-risks-mitigation.md) |

---

## Ops-blocker v1.0 / v1.0.1

| Задача | Действие |
|--------|----------|
| **NuGet publish v1.0.0** | `NUGET_API_KEY` → tag `v1.0.0` → push |
| **Release v1.0.1** | Commit P2 → tag `v1.0.1` (P2 core для production hardening) |

Runbook: [`runbooks/2026-07-04_0845-nuget-release.md`](runbooks/2026-07-04_0845-nuget-release.md)

---

## Adoption (не код — runbooks)

| ID | Риск | Mitigation |
|----|------|------------|
| R3 | `ThrowOnFailure=false` default | `ThrowOnFailure = true` или `IntegrateWithResult()` |
| R4 | Server ack до reply | Reply до return |
| R1/R2 | Sync RPC timeout | `MessageId` + cache + `RetryOnTimeout`; critical → AsyncOutbox |
| A3 | Sync `Integrate()` в ASP.NET | `IntegrateAsync()` |

---

## Следующий кодовый приоритет: P3

| P | Задача |
|---|--------|
| P3 | Health checks (`IHealthCheck`), env/`IConfiguration` overlay |
| P3 optional | Distributed tracing через headers |
| P4 | Shared connection profiles, Headers API, netstandard2.0 hosted listener |

---

## Optional / v1.1 (не блокер)

| ID | Задача | План |
|----|--------|------|
| P2-CB | Circuit breaker publish/consume | [`plans/2026-07-06_1519-rabbitmq-p2-resilience-hardening.md`](plans/2026-07-06_1519-rabbitmq-p2-resilience-hardening.md) волна 4 |
| P2-HA | Cluster endpoints | то же |
| P2-PF+ | Pool idle eviction / max lifetime | v1.1 |
| Tech debt | RabbitMQ.Client v7, chaos/DLQ E2E | roadmap |

---

## Итог

**P1 + P2 core закрыты.** До публичного release — **NuGet publish**. Следующая волна разработки — **P3 ops readiness** (health checks, config overlay).
