---
id: us-02
type: user-story
parent: feature-03
wave: 6
status: active
---

# us-02-navigation-shell — Left-rail shell + role guards + global Ask

## Story

As a **workspace user**, I want the 224px left-rail navigation with
admin/procurement guards and the global Ask bar, so I can reach every Day-1
surface safely.

## Acceptance criteria

- [ ] AC-1 Left rail: Home, Portfolio, Renewals, Ask (⌘K), Quote check, Documents, Review queue, Workspace & members.
- [ ] AC-2 Workspace & members hidden/gated for Procurements ("request access").
- [ ] AC-3 Global Ask bar on every app screen (contextual suggestions; Enter/⌘K → /ask).

## Definition of done

- [ ] Shell renders on `demo`; role guard navigates correctly per role.
- [ ] honours ADR-012, ADR-018, ADR-019 (shell pattern).

## Dependencies

| Depends on | Why |
|------------|-----|
| feature-02 (design system) | shell styling |
| us-01 (sign-in) | auth/login |

## Architecture decisions in force

- ADR-018 (route map, roles), ADR-019 (shell, global Ask).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Left-rail shell + guards + global Ask | M | phase-04 |

## Council decisions carried into this story

Roles are a permission gate, never an IA fork.
