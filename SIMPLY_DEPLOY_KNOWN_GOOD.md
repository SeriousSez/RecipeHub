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

## API startup behavior

File: `RecipeHub.Api/Program.cs`

- `CreateDbIfNotExists(...)` is called **only in Development**.
- Production startup should not create DB scope before `host.Run()`.

## Why this baseline matters

These settings avoid common Simply shared-hosting startup failures:

- ANCM 502.5 Out-Of-Process startup failure
- Missing runtime/assembly issues during process start
- IIS shared app-pool incompatibilities

If startup regresses, compare current config against this file first.
