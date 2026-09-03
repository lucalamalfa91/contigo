# Contigo web client

React + TypeScript + Vite SPA. OIDC Authorization Code + PKCE via MSAL, config
injected at runtime. Honours ADR-012 (web stack) and ADR-010 (Entra ID / OIDC).

## Stack

- **React 19 + TypeScript + Vite** — static bundle, no server runtime
  (ADR-012). Build output is `dist/`.
- **`@azure/msal-browser` + `@azure/msal-react`** — Authorization Code + PKCE
  against Entra ID. `PublicClientApplication` has no client-secret field: this
  is structural, not just convention (AC-1).
- **Vitest + Testing Library** — unit tests under `tests/`, mirroring `src/`.

## Commands

```bash
npm ci          # install (CI uses this; --if-present until this scaffold landed)
npm run dev     # Vite dev server on :5173, reads public/config.json
npm run build   # tsc --noEmit type-check, then vite build -> dist/
npm test        # vitest run (single pass, CI mode)
npm run preview # serve dist/ locally
```

## Runtime config injection (ADR-012 "config, not code")

The SPA never hard-codes the API origin, OIDC authority, or client id. At
boot (`src/main.tsx`), it fetches same-origin `/config.json` and validates it
(`src/config/appConfig.ts`) before constructing MSAL or rendering the app; a
missing or malformed config fails loudly (a full-page error), never silently
falls back to a guessed value.

This is required by how the CI pipeline is already shaped, not just by
preference: `.github/workflows/web.yml`'s `build` job runs `npm run build`
**once** and uploads a single `web-dist` artifact; the `deploy` job downloads
that same artifact for either `dev` (push to `main`) or `demo` (`workflow_call`
reuse, ADR-016). One compiled bundle is deployed unchanged to both
environments, so per-environment values cannot be baked in at build time
(e.g. Vite `import.meta.env.VITE_*` statics) — they must be resolved from the
deployed origin at request time. `config.json` being a plain static asset
(not part of the JS bundle) is what makes it independently overwritable per
environment after the shared build.

`public/config.json` in this repo is a **local-dev-only placeholder** with
safe, non-secret values (`https://localhost:7109` — the API's dev
`launchSettings.json` port — and `REPLACE_WITH_*` OIDC placeholders). None of
the three config values are secret by design: `client_id` and the redirect
URI are public for a PKCE public client (ADR-010); the API base URL is not
sensitive.

### Known gap: per-environment config injection in CI

`web.yml`'s `deploy` job does not yet overwrite `config.json` in the
downloaded `web-dist/` artifact with that environment's real values before
the Static Web Apps deploy step — there is also no `infra/modules/staticwebapp`
yet to source those values from (tracked separately). Until both land, a real
`dev`/`demo` deploy of this bundle would serve the checked-in localhost
placeholder. Closing this is CI/infra work, not this task's file scope
(`web/src/`); the fix is additive to `web.yml`'s `deploy` job: write real
`apiBaseUrl` / `oidcAuthority` / `oidcClientId` / `oidcRedirectUri` /
`oidcApiScopes` values (sourced from that GitHub Environment's `vars.*`, none
of them secret) into `web-dist/config.json` after `download-artifact` and
before the Azure Static Web Apps deploy step, mirroring how
`vars.AZURE_STATIC_WEB_APP_NAME` is already read per-environment.

## Directory layout

```
web/
  public/
    config.json               # runtime config contract; dev-only placeholder values
    staticwebapp.config.json  # Azure SWA: SPA fallback routing
  src/
    config/appConfig.ts       # fetch + validate runtime config
    auth/msalConfig.ts        # AppConfig -> MSAL Configuration (no secret, ever)
    App.tsx                   # sign-in/sign-out shell (AuthenticatedTemplate/UnauthenticatedTemplate)
    main.tsx                  # boot: load config -> construct MSAL -> render
  tests/                      # mirrors src/; vitest + Testing Library
```
