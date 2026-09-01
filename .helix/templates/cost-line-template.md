# Cost line (markdown row)

| id | traces_to | item | cadence | env | qty | unit_price | amount | source_url | source_date | notes |
|----|-----------|------|---------|-----|-----|------------|--------|------------|-------------|-------|
| infra-001 | ADR-003 | Azure DB SKU | monthly | dev | 1 | TODO | TODO | | | unconfirmed |

Cadence is `monthly` (run) or `one_off` (build). A price without URL+date is a
TODO, never an invented number. Mirror every row in the matching `*.json`.
