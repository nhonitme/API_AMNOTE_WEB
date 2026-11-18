# G-K real-flow audit + atomic REOPEN B-I (2026-11-18)

**Status:** SOURCE implementation on feature branch; **NOT DB integration-tested**, **NOT deployed**.
**Deployment policy:** Use DEV/TEST company database first; never run these scripts in production without backup, approved change window and successful acceptance tests.

## 1. G-K demo-flow audit (stage 1)

- B1 menu had a fake local "preview successful" state and `faRows=[]`, hardcoded checkmarks and zero amount. Removed the fake preview and misleading ledger figures. The B1 action still calls a **real B-I PeriodLock job**; status DONE is reloaded from DB. The "Tải trạng thái B-I" button only refreshes DB status, **not** an FA financial validation.
- B2/B4/B5 previews use real DB sources when no DryRun payload is supplied (as in the G-K UI); Confirm uses `ProductionCost:EnableWrites`, caller-owned transaction, DB provenance validation and real history. Internal test/dry-run API payload paths remain, not used by the G-K menu.
- B3 Confirm calls GK's `EXP_TO_154_ONLY` via PeriodLock in the existing shared transaction; no fake COGS post.
- B6 runs real PeriodLock `PROFIT_LOSS_REPORT`. Its preliminary "Kiểm tra B1–B5" only checks persisted step statuses; the real PeriodLock validation occurs during execution.
- Removed old demo labels/unused demo state, corrected the B4 "ID2720 p=0.5" preset and restored persisted preset selection in frontend.
- Frontend PeriodLock polling now requires explicit `Status=DONE`; progress percentage alone cannot signal completion. B-I controls its actual `FromPeriodYm` from recorded locked-month milestones: **starting B1/B6 may process missing earlier months**. This behavior is unchanged by G-K.
- Frontend G-K status resolution preserves `REOPENED` when the related B-I step is already `OPEN`, but never presents `REOPENED` while B-I is still `DONE`.

### Scope gap to resolve before claiming full accounting support

The existing B-I `FA_PREPAID_LOCK` scripts read `fa_asset`, `fa_asset_depre_posted`, and related asset allocations. **No independent prepaid-expense/CPTT source and reversal was identified in these scripts.** Do not claim all CPTT entries are reversed until their actual tables/flow are separately traced and tested. This change reuses B-I's *existing* behavior only.

## 2. Atomic B-I REOPEN from G-K (stage 2)

`ConfirmReopenAsync` obtains G-K RUN+period locks and calls `ReverseReopenSideEffectsAsync` in the same DapperSession, before `PersistReopenAsync` and history insertion. Cascade order:

1. B6 `PROFIT_LOSS_REPORT`: `sp_period_lock_pl_unlock_core` (delete closing snapshot + PL lock vouchers, B-I step OPEN).
2. B5 `VALUATION`: restore original `INPUT_ID`/`OUTPUT_ID` prices from price audit, reset applied flag, reverse G-K valuation voucher.
3. B3 `EXP_TO_154_ONLY`: soft-reverse G-K transfer, require engine and B-I status to release; if no linked voucher, check for orphan vouchers, then call `sp_period_lock_cogs_unlock(...,'GK')`.
4. B1 `FA_PREPAID_LOCK`: `sp_period_lock_fa_prepaid_unlock_core` (same FA reversal as existing B-I menu).
5. Re-derive and persist B-I month status in the same transaction.
6. Persist G-K working-version REOPENED, event history and commit.

The REOPEN planner now merges authoritative B-I statuses for B1/B6 (and B3 only when COST_ENGINE=GK) so it does not skip a B-I DONE step absent from `production_cost_step`.

### SQL deployment order (DEV/TEST first)

On each **company DB** that will use the new API, install updated SQL before deploying backend. Run from repository root in a trusted MySQL client with proper privileges and a backup:

```sql
SOURCE Database/StoredProcedures/PeriodLock/FaPrepaid/05_sp_period_lock_fa_prepaid_unlock.sql;
SOURCE Database/StoredProcedures/PeriodLock/ProfitLoss/01_sp_period_lock_pl.sql;
```

