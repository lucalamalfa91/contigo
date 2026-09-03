# contigo-mobile

React Native (Expo) + TypeScript app for Contigo (ADR-013). This is a
**non-gating** lane: V1 is web-first, and neither this app nor a store
release is required for any R0–R4 wave or the Day-1 `demo` (see
`.github/workflows/mobile.yml`, which runs with `continue-on-error: true`
at both the job and step level so a failure here can never block `dev`/
`demo` promotion — parent story `us-01-mobile-scaffold` AC-3).

## Status

- Task T01 (this scaffold): Expo + TypeScript app, native redirect scheme
  (`contigo://callback`, AC-1) and per-environment config plumbing
  (`src/config/`).
- Task T02: wires the actual OIDC Authorization Code + PKCE flow (AC-2)
  against Entra ID, using `getNativeRedirectUri()` / `getMobileEnvConfig()`
  from `src/config/`.

## Requirements

- Node.js + npm (Expo SDK 57 / React Native 0.86 / TypeScript ~6.0).
- No native Android Studio / Xcode toolchain is required to develop —
  `npm start` runs the app in Expo Go or a simulator.

## Getting started

```bash
npm ci
cp .env.example .env   # then fill in the per-environment values
npm start
```

## Scripts

| Script | What it does |
|---|---|
| `npm run typecheck` / `npm run build` | `tsc --noEmit` — no native bundling step exists without EAS credentials (out of scope for V1; ADR-013: no store release for R0–R4), so type-checking is this app's CI-provable "build". |
| `npm test` | Runs the Jest (`jest-expo` preset) unit tests under `tests/`. |
| `npm start` / `npm run android` / `npm run ios` / `npm run web` | Expo dev server. |

## Config (no secrets in the bundle)

Per ADR-013 ("Mobile reads API base URL + OIDC authority from per-environment
config, exactly as `web/` does; no client secret is ever placed in the
app/bundle") and ADR-010 (PKCE public clients carry no client secret), the
app reads three non-secret, per-environment values from `EXPO_PUBLIC_*`
build-time env vars — see `.env.example` and `src/config/env.ts`:

- `EXPO_PUBLIC_API_BASE_URL` — backend API origin for this environment.
- `EXPO_PUBLIC_OIDC_AUTHORITY` — Entra ID issuer for this environment's tenant.
- `EXPO_PUBLIC_OIDC_CLIENT_ID` — the public-client application ID (no secret).

`getMobileEnvConfig()` fails fast (throws, naming the missing variable) if
any of the three is unset, rather than silently defaulting and pointing the
app at the wrong environment.

## Native redirect scheme

`app.json`'s `expo.scheme` is `contigo`, which is also Entra ID's registered
native reply URL for the public client (ADR-010). `src/config/redirectUri.ts`
builds this as `contigo://callback` (`getNativeRedirectUri()`), read from
`app.json` rather than duplicated as a literal.
