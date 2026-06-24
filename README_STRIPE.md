Stripe integration notes

This project uses Stripe Checkout and webhooks for secure card payments. Follow these steps to configure and test locally.

1) Add configuration (do NOT commit secret keys)

- Local development (recommended): use `dotnet user-secrets` in the `src/SmartEGov.Web` project folder:

  dotnet user-secrets init
  dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
  dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..."
  dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..."

- Or set environment variables:

  # Linux/macOS
  export STRIPE_SECRET_KEY="sk_test_..."
  export STRIPE_PUBLISHABLE_KEY="pk_test_..."
  export STRIPE_WEBHOOK_SECRET="whsec_..."

  # Windows PowerShell
  $Env:STRIPE_SECRET_KEY = "sk_test_..."
  $Env:STRIPE_PUBLISHABLE_KEY = "pk_test_..."
  $Env:STRIPE_WEBHOOK_SECRET = "whsec_..."

2) Configure application base URL (used for Checkout redirect)

- Set `App:BaseUrl` in `appsettings.Development.json` or environment variable `APP_BASE_URL`.
  Example: `https://localhost:7246`

3) Run app locally and expose webhook endpoint

- Use the Stripe CLI to forward webhooks to your local app (recommended):

  stripe listen --forward-to localhost:7246/stripe/webhook

  Copy the webhook signing secret printed by the CLI and set it as `Stripe:WebhookSecret` or `STRIPE_WEBHOOK_SECRET`.

- Alternatively, use ngrok:

  ngrok http 7246

  Then register the resulting HTTPS URL in Stripe Dashboard as a webhook endpoint for events:
  - `checkout.session.completed`
  - `payment_intent.succeeded`

4) Run EF Core migration to add Stripe columns to Payments

From repository root, using the SmartEGov.Infrastructure project for migrations:

  cd src/SmartEGov.Infrastructure
  dotnet ef migrations add AddStripeFieldsToPayments --project ../SmartEGov.Infrastructure/SmartEGov.Infrastructure.csproj --startup-project ../SmartEGov.Web/SmartEGov.Web.csproj
  dotnet ef database update --project ../SmartEGov.Infrastructure/SmartEGov.Infrastructure.csproj --startup-project ../SmartEGov.Web/SmartEGov.Web.csproj

(If you prefer, the repository already contains a migration file; you can run `dotnet ef database update`.)

5) Test Checkout flow

- Create a payment in the app; it should redirect to Stripe Checkout.
- Complete Checkout using Stripe test card numbers (e.g., `4242 4242 4242 4242`).
- Confirm webhook events are received and the payment status is updated.

Security notes

- Never commit `sk_live_` keys. Use user-secrets or environment variables for production and development.
- Webhook endpoint validates signatures. Keep `Stripe:WebhookSecret` secret.
- Prefer client-side tokenization (Elements) where appropriate.

