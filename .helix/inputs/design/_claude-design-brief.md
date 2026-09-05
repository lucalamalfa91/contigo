# Claude Design brief — Contigo web Day-1 (wave 6+)

Use **Claude Design** (`/design`, DesignSync, claude.ai/design). Do **not** invent a
pixel system as a coding agent and call it done.

## Product

Contigo is an AI-native Procurement Intelligence Platform (web-first, ADR-012:
React + TS + Vite SPA, Entra OIDC PKCE, Azure Static Web Apps). Backend R0–R4
is treated as done. This pass designs the **user-visible web** only.

North star: *Contigo knows what we bought, what we pay, when we need to act, and
where we can save money.*

Roles in V1 Day-1 path: **Workspace Admin** vs **Procurement**. Legal / Finance /
read-only exist in spec but are not required as full nav variants.

## Required screens (one clickable Day-1 path)

1. Sign-in (Entra / MSAL) → workspace
2. Invite / role (admin vs procurement)
3. Upload contract → document status (processing / ready / failed)
4. Portfolio list + filters (spec §8.1 columns)
5. Contract 360 (header + tabs: Overview, Commercials, Products, Clauses,
   Obligations, Risks, Documents, Benchmark, Renewal, Activity)
6. Review / correction (confidence: >95% accept, 80–95% flag, <80% require review)
7. Ask Contigo + citations / abstain (“cannot determine reliably”)
8. Renewal pipeline + insight card + action (spec §9.3)
9. Savings KPIs + list (spec §10.1)
10. Quote extract → assessment → target → negotiation (spec §11–12)

Include empty, error, and loading states on the Day-1 path. No marketing landing.

## Export onto disk (this repo)

Write under `.helix/inputs/design/`:

```
design-system.md
ia.md
screens.md
prototypes/
  day1-demo.html
  r0-workspace.html
  r1-contract-360.html
  r1-ask-contigo.html
  r2-renewals.html
  r3-savings.html
  r4-quote-check.html
```

Update `README.md` with the Claude Design **project name + URL**
(`https://claude.ai/design/...`) so Helix council and later `/design-sync` can
find it.

Cite spec §16 / §20 in `screens.md`. ADRs 012/013 stay locked (no new SPA stack).
