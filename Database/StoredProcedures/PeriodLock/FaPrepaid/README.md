# FA Prepaid Period Lock (TSCĐ)

Stored procedures for PeriodLock step `FA_PREPAID_LOCK`.

## Deploy

Run on each company database (`am_web_xxx`):

```bash
mysql -u user -p am_web_001 < 01_helpers.sql
mysql -u user -p am_web_001 < 02_sp_period_lock_fa_prepaid_validate.sql
mysql -u user -p am_web_001 < 03_sp_period_lock_fa_prepaid_calculate.sql
mysql -u user -p am_web_001 < 04_sp_period_lock_fa_prepaid_create_voucher.sql
mysql -u user -p am_web_001 < 05_sp_period_lock_fa_prepaid_unlock.sql
mysql -u user -p am_web_001 < 06_migrate_fa_asset_status.sql
```

## Procedures

| Procedure | Called by C# | Purpose |
|-----------|--------------|---------|
| `sp_period_lock_fa_prepaid_validate` | `RunFaPrepaidValidateAsync` | Validate assets, allocations, sum check |
| `sp_period_lock_fa_prepaid_calculate` | `RunFaPrepaidCalculateAsync` | Insert `fa_asset_depre_posted` + `_alloc` |
| `sp_period_lock_fa_prepaid_create_voucher` | `RunFaPrepaidCreateVoucherAsync` | Create `chitinfo`/`chitdetail`, link posted |
| `sp_period_lock_fa_prepaid_unlock` | `UnlockPeriodStepAsync` when step = `FA_PREPAID_LOCK` | Hard delete CT, `CANCEL_YN='Y'` on posted, set step `OPEN` |

## Helpers

- `sp_period_lock_fa_prepaid_rollback_period` — rollback `(COMPANY_CD, DEPRE_YM)` on create_voucher error
- `sp_period_lock_fa_prepaid_next_chit_no` — increment `sys_code_sequence` for `OT` (`IS_USE IN ('1','Y')`, `CODE_PATTERN`, `UPDATE_AT`)
- `sp_period_lock_fa_prepaid_last_day` — last calendar day of `YYYYMM`
- `sp_period_lock_fa_prepaid_resolve_depre_type` — FIRST / NORMAL / LAST

## Business rules

- Only `fa_asset.STATUS = 'IN_USE'` (chỉ tài sản đang sử dụng mới tính khấu hao; bảng `fa_asset` không có cột `ISDEL`)
- Status values: `NOT_IN_USE`, `IN_USE`, `SUSPENDED`, `SOLD` (legacy: `USING`→`IN_USE`, `STOP`→`SUSPENDED`, `FINISHED`→`SOLD`)
- Allocation: `fa_asset_depre_alloc.ISDEL = '0'`, both PERCENT and AMOUNT use `FIRST/NORMAL/LAST_ALLOC_AMT`
- 1 asset = 1 voucher per month (`INPUT_TYPE=LOCK`, `CHIT_TYPE=OT`, `LOCK_STEP_CODE=FA_PREPAID_LOCK`)
- `END_BOOK_AMT = REMAIN_DEPRE_AMT - sum(previous DEPRE_AMT) - current DEPRE_AMT`
- `ACCUM_DEPRE_AMT = ORIGINAL_AMT - END_BOOK_AMT`
- Does not update `fa_asset`
- Unlock soft-cancels `posted` (`CANCEL_YN='Y'`), giữ lịch sử đến lần khóa lại
- `calculate` xóa `posted`/`posted_alloc` đã hủy hoặc chạy dở trước khi INSERT (tránh trùng `uk_fa_asset_depre_posted_01`)

## Voucher tables (production schema)

| Logical | Physical table |
|---------|----------------|
| CT header | `chitinfo` |
| CT header extension | `chitinfo_ext` (mặc định `IS_LOCK/ISEXCEL/IS_CONFIRMED/IS_PAYMENT='0'`) |
| CT header description | `chitdescriptioninfo` (`LANG_TYPE='VIET'`) |
| CT detail | `chitdetailinfo` |
| CT detail description | `chitdetaildescriptioninfo` (`LANG_TYPE='VIET'`) |
| CT detail extension | `chitdetailinfo_ext` (`DEPARTMENT_ID`/`DEPARTMENT_CD` từ `fa_asset_depre_posted_alloc`) |

- `CHIT_YMD` format: `varchar(8)` = `YYYYMMDD`
- `INPUT_TYPE='LOCK'`, `LOCK_STEP_CODE='FA_PREPAID_LOCK'`, `CHIT_TYPE='OT'`
