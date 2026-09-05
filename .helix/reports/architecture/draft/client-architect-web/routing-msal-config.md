# client-architect (web) — Routing, MSAL/config, OpenAPI regen, SWA

**Seat scope (this pass):** I own the *plumbing* that makes the web experience
reachable in the browser — client-side routing, the MSAL OIDC flow and
`config.json`, the generated TypeScript API client, and Static Web Apps
hosting/path concerns. I do **not** own pixels, type, colour, IA, or screen
semantics (all ux-ui-designer). I consume `inputs/design/prototypes/ia.md` only
to anchor routes; I do not author or alter it.

**Locked citations:** ADR-012 (React+TS+Vite SPA, OIDC PKCE, SWA free tier),
ADR-013 (mobile, non-gating), ADR-014/015/016 (trunk flow, CI→Azure OIDC,
`dev`→`demo` promotion). Brief §1 "API-first", "No secrets in client bundles".

---

## 1. Route table (backs `inputs/design/prototypes/ia.md`, does not replace it)

The IA route map is the designer's contract. My lane states the **router-level
facts only**: path, param, auth gate, and which generated API modules feed it.

| Route | Auth | Role gate | Primary API module(s) |
| --- | --- | --- | --- |
| `/signin` | public (MSAL redirect) | — | auth bootstrap |
| `/workspace/picker` | required | any | workspaces |
| `/workspace/members` | required | admin | workspace-members |
| `/documents` | required | any | documents |
| `/contracts` | required | any | contracts |
| `/contracts/:id` | required | any | contracts, evidence |
| `/contracts/:id/review` | required | any (labourer capability) | documents/extraction, corrections |
| `/ask` | required | any | ask (chat + citations) |
| `/renewals` | required | any | renewals |
| `/` (home) | required | any | savings |
| `/quotes/:id` | required | any | quote-check |

- `:id` params are **string object ids** (Postgres UUID per ADR-003); never ints.
- Every non-`/signin` route is behind an **auth gate**; the `/workspace/members`
  route additionally enforces the admin role (render gate on the member-API
  results plus a route guard as a UX affordance — the API remains the sole
  source of authorization, per ADR-010/012).
- The global **Ask bar** (⌘K, present on every app screen in the design system)
  is a route-level affordance: it navigates to `/ask` with an optional
  `?q=`/context param, not a door to a new page type.

## 2. MSAL + `config.json` (unchanged from ADR-012; re-asserted for the SPA)

The OIDC mechanics are already locked and correct. This pass does **not** change
them; it restates the invariants a web-wave implementer must not regress:

- **Public client, Authorization Code + PKCE.** Only `client_id` + `redirect_uri`
  live in the browser. No client secret, ever (locked: "No secrets in client
  bundles"; ADR-010).
- **`config.json` (runtime injection), not code.** The SPA reads three runtime
  values, so one build deploys to both `dev` and `demo` with only config
  differing (ADR-012 "Assumptions"):
  - `apiBaseUrl` — per-environment API origin.
  - `oidcAuthority` — Entra tenant (`dev` vs `demo`).
  - `oidcClientId` + `oidcRedirectUri` — public app id + SPA origin.
- **MSAL redirect URI must be the SWA origin + `/signin`** (the route above),
  registered in the per-env public client (ADR-010: four registrations total).
- **No BFF / API-proxy** for V1 (ADR-012 assumption carries): SPA calls the API
  origin directly with `Authorization: Bearer`; CORS is scoped to the registered
  front-end origin. If a web screen forces a BFF revisit, that is a defect to
  raise as OBJECT, not a silent addition.
- Refresh/session handling stays in MSAL (token cache in session/local storage);
  the app never stores a refresh secret beyond what the library secures.

## 3. MSAL SDK + version pinning

- Use **`@azure/msal-browser`** (not the legacy `msal`) for the SPA.
- `@azure/msal-react` is optional; a thin auth-context wrapper around
  `msal-browser` suffices for this app and keeps the bundle smaller. Either is
  acceptable — this is a build detail, not an architecture decision. No change to
  ADR-012.
- Pin an exact major/minor in `web/package.json` and regenerate a lockfile; do
  not float to latest across a web wave.

## 4. OpenAPI client regen (one generated TS client, no hand-written DTOs)

- **Single versioned OpenAPI document → one generated TS client** consumed by
  both `web/` and `mobile/` (ADR-012/013, `api-consumption.md` §1). Never
  hand-write a divergent DTO or endpoint list in `web/`.
- **Regen is a repeating chore**, run whenever the backend OpenAPI grew (E02–E05
  landed new endpoints/fields). Source of the doc is `web/openapi/` (or the
  canonical backend artifact the software-architect designates); the generated
  output lands under `web/src/api/` (generated dir), committed so the SPA builds
  without a live backend.
- **Codegen tool** stays the one already adopted for the E01/F07 shell. I do not
  re-pick it here; if the tool is not pinned, pin it in `web/` dev-dependencies
  so regen is reproducible. Exact versioning scheme (`/v1/...` vs header) is the
  software-architect's; clients honor exactly one active version per deployment
  and fail loudly on mismatch (`api-consumption.md` §1).
- **Thin API gap** handoff (the only allowed backend write, brief §2/§3): if a
  locked screen needs a field no endpoint exposes, the story may add a *thin,
  named* backend gap task. I flag candidate gaps only as "needs software-architect
  ruling"; I do not redesign modules. I did **not** find a gap blocking routing or
  auth itself.

## 5. Static Web Apps hosting — no infra delta (confirm with cloud-architect)

- ADR-012 → SWA free tier; build output is the static Vite bundle under `web/`.
- **Fallback routing:** SPA deep links (`/contracts/:id`, `/ask`, `/renewals`)
  must resolve to `index.html`. SWA provides `navigationFallback` (via
  `staticwebapp.config.json` in `web/` or platform setting). Assert a
  `staticwebapp.config.json` (or equivalent platform config) with
  `navigationFallback.rewrite: "/index.html"` so a cold load of
  `/contracts/<uuid>` does not 404. This is the one routing-adjacent infra file in
  my lane.
- **API route (optional):** no linked Functions/API is needed — SPA calls the
  backend origin directly (no BFF, §2). CORS on the backend scoped to the SWA
  origin (already a backend/security concern; I note it as a dependency, not a
  web write).
- **CI path-filter `web/`** already exists (ADR-012/014/015); web waves deploy to
  `dev`, promote to `demo` via ADR-016 (`demo-v*`). No new environment, no new
  infra SKU. I defer any SKU/region/SWA-quota confirmation to cloud-architect;
  I found nothing in my lane requiring an infra change.

## 6. Open items I hand to other seats (not blocking routing)

- **Codegen tool + OpenAPI versioning scheme pin** → software-architect (already
  an open item in `api-consumption.md` §5).
- **CORS origin list for the SWA front-end** → security-architect/cloud-architect
  (ADR-010/012 dependency; not a web source write).
- **SWA free-tier + region availability confirmation** → cloud-architect (carried
  from ADR-012 "Assumptions").
- **Thin API gap rulings** (if any surface from screen inventory) →
  software-architect; none found in my routing/auth/regen lane.