These scripts create transaction-free `*_unlock_core` procedures and preserve original B-I `*_unlock` procedure names as self-committing wrappers. **Never call the wrappers from G-K's transaction.** Verify other procedures invoked within the cores (`sp_period_lock_step_upsert`, `sp_period_lock_month_upsert`, and `sp_period_lock_month_derive_status`) do not start/commit/rollback caller transaction. DDL changes themselves are not transaction-safe and require maintenance controls.

Run **read-only preflight** from the company database:

```sql
SELECT ROUTINE_NAME, ROUTINE_TYPE
FROM information_schema.ROUTINES
WHERE ROUTINE_SCHEMA = DATABASE()
  AND ROUTINE_NAME IN (
    'sp_period_lock_fa_prepaid_unlock_core',
    'sp_period_lock_pl_unlock_core',
    'sp_period_lock_cogs_unlock',
    'sp_period_lock_step_upsert',
    'sp_period_lock_month_derive_status',
    'sp_period_lock_month_upsert'
  )
ORDER BY ROUTINE_NAME;

SELECT TABLE_NAME, ENGINE
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN (
    'acc_period_lock_step','acc_period_lock_month',
    'production_cost_run','production_cost_step','production_cost_period',
    'production_cost_receipt','production_cost_price_audit',
    'production_cost_history','production_cost_voucher_ref',
    'chitinfo','chitdetailinfo','chitdescriptioninfo',
    'chitinfo_ext','chitdetailinfo_ext','chitdetaildescriptioninfo',
    'chit_inventory_input','chit_inventory_output',
    'fa_asset_depre_posted','fa_asset_depre_posted_alloc',
    'closingmonth_detail'
  )
ORDER BY TABLE_NAME;

-- Replace the two placeholders with an authorized DEV/TEST company and period.
SELECT COMPANY_CD, PERIOD_YM, STEP_CODE, STATUS
FROM acc_period_lock_step
WHERE COMPANY_CD = 'DEV_COMPANY_CD'
  AND PERIOD_YM = 'YYYYMM'
ORDER BY STEP_CODE;

SELECT COMPANY_CD, PERIOD_YM, COST_ENGINE, COGS_RULE, STATUS
FROM production_cost_period
WHERE COMPANY_CD = 'DEV_COMPANY_CD'
  AND PERIOD_YM = 'YYYYMM';
```

**Preflight must verify all tables affected by a single REOPEN are transactional (InnoDB) and the called procedures do not commit on behalf of the caller.** Do not enable this feature against MyISAM tables. Verify routines with `SHOW CREATE PROCEDURE ...` and existing permissions.

## 3. DEV/TEST acceptance matrix

- [ ] Real B1 execution: no success toast from a purely local preview, actual PeriodLock job DONE + real DB status, including a company with zero assets.
- [ ] B2–B5: real DB preview; Confirm writes real row/history; repeat OperationId is idempotent; stale RUN_VERSION rejected.
- [ ] Real B6 execution and snapshot/vouchers exist in DB.
- [ ] Reopen B6 only: PL vouchers/snapshot reversed and B-I OPEN; G-K B6 REOPENED; old G-K snapshot/history preserved.
- [ ] Reopen B5: restore original inventory prices and IS_APPLIED; no B-I B1 opened.
- [ ] Reopen B3: GK voucher reversed, COST_ENGINE released, B-I COGS OPEN. Test both with linked voucher and zero-voucher claim.
- [ ] Reopen B1 when B1–B6 DONE: B6→B5→B3→B1 all reversed, B-I statuses OPEN and month derived; G-K version+1 and REOPENED.
- [ ] Inject error at B1 after reversing B6/B5/B3: all money, voucher flags, B-I month/step statuses, G-K version/history ROLLBACK atomically.
- [ ] B-I manual unlock (existing menu) continues to work through wrapper.
- [ ] Accounting case: prepaid-expense/CPTT entries, non-asset allocation (if applicable), and later closed months. Review separately before production.
- [ ] Cross-browser FE build, ASP.NET build, MySQL 8.4 integration tests, source-side static checks.

**Not performed in this branch creation:** SQL SOURCE, real DEV DB operations, production changes, end-to-end test, FE/BE compile. `ProductionCost:EnableWrites` is **not changed**. This PR should remain draft until these tests pass.
