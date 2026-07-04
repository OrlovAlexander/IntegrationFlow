# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-07-04

### Added

- `IntegrationFlow.Core` — RabbitMQ ReceiveAndProcess and SentAndForgot with transactional outbox
- `IntegrationFlow.EntityFrameworkCore` — EF Core stores for outbox claim and message deduplication
- `IntegrationFlow.Metrics.OpenTelemetry` — reference `IIntegrationFlowMetrics` via `System.Diagnostics.Metrics`
- Hosted listener API (`AddIntegrationFlowRabbitMqListener`), outbox relay worker, abandoned replay
- CI pack job and NuGet release workflow

[1.0.0]: https://github.com/OrlovAlexander/IntegrationFlow/releases/tag/v1.0.0
