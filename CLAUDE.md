# Contigo

Processo di implementazione a due ruoli: **implementer** poi **reviewer**.
Entrambi usano **AFI** (Agentic File Index) per ogni domanda strutturale sul codice.
Grep e Read restano per testo e file singoli; caller, import, blast radius e catene di chiamata passano da AFI.

## Ruoli

| Ruolo | File | Compito |
|---|---|---|
| Implementer | `.claude/agents/implementer.md` | Scrive il codice. Prima di toccare un simbolo interroga il grafo. |
| Reviewer | `.claude/agents/reviewer.md` | Non riscrive produzione. Approva solo se il raggio d'impatto AFI è coperto. |

Lancia i due agent in sequenza (implementer → reviewer). Il reviewer non parte senza il blocco `AFI:` nell'handoff.

## AFI

Playbook condiviso: `.claude/skills/afi-contigo/SKILL.md`.
Plugin locale: `afi@local-dev`. Wrapper: `${CLAUDE_PLUGIN_ROOT}/scripts/afi` (fallback in `settings.local.json`).
