# client-architect (web delta) — independent lane

Owner: client-architect. Scope this pass: **routing, MSAL/`config.json`, OpenAPI
regen into `web/`, and SWA hosting/path config.** Explicitly **not** pixels,
type, colour, IA, screen semantics, or role definitions (ux-ui-designer).

## Files
- `routing-msal-config.md` — the lane draft (routes, MSAL invariants, OpenAPI
  regen chore, SWA fallback routing). Consumes `inputs/design/prototypes/ia.md`
  only to anchor routes; does not author or alter IA.

## Locked decisions re-cited (never re-litigated)
ADR-012 (web stack/SWA/OIDC-PKCE), ADR-013 (mobile non-gating), ADR-010 (Entra
OIDC public client), ADR-016 (`dev`→`demo` promotion). No infra delta found.

## What I am NOT doing here
- No ADR for IA, design system, or screen inventory (ux-ui-designer + product-owner).
- No screen semantics, no visual tokens.
- No backend write; I flag thin-gap candidates to software-architect only.

## Vote posture at close
APPROVE on routing/MSAL-config/OpenAPI-regen/SWA as a client-side plumbing
stabilization, pending cloud-architect's SWA-availability confirm and
security-architect's CORS-origin confirm. No OBJECT expected from this lane.
