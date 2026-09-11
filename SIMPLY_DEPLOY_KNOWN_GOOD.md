# Simply.com known-good API deploy state

Date confirmed: 2026-02-22

This is the deployment baseline that successfully starts the API on Simply.com.

## CI/CD workflow

File: `.github/workflows/deploy-to-simply.yml`

- API publish mode: **Self-contained**
- Runtime identifier: **win-x86**
- Hosting model: **OutOfProcess**
- FTP deploy mode: `dangerous-clean-slate: false`
- No `app_offline.htm` deploy/remove cycle in pipeline
- Keep startup diagnostics enabled (`stdoutLogEnabled="true"` in deployed `web.config`)
- The workflow must pass `https://recipes.api.sezginsahin.dk/api/health/ping` before a deployment is considered successful.

## API startup behavior

File: `RecipeHub.Api/Program.cs`

- `CreateDbIfNotExists(...)` is called **only in Development**.
- Production startup should not create DB scope before `host.Run()`.

## Password reset email configuration

Files: `RecipeHub.Api/Services/SmtpEmailSender.cs`, `RecipeHub.ApplicationService/Services/AuthService.cs`

Configure these production settings on Simply.com through the host environment or the deployed `appsettings.json`. Keep the SMTP password out of source control:

- `Email:Enabled`: `true`
- `Email:Host`: the SMTP host provided by the mail provider
- `Email:Port`: usually `587`
- `Email:EnableSsl`: `true`
- `Email:Username`: the SMTP account username
- `Email:Password`: the SMTP account password, supplied as a deployment secret
- `Email:FromAddress`: a verified sender address for the domain
- `Email:FromName`: `RecipeHub`
- `PasswordReset:FrontendUrl`: `https://recipes.sezginsahin.dk`

After deployment, verify that a reset request sends an email and that its link opens the deployed `/reset-password` page. If SMTP is not configured, the API deliberately returns a service-unavailable response instead of exposing a reset token.

## Data Protection keys

The API persists ASP.NET Core Data Protection keys under `App_Data/DataProtection-Keys` by default. The deployment must preserve this runtime directory between releases and grant the application identity read/write access. It contains sensitive key material and must not be publicly served or committed to source control.

Override `DataProtection:KeysPath` with an absolute path when the host provides a private persistent directory outside the application root. On Simply.com, keep `dangerous-clean-slate: false` so password-reset tokens remain valid across application restarts and deployments.

## Why this baseline matters

These settings avoid common Simply shared-hosting startup failures:

- ANCM 502.5 Out-Of-Process startup failure
- Missing runtime/assembly issues during process start
- IIS shared app-pool incompatibilities

If startup regresses, compare current config against this file first.
