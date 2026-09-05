---
id: us-01
type: user-story
parent: feature-04
wave: 6
status: active
---

# us-01-workspace-members-invite — Members & roles + invite

## Story

As a **Workspace Admin**, I want to see members/roles and invite a Procurement
user, so I can staff the workspace.

## Acceptance criteria

- [ ] AC-1 Members table (Member/Role/Status/Last active).
- [ ] AC-2 Invite pane: email + role radio (Admin vs Procurement) + permission summaries + Send.
- [ ] AC-3 Non-admin sees "You don't manage this workspace" + Request access.

## Definition of done

- [ ] Invite flow works in browser on `demo`; role gate enforced client-side per claims.
- [ ] honours ADR-018, ADR-020 (screen 2).

## Dependencies

| Depends on | Why |
|------------|-----|
| feature-03 us-02 (shell) | nav + role claims |

## Architecture decisions in force

- ADR-018 (roles), ADR-020 (screen 2).

## Tasks

| ID | Title | Effort | Phase |
|----|-------|--------|-------|
| task-01 | Members & roles + invite UI | M | phase-05 |

## Council decisions carried into this story

Admin vs Procurement is a permission gate, not a separate nav variant.
