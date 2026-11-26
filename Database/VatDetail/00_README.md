# VAT Detail — SQL deployment (DEV/TEST only)

Do **not** run `Database/database.sql` in full.  
Do **not** deploy to production without review.

## Order

| # | Script | Database | Purpose |
|---|---|---|---|
| 1 | `01_alter_chitdetailinfo_vat_direction.sql` | Company DB | Add `VAT_DIRECTION` (idempotent) |
| 2 | `02_seed_sys_code_vat_categories.sql` | **Manager DB** (`sys_code`) | Seed `VAT_BUY_CATEGORY` / `VAT_SELL_CATEGORY` |
| 3 | `03_sp_setchitdetail.sql` | Company DB | Persist VAT only when `VAT_DIRECTION` is set |
| 4 | `04_sp_getchitdetail_batch.sql` | Company DB | Return 4 VAT fields |

## Notes

- Columns `VAT_RATE_CD`, `VAT_RATE_PERCENT`, `VAT_CUSTOMS_EXCHANGE_RATE` are already in schema; do not recreate them.
- Historical rows keep `VAT_DIRECTION = NULL` — no mass UPDATE.
- Seed does not overwrite existing `sys_code` rows.
- After deploy, verify with create → reopen → edit-other-fields → save again on TEST.
