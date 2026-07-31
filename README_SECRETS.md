# Configuring Secrets

`appsettings.json` no longer contains real credentials — it only has empty
placeholders for every secret key. Real values must come from either
**User Secrets** (local development) or **environment variables** (any other
environment: staging, production, CI).

## ⚠️ First: rotate the old credentials

Before doing anything else, if this repository has ever been pushed to a
remote (even a private one, even briefly), treat every credential that used
to be in `appsettings.json` as compromised and rotate it:

- Brevo SMTP password + Brevo API key ([app.brevo.com](https://app.brevo.com) → SMTP & API settings)
- Google OAuth Client Secret ([Google Cloud Console](https://console.cloud.google.com/) → APIs & Services → Credentials)
- Stripe secret/publishable keys (these were `sk_test_...` / `pk_test_...` — test mode, but still worth rotating if you plan to go live with the same Stripe account)
- The default admin account password

## Local development: `dotnet user-secrets`

Run these from `src/SmartEGov.Web` (where the `.csproj` lives). User Secrets
are stored outside the repo (in your OS user profile), so they're never
committed:

```bash
cd src/SmartEGov.Web
dotnet user-secrets init   # only needed once - adds a UserSecretsId to the .csproj

dotnet user-secrets set "DefaultAdmin:Password" "ChooseYourOwnStrongPassword!1"
dotnet user-secrets set "AutoFill:EncryptionKey" "<base64 32-byte key>"
dotnet user-secrets set "OcrSpace:ApiKey" "<your OCR.space API key>"
dotnet user-secrets set "SmtpSettings:UserName" "<brevo smtp login>"
dotnet user-secrets set "SmtpSettings:FromEmail" "<brevo smtp login>"
dotnet user-secrets set "SmtpSettings:Password" "<brevo smtp password>"
dotnet user-secrets set "Brevo:ApiKey" "<brevo api key>"
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..."
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..."
dotnet user-secrets set "Authentication:Google:ClientId" "<google oauth client id>"
dotnet user-secrets set "Authentication:Google:ClientSecret" "<google oauth client secret>"
```

If you don't set `DefaultAdmin:Password`, the app will generate a random
one-time password on startup and print it to the console/logs (Development
only) — that's expected, not a bug.

To generate a fresh `AutoFill:EncryptionKey` (32 random bytes, base64-encoded):

```bash
dotnet run --project src/SmartEGov.Web -- --generate-key
# or, without running the app:
python3 -c "import base64, os; print(base64.b64encode(os.urandom(32)).decode())"
```

## Production / staging: environment variables

ASP.NET Core's configuration system maps `:` in a config key to `__` (double
underscore) in an environment variable name. Set these on your host,
container, or CI secret store — never in a committed file:

```
DefaultAdmin__Password=...
AutoFill__EncryptionKey=...
OcrSpace__ApiKey=...
SmtpSettings__UserName=...
SmtpSettings__FromEmail=...
SmtpSettings__Password=...
Brevo__ApiKey=...
Stripe__SecretKey=...
Stripe__PublishableKey=...
Authentication__Google__ClientId=...
Authentication__Google__ClientSecret=...
ReverseProxy__TrustedProxyIps=10.0.0.4,10.0.0.5   # only if running behind nginx/ingress
```

In Production, if `DefaultAdmin__Password` is missing the app will **refuse
to seed the admin account** (logged as an error) rather than fall back to any
default password — configure it before first run.

For a managed hosting platform, prefer its native secret store over plain
environment variables where available (Azure App Service → Configuration →
"Deployment slot setting" + Key Vault references; AWS → Secrets Manager /
Parameter Store; etc.).
