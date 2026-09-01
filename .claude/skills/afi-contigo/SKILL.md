---
name: afi-contigo
description: >
  Playbook AFI obbligatorio per implementer e reviewer del processo Contigo.
  Usalo all'avvio di ogni turno di implementazione o review, e ogni volta che stai
  per grep/read per tracciare caller, import, implementazioni, dipendenze o blast radius.
---

# AFI nel processo Contigo

Implementer e reviewer **non** tracciano relazioni a colpi di grep. AFI risolve caller, import e dispatch come il compilatore. Grep/Read restano per presenza di testo e lettura di un file già noto.

## Wrapper

Non è sul PATH. In ordine:

```bash
AFI="${CLAUDE_PLUGIN_ROOT}/scripts/afi"
[ -x "$AFI" ] || AFI="C:/Users/luca.la-malfa/source/repos/agentic-file-index/scripts/afi"
```

Su PowerShell:

```powershell
$AFI = if ($env:CLAUDE_PLUGIN_ROOT) { "$env:CLAUDE_PLUGIN_ROOT\scripts\afi.cmd" } else { "C:\Users\luca.la-malfa\source\repos\agentic-file-index\scripts\afi.cmd" }
```

Esegui i comandi dalla root della repo su cui stai lavorando (non da `contigo` se il codice sta altrove).

## Avvio di ogni sessione

```bash
"$AFI" status --json
```

| `readiness.state` | Azione |
|---|---|
| `fresh` | Query subito. |
| `absent` o `stale` e `autoScanSafe: true` | Avvia `scan` in background, intanto usa grep/read, poi ripeti le query AFI. |
| `stale` e `autoScanSafe: false` | Non riscannare da solo: è un grafo multi-lingua. Chiedi quali `--lang` re-indicizzare. |
| `unknown` | Tratta come stale. |

`scan` fa `env up` da solo. La prima immagine Docker sul host richiede 5–15 minuti: **annuncialo**, non chiedere il permesso.

```bash
"$AFI" scan . --lang <python|typescript|go>          # prima lingua
"$AFI" scan . --lang <altra> --append                # lingue successive — senza --append cancelli il grafo
```

Per `java` / `dotnet` / `rust` / `php` produci tu l'indice SCIP e passalo con `--scip`. Vedi la skill `scan`.

## Mai indovinare un ref

Un function-ref sbagliato torna "Function not found" in silenzio. Risolvi sempre:

```bash
"$AFI" query --list-functions | grep "NomeSimbolo"
```

Poi usa esattamente `<file>::<lexicalPath>` stampato lì.

## Query da usare

```bash
"$AFI" query --stats
"$AFI" query --structure-of <file>
"$AFI" query --called-by '<file>::<fn>'          # chi chiama
"$AFI" query --calls-from '<file>::<fn>'         # cosa chiama
"$AFI" query --callers-of '<file>::<fn>'         # chiusura transitiva caller
"$AFI" query --call-chain-from '<file>::<fn>' --depth 3
"$AFI" query --imported-by <file>
"$AFI" query --imports-of <file>
"$AFI" query --dependents-of <file>              # blast radius file
"$AFI" query --impact-of '<file>::<fn>'          # CALLS + IMPORTS
"$AFI" query --incoming '<class-or-iface>'       # IMPLEMENTS e edge custom
"$AFI" query --dead-code
"$AFI" query --search "descrizione o identificatore"
```

## Quando AFI, quando grep

| AFI | Grep / Read |
|---|---|
| Chi chiama / è chiamato da X | La stringa Y compare da qualche parte? |
| Import e dipendenze transitive | Leggere un path già noto |
| Blast radius, implementazioni | Pattern unico e letterale |
| Catena di chiamata | Un solo file |

## Errori `E_AFI_*`

`E_AFI_DOCKER_UNREACHABLE` — Docker spento, fermati. `E_AFI_CONTAINER_STALE` — non fare `env rebuild` da solo (cancella il DB). Per gli altri token apri la skill `env`.
