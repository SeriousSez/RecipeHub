---
name: recipehub-feature-work
description: "Use when: implementing RecipeHub features, changing Angular UI, adding ASP.NET Core API endpoints, updating EF Core entities or migrations, working with food plans, recipes, pantry, groceries, dashboard, or troubleshooting RecipeHub build/database issues."
---

# RecipeHub Feature Work

Use this skill for project-specific implementation work in the RecipeHub workspace.

## Project Shape

- Frontend Angular app: `RecipeHub/`
- API host: `RecipeHub.Api/`
- Application services: `RecipeHub.ApplicationService/`
- Domain entities/models/responses: `RecipeHub.Domain/`
- EF Core DbContext, repositories, and migrations: `RecipeHub.Infrastructure/`

## Frontend Conventions

- Prefer existing Angular component patterns and `SharedModule` exports for reusable controls.
- Prefer reusable shared components over native controls when the app already has a custom equivalent.
- Use `app-taxonomy-select` instead of native `select` controls where a styled dropdown is expected.
- Use `app-date-picker` instead of native `input type="date"` for date fields.
- When introducing a reusable component, declare/export it from `RecipeHub/src/app/shared/modules/shared.module.ts` if it needs to be used across feature modules.
- Keep operational app screens dense and task-focused; avoid landing-page or marketing layouts.
- For mobile, verify controls fit inside their containers, do not overlap, and remain tappable.
- For dark mode, add `:host-context([data-theme='dark'])` rules for new component surfaces, controls, popovers, panels, and active states.
- Use translation keys for user-facing labels in shared or existing localized areas. Update all locale files under `RecipeHub/src/assets/i18n/` when adding keys.

## Modals and Popovers

- Prefer reusable modal components for workflows that may appear in more than one place, such as adding recipes to the food plan.
- Modal backdrops should match existing app behavior: semi-transparent overlay plus `backdrop-filter: blur(4px)`.
- Keep modal z-index above page content and below any higher-priority app overlays. Date picker popovers should appear above page content but not above modal dialogs.
- Make modal bodies scroll internally on mobile so headers and footers remain reachable.
- For mobile modals, use bottom-sheet-style sizing when it improves usability: fit within `100dvh`, keep footer actions tappable, and avoid content spilling outside rounded corners.
- Avoid native browser date/select pickers inside app-designed workflows unless the feature specifically needs native platform behavior.

## Backend Conventions

- Controllers live in `RecipeHub.Api/Controllers/` and generally call application services.
- Service interfaces live in `RecipeHub.ApplicationService/Interfaces/`; implementations live in `RecipeHub.ApplicationService/Services/`.
- Entities live in `RecipeHub.Domain/Entities/`; request models in `RecipeHub.Domain/Models/`; response DTOs in `RecipeHub.Domain/Responses/`.
- Repositories live in `RecipeHub.Infrastructure/Repositories/` or `RecipeHub.Infrastructure/Interfaces/` depending on existing pattern.
- Register new services/repositories in `RecipeHub.Api/Startup.cs`.
- Add AutoMapper mappings in `RecipeHub.Api/AutoMapper.cs` when exposing new response models.

## EF Core and Database

- Run EF commands from `C:\Repos\RecipeHub`.
- Use `ASPNETCORE_ENVIRONMENT=Development` for local migration commands.
- Use infrastructure as the EF project and API as startup project:

```powershell
$env:ASPNETCORE_ENVIRONMENT='Development'; dotnet ef migrations add <MigrationName> --project .\RecipeHub.Infrastructure\RecipeHub.Infrastructure.csproj --startup-project .\RecipeHub.Api\RecipeHub.Api.csproj
$env:ASPNETCORE_ENVIRONMENT='Development'; dotnet ef database update --project .\RecipeHub.Infrastructure\RecipeHub.Infrastructure.csproj --startup-project .\RecipeHub.Api\RecipeHub.Api.csproj
```

- For new MySQL foreign keys to `AspNetUsers.Id` or existing `char(36)` id columns, generated migrations may need to create the table without the FK, dynamically alter the FK column charset/collation from `information_schema.COLUMNS`, then add the FK.

## Validation

- Backend validation:

```powershell
dotnet build .\RecipeHub.Api\RecipeHub.Api.csproj
```

- Frontend validation:

```powershell
Set-Location C:\Repos\RecipeHub\RecipeHub; npm run build-prod
```

- The Angular build may emit an existing initial bundle budget warning; do not treat that as a regression unless the task is about bundle size.
- The solution may emit an existing `AutoMapper 12.0.1` vulnerability warning; report it when relevant but do not fix it unless requested.

## Implementation Checklist

1. Start from the nearest controller/component/service that owns the behavior.
2. Make the smallest coherent slice first.
3. Validate immediately after the first substantive edit.
4. Keep UI changes responsive and dark-mode aware.
5. Keep unrelated generated files, user changes, and worktree noise intact.
