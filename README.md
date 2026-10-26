# OnlineEbookShopV1

ASP.NET Core MVC storefront (`src/OnlineEbookShopV1.Mvc`) that calls a REST API (`src/OnlineEbookShopV1.Api`).
.NET 10, EF Core on Postgres (Supabase), JWT auth with `Admin` and `User` roles, Stripe or PayPal checkout,
RabbitMQ events, and Serilog or Azure Application Insights logging.

## Run with Docker

1. Copy `.env.example` to `.env` and fill in at least `SUPABASE_CONNECTION_STRING` and `JWT_KEY`.
   Add Stripe and/or PayPal sandbox keys to enable that payment option.
2. `docker compose up --build`
3. Shop: http://localhost:8081 · API + Swagger: http://localhost:8080/swagger · RabbitMQ UI: http://localhost:15672 (guest/guest)

The API applies EF Core migrations and seeds the admin user (`SEED_ADMIN_EMAIL` / `SEED_ADMIN_PASSWORD`) on startup.
Set `Database__AutoMigrate=false` to turn that off and run `db/InitialCreate.sql` in the Supabase SQL editor instead.
All tables live in the `ebookshop` schema.

## Run locally

```
dotnet user-secrets --project src/OnlineEbookShopV1.Api set "ConnectionStrings:Supabase" "<connection string>"
dotnet run --project src/OnlineEbookShopV1.Api     # http://localhost:5144
dotnet run --project src/OnlineEbookShopV1.Mvc     # http://localhost:5234
```

Development settings disable RabbitMQ; set `RabbitMq__Enabled=true` with a broker running to see events.

## Tests

`dotnet test` runs the xUnit + FakeItEasy suite (EF Core InMemory, no external services needed).

## Database migrations

```
dotnet tool install --global dotnet-ef
dotnet ef migrations add <Name> --project src/OnlineEbookShopV1.Api -o Data/Migrations
dotnet ef migrations script --idempotent --project src/OnlineEbookShopV1.Api -o db/InitialCreate.sql
```

## Payments

- **Stripe**: Checkout Session. Point a webhook at `/api/webhooks/stripe` for `checkout.session.completed`,
  `checkout.session.expired` and `payment_intent.payment_failed`, and set `STRIPE_WEBHOOK_SECRET`.
  Locally: `stripe listen --forward-to localhost:8080/api/webhooks/stripe`.
- **PayPal**: Orders v2. The buyer approves on PayPal, returns to `/Checkout/Success?token=...`, and the shop
  captures the payment there. Optionally add a webhook to `/api/webhooks/paypal` for `PAYMENT.CAPTURE.*`
  events and set `PAYPAL_WEBHOOK_ID`.

## Events (RabbitMQ)

Published to the `ebookshop.events` topic exchange and consumed from `ebookshop.audit` by
`AuditEventConsumer`, which hands each event to the matching `IDomainEventHandler`
(`AuthEventHandler`, `PaymentEventHandler`). Add a handler and register it in `Program.cs` to react to events.

| Event | Routing key |
|---|---|
| Login succeeded | `auth.login.succeeded` |
| Login failed | `auth.login.failed` |
| Logout | `auth.logout` |
| Invalid (inactive or missing) account | `auth.invalid_account` |
| Payment succeeded | `payment.succeeded` |
| Payment pending | `payment.pending` |
| Payment invalid or failed | `payment.invalid` |

## Logging

`Logging:IsLocal` (`LOGGING_IS_LOCAL` in Docker): `true` uses Serilog (console plus `logs/` files),
`false` uses Azure Application Insights via `APPLICATIONINSIGHTS_CONNECTION_STRING`.
