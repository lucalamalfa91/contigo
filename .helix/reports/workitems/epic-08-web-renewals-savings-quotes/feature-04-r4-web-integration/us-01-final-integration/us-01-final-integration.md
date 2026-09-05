---
id: us-01
type: user-story
parent: feature-04
wave: 8
status: active
---

# us-01-final-integration — Web Day-1 final integration (browser on demo)

## Story

As a **Procurement user on `demo`**, I want the full §20 Day-1 path to work
end-to-end in the browser, so the web pass closes against the prototype, not
`dotnet test`.

## Acceptance criteria

- [ ] AC-1 Walk the full Day-1 path in a browser on `demo` (sign in → invite → upload → review → Contract 360 → Ask with citations + one abstain → renewal action → savings opportunity → quote check → record outcome → Home realized updates).
- [ ] AC-2 UI matches `inputs/design/prototypes/day1-demo.html` (not localhost `config.json`, not Swagger).
- [ ] AC-3 `demo-v*` promotion honoured (ADR-016).

## Definition of done

- [ ] Manual + automated smoke of the Day-1 path on `demo` passes; recorded as the web-pass integration gate.
- [ ] honours ADR-016, ADR-018 (Day-1 path), ADR-020.

## Dependencies

| Depends on | Why |
|------------|-----|
| feature-01/02/03 (all web screens) | full ladder |

## Architecture decisions in force

- ADR-016 (promotion), ADR-018, ADR-020.

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Browser Day-1 walk on demo | L | phase-15 |

## Council decisions carried into this story

Final integration is a browser user journey, not a backend test.
