# Nightly slices

Launch: `./run.ps1 -Max -Slice <id> -o execution-fanout`

Packing: Max window 10.0M × fill 80% = **8.0M cap** (S=0.5M M=1.0M L=1.8M). Not wall-clock.

| Slice | Tasks | Tokens | Title |
|-------|-------|--------|-------|
| `r0-a` | 4 | 2.5M | R0-a platform bootstrap |
| `r0-b` | 6 | 7.4M | R0-b azure infrastructure |
| `r0-c` | 4 | 4.3M | R0-c azure infrastructure |
| `r0-d` | 6 | 6.1M | R0-d ci cd delivery |
| `r0-e` | 6 | 6.9M | R0-e backend foundation |
| `r0-f` | 2 | 2.0M | R0-f backend foundation |
| `r0-g` | 2 | 2.0M | R0-g identity workspace |
| `r0-h` | 4 | 3.0M | R0-h document ingestion |
| `r0-i` | 2 | 1.5M | R0-i web client |
| `r0-j` | 2 | 1.5M | R0-j mobile scaffold |
| `r0-k` | 1 | 1.8M | R0-k R0 integration |
| `r1-a` | 4 | 4.3M | R1-a extraction pipeline |
| `r1-b` | 4 | 4.8M | R1-b contract schema |
| `r1-c` | 3 | 3.0M | R1-c portfolio contract 360 |
| `r1-d` | 4 | 4.8M | R1-d ask contigo citations |
| `r1-e` | 2 | 1.5M | R1-e validation corrections |
| `r1-f` | 1 | 1.8M | R1-f R1 integration |
| `r2-a` | 4 | 3.5M | R2-a renewal engine |
| `r2-b` | 2 | 2.0M | R2-b cancellation alerts |
| `r2-c` | 2 | 2.0M | R2-c renewal dashboard |
| `r2-d` | 1 | 1.8M | R2-d R2 integration |
| `r3-a` | 4 | 4.0M | R3-a benchmark service |
| `r3-b` | 4 | 4.3M | R3-b savings engine |
| `r3-c` | 2 | 2.0M | R3-c savings dashboard |
| `r3-d` | 1 | 1.8M | R3-d R3 integration |
| `r4-a` | 4 | 4.8M | R4-a quote extraction |
| `r4-b` | 2 | 2.8M | R4-b quote assessment |
| `r4-c` | 4 | 4.8M | R4-c negotiation strategy |
| `r4-d` | 1 | 1.8M | R4-d Day-1 path |
