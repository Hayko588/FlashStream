# FlashStream API

FlashStream is a .NET 9 order-ingestion API backed by PostgreSQL, Redis, and Kafka. It uses controllers and a layered architecture:

- **Domain** — order, product, outbox, and event types.
- **Application** — use cases, ports, DTOs, configuration contracts, and FluentValidation rules.
- **Infrastructure** — PostgreSQL repositories, Redis adapters, Kafka outbox publishing, and persistence.
- **API** — controllers, JWT authentication, request validation, HTTP error mapping, and Swagger.

## Run the development stack

Copy `.env.example` to `.env`, change the PostgreSQL password, and set `JWT_AUTHORITY` and `JWT_AUDIENCE` to values from your HTTPS identity provider. The access token's `sub` claim must be a GUID. Then run:

```powershell
docker compose up --build
```

The API listens on `http://localhost:8080`. Swagger is exposed only in Development:

- UI: `http://localhost:8080/swagger`
- OpenAPI JSON: `http://localhost:8080/swagger/v1/swagger.json`
- Readiness: `http://localhost:8080/health`

All published service ports bind to loopback. If a default port is occupied, set `POSTGRES_HOST_PORT`, `REDIS_HOST_PORT`, `KAFKA_HOST_PORT`, or `API_HOST_PORT` in `.env` to a free local port.

PostgreSQL creates the schema on startup. There is no implicit sample product or user. Products must be loaded through a trusted provisioning process; do not expose direct database access publicly.

## API

- `GET /health` — checks PostgreSQL availability.
- `GET /products/{productId}` — reads product data, using Redis as a best-effort cache.
- `POST /orders` — requires a bearer token and `Idempotency-Key` GUID header. Body: `{"productId":"<guid>","quantity":2}`.
- `GET /orders/{orderId}` — requires a bearer token and returns only orders owned by the token subject.

`POST /orders` returns `202 Accepted` for a new order and `200 OK` when returning the result for a matching idempotent retry. A reused key with different order details returns `409 Conflict`. The worker asynchronously confirms or rejects the order based on available inventory.

## Configuration

Connection strings and identity-provider details are supplied through environment variables or a secret manager, not committed appsettings:

- `ConnectionStrings__Postgres`
- `Redis__ConnectionString`
- `Kafka__BootstrapServers`
- `Authentication__Authority` — HTTPS issuer/discovery endpoint.
- `Authentication__Audience`

Kafka topic names, consumer group, partition and replication counts, Redis cache TTL, rate limits, maximum order quantity, outbox timing, and request-size/JWT clock-skew limits are bound from configuration and validated at startup. Development defaults are in each service's `appsettings.json`; override them for deployed environments.

For local `dotnet run`, start PostgreSQL, Redis, and Kafka separately, then set the required environment variables in the terminal before starting the API and worker in separate terminals. The API requires the JWT issuer and audience even in Development.

## OWASP Top 10 (2021) coverage

| Risk | Current controls and remaining work |
| --- | --- |
| A01 Broken Access Control | Order endpoints require JWTs and order lookups are scoped to the authenticated GUID subject. Product and health endpoints are explicitly public. |
| A02 Cryptographic Failures | The API accepts only HTTPS identity-provider metadata. Configure TLS at the edge and encrypted database, Redis, and Kafka links in deployed environments. |
| A03 Injection | Persistence uses parameterized EF Core queries; no user-provided SQL or shell commands are executed. |
| A04 Insecure Design | FluentValidation, idempotency, inventory row locking, fixed-window request limits, and a request-body size cap constrain abuse and race conditions. |
| A05 Security Misconfiguration | External secrets/configuration are required, configuration is validated at startup, security headers are set, and Swagger is Development-only. Review host and proxy settings for each deployment. |
| A06 Vulnerable and Outdated Components | Package versions are explicit. Add automated NuGet/container vulnerability scanning and an update process to CI; this repository does not provide that pipeline. |
| A07 Identification and Authentication Failures | JWT issuer, signature, audience, and lifetime validation is enabled. Token issuance, MFA, account recovery, and identity-provider policy remain the responsibility of the configured provider. |
| A08 Software and Data Integrity Failures | PostgreSQL transactions and idempotent event processing protect order state. Add dependency provenance/signing checks and production migration controls to deployment pipelines. |
| A09 Security Logging and Monitoring Failures | Unexpected server errors are logged without returning exception details. Add centralized log retention, security-event alerts, and operational metrics before production. |
| A10 Server-Side Request Forgery | No endpoint fetches caller-supplied URLs. Restrict service egress and treat configured broker/database/issuer addresses as trusted deployment configuration. |

The Compose stack binds service ports to loopback for local development. These controls reduce risk but are not a production security certification; perform deployment-specific threat modeling and security testing.

## Delivery guarantees

- API order and request outbox records are persisted atomically.
- Outbox messages are retried; Kafka delivery is at least once.
- The worker commits inventory, order state, a processed-event marker, and the status outbox event in one PostgreSQL transaction, then commits the Kafka offset.
- PostgreSQL is authoritative for inventory; Redis only caches product reads.
- Payment-provider authorization is not implemented. No card data is accepted.

The starter initializes a new development schema with EF Core `EnsureCreated`. Use versioned migrations and production-grade database operations before deployment.
