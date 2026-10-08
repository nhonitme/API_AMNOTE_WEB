# Period Lock — Đặc tả tổng hợp (tham chiếu lâu dài)

Tài liệu này gom **quy ước nghiệp vụ đã chốt**, **schema DB**, **danh sách SP**, **luồng C#/FE** và **checklist deploy**. Dùng khi sửa tính năng mà không cần tra lại chat.

---

## 1. Tổng quan

Khóa sổ kỳ kế toán theo tháng `YYYYMM`, gồm **3 bước** chạy tuần tự trong job nền:

| Order | STEP_CODE | Tên | Sub-step (C#/SQL) |
|------:|-----------|-----|-------------------|
| 1 | `FA_PREPAID_LOCK` | Khóa TSCĐ / CP trả trước | validate → calculate → create_voucher |
| 2 | `COGS_SUMMARY` | Tổng hợp giá vốn | validate → calculate → create_voucher |
| 3 | `PROFIT_LOSS_REPORT` | Báo cáo lãi/lỗ | validate → calculate → create_voucher |

- **Metadata**: `acc_period_lock_month`, `acc_period_lock_step`, `acc_period_lock_task`, `acc_period_lock_job`
- **CT khóa sổ**: `chitinfo` với `INPUT_TYPE='LOCK'`, `LOCK_STEP_CODE` = mã bước
- **Unlock**: thứ tự **3 → 2 → 1**; CT LOCK **hard DELETE** theo `LOCK_STEP_CODE` + ngày cuối tháng

---

## 2. Quy ước nghiệp vụ đã chốt

### 2.1 Số tiền kết chuyển (bước 2 & 3)

| Nhóm TK | Cách lấy số |
|---------|-------------|
| Chi phí / GV: 621, 622, 623, 627, 521, 632, 635, 641, 642, 811, 8211 | **Chỉ PS Nợ** tháng T |
| Doanh thu: 511, 515, 711 | **Chỉ PS Có** tháng T (đối xứng chi phí) |
| 8212 | **Net PS** tháng T: PS Nợ − PS Có |
| 911 → 4212 | Net 911 **sau** các dòng kết chuyển PL trong cùng job (ledger PS 911% + ảnh hưởng xfer) |

- **Không** dùng số dư đầu kỳ cho kết chuyển TK 5xx/6xx.
- **Không** nhận diện nhập kho; chỉ theo đầu TK trên chứng từ.
- Bước 3 **query trực tiếp** `chitdetailinfo` (không chạy lại trial balance).

### 2.2 Tài khoản hạch toán (`acclist_info`)

| Quy tắc | Chi tiết |
|---------|----------|
| Nguồn PS | TK thực trên CT (`chitdetailinfo.DEBIT` / `CREDIT`), join `ISABLEINPUT='1'`, prefix `621%`, `511%`, … |
| TK đích trên CT kết chuyển | **Không** hạch toán TK mẹ; resolve `MIN(ACC_CD)` where `ACC_CD LIKE '{prefix}%' AND ISABLEINPUT='1'` |
| Prefix đích bắt buộc resolve | 154, 155, 632 (COGS); 511, 911, 4212, 8212 (PL) |
| Gom tiền | Theo **TK nguồn thực + `DEPARTMENT_ID`** (`chitdetailinfo_ext`) |
| Không gom lên TK mẹ | Không rollup parent như trial balance report |

**Lưu ý `ISABLETYPE`**: `BALANCE_ACCOUNT_TWO_SIDE` áp dụng khi `ISABLETYPE = 2` (trên DB comment: *Enter customer* — dùng cho TK công nợ snapshot). Chưa lọc `DECISION` trên `acclist_info`.

### 2.3 Bước 2 — COGS

**Luồng trong cùng job** (FE chọn `p_RULE_CODE` trước):

1. **621/622/623/627 → 154** (luôn): CT nhóm `EXP_TO_154`
2. Theo rule:
   - `154_TO_155`: **154 → 155**, dừng (CT `154_TO_155`)
   - `154_TO_632`: **154 → 632** (CT `154_TO_632`)
   - `154_TO_155_TO_632`: 154→155 rồi **155→632**; số 155→632 = **số vừa 154→155 trong job** (CT `155_TO_632`)

**Số 154 cho bước ②** (theo phòng ban):

```
PS Nợ 154% (ISABLEINPUT=1) tháng T
+ tổng đã kết từ 621…627 → 154 cùng DEPARTMENT_ID
```

**Rule FE** (`PeriodLockPage`): `154_TO_155` | `154_TO_155_TO_632` | `154_TO_632`

**Không có PS Nợ giá vốn** (621/622/623/627/154 tháng T): bước 2 **bỏ qua** — không báo lỗi, không tạo CT LOCK.

**632 → 911**: chỉ ở **bước 3**, không ở bước 2.

### 2.4 Bước 3 — P&amp;L

**Cặp kết chuyển** (mỗi cặp = 1 CT LOCK riêng nếu có PS):

| TRANSFER_CODE | Nguồn | Đích | Side |
|---------------|-------|------|------|
| `521_TO_511` | 521% PS Nợ | 511 resolve | Nợ 511 / Có 521x |
| `511_TO_911` | 511% PS Có | 911 resolve | Nợ 511x / Có 911 |
| `515_TO_911` | 515% PS Có | 911 | |
| `632_TO_911` | 632% PS Nợ | 911 | Nợ 911 / Có 632x |
| `635_TO_911` | 635% PS Nợ | 911 | |
| `641_TO_911` | 641% PS Nợ | 911 | |
| `642_TO_911` | 642% PS Nợ | 911 | |
| `711_TO_911` | 711% PS Có | 911 | |
| `811_TO_911` | 811% PS Nợ | 911 | |
| `8211_TO_911` | 8211% PS Nợ | 911 | |
| `8212_NET` | 8212 net PS | 911 ↔ 8212 resolve | |
| `911_TO_4212` | Net 911/dept | 4212 resolve | Lãi: Nợ 911 / Có 4212; Lỗ: ngược lại |

**Không có PS cần kết chuyển lãi lỗ** tháng T: bước 3 **bỏ qua** CT LOCK — không báo lỗi; vẫn chạy **snapshot** `closingmonth_detail` nếu có `create_voucher`.

**`p_BALANCE_METHOD`**: `BALANCE_ACCOUNT` | `BALANCE_ACCOUNT_TWO_SIDE` — chỉ ảnh hưởng **snapshot** `closingmonth_detail` (TK `ISABLETYPE=2` giữ Nợ/Có riêng).

### 2.5 `closingmonth_detail`

| Hạng mục | Quy ước |
|----------|---------|
| **Khi ghi** | **Chỉ sau bước 3** (`sp_period_lock_pl_create_voucher` gọi snapshot) |
| **CLOSE_YMD** | Ngày **cuối tháng** khóa (`YYYYMMDD`), ví dụ `20250630` |
| **Nguồn số** | Logic giống `rpt_trial_balance_s06dn`: đầu kỳ + PS bù + PS tháng → END → tách Nợ/Có |
| **Đầu kỳ** | Ưu tiên snapshot `closingmonth_detail` gần nhất; không có → `before_states` (+ bank/customer/department) |
| **Unlock bước 3** | **Hard DELETE** rows `(COMPANY_CD, CLOSE_YMD)` — `sp_period_lock_closingmonth_delete` |
| **Không** ghi snapshot sau bước 1/2 | |

**DDL đã xác nhận trên DB** (`am_web_001.closingmonth_detail`):

```sql
CLOSE_YMD  char(8)   -- yyyyMMdd
ACC_CD     varchar(20)
DEBIT/CREDIT decimal(18,6)
ISDEL      char(1) default '0'
-- index: COMPANY_CD, CLOSE_YMD, ACC_CD
-- KHÔNG có UNIQUE (COMPANY_CD, CLOSE_YMD, ACC_CD) → xóa cũ trước khi insert
```

### 2.6 Chứng từ LOCK

| Hạng mục | Giá trị |
|----------|---------|
| `CHIT_YMD` | Ngày cuối tháng khóa |
| `CHIT_TYPE` | `OT` |
| `INPUT_TYPE` | `LOCK` |
| `LOCK_STEP_CODE` | `FA_PREPAID_LOCK` / `COGS_SUMMARY` / `PROFIT_LOSS_REPORT` |
| Số CT | **Nhiều CT/tháng/bước** (COGS: theo nhóm xfer; PL: theo TRANSFER_CODE) |
| Chi tiết | Tách **`DEPARTMENT_ID`** trên `chitdetailinfo_ext` |
| Số CT | Lấy từ `sys_code_sequence` (`OBJECT_TYPE='OT'`) |

---

## 3. Schema DB — bảng liên quan

### 3.1 Bắt buộc có trên DB công ty

| Bảng | Vai trò |
|------|---------|
| `acc_period_lock_*` | Metadata khóa sổ |
| `chitinfo` | Cột `INPUT_TYPE`, `LOCK_STEP_CODE` |
| `chitinfo_ext`, `chitdescriptioninfo` | Header CT |
| `chitdetailinfo` | Dòng Nợ/Có |
| `chitdetailinfo_ext` | **`DEPARTMENT_ID`**, `DEPARTMENT_CD` |
| `chitdetaildescriptioninfo` | Mô tả dòng |
| `acclist_info` | `ISABLEINPUT`, `ISABLETYPE`, `ISDEL`, `ACC_CD` (varchar 7 trên DB hiện tại) |
| `department_info` | Lookup `DEPARTMENT_CD` |
| `closingmonth_detail` | Snapshot cuối tháng (xem §2.5) |
| `before_states*` | Fallback đầu kỳ snapshot |
| `sys_code_sequence` | Sinh số CT OT |
| `fa_asset*`, `fa_asset_depre_*` | Bước 1 TSCĐ |

### 3.2 Bước 1 TSCĐ (đã deploy trên DB)

- `fa_asset.STATUS = 'IN_USE'` (legacy `USING` → migrate script `FaPrepaid/06_migrate_fa_asset_status.sql`)
- Posted: `fa_asset_depre_posted`, `fa_asset_depre_posted_alloc` (`DEPARTMENT_ID`, link `CHITDETAIL_ID`)
- Unlock FA: soft-cancel posted (`CANCEL_YN='Y'`), xóa CT qua join posted (khác COGS/PL unlock theo `LOCK_STEP_CODE`)

---

## 4. Stored procedures — inventory

### 4.1 Trên DB (từ `full.sql` / đã có)

**Metadata & job**

- `sp_period_lock_months_get`, `sp_period_lock_steps_get`, `sp_period_lock_current_status_get`
- `sp_period_lock_step_status_get`, `sp_period_lock_step_upsert`, `sp_period_lock_task_upsert`
- `sp_period_lock_month_upsert`, `sp_period_lock_month_derive_status`, `sp_period_lock_period_unlock`
- `sp_period_lock_job_create`, `sp_period_lock_unlock_job_create`, `sp_period_lock_job_get_*`, `sp_period_lock_job_update_progress`
- `sp_period_lock_fiscal_start_year_get`, `sp_period_lock_get_actual_*`

**Bước 1**

- `sp_period_lock_fa_prepaid_validate`, `_calculate`, `_create_voucher`, `_unlock`
- `sp_period_lock_fa_prepaid_last_day`, `_next_chit_no`, `_resolve_depre_type`, `_rollback_period`

**Stub (backend KHÔNG gọi)**

- `sp_period_lock_run_fa_prepaid`, `sp_period_lock_run_cogs_transfer`, `sp_period_lock_run_profit_loss` → chỉ `SELECT 1`

### 4.2 Cần deploy từ repo (bước 2/3 — **chưa có trên DB**)

**`Common/01_helpers.sql`**

- `sp_period_lock_delete_lock_vouchers`
- `sp_period_lock_closingmonth_delete`
- `sp_period_lock_closingmonth_snapshot`
- `sp_period_lock_step_require_done`, `sp_period_lock_step_require_open`

**`Common/02_transfer_helpers.sql`**

- `sp_period_lock_resolve_posting_acc`
- `sp_period_lock_lock_voucher_begin`, `_line`, `_end`
- `sp_period_lock_post_xfer_group`

**`CogsSummary/01_sp_period_lock_cogs.sql`**

- `sp_period_lock_cogs_build_xfer`
- `sp_period_lock_cogs_validate`, `_calculate`, `_create_voucher`, `_rollback_period`, `_unlock`

**`ProfitLoss/01_sp_period_lock_pl.sql`**

- `sp_period_lock_pl_insert_debit_xfer`, `_insert_credit_xfer`, `_build_xfer`
- `sp_period_lock_pl_validate`, `_calculate`, `_create_voucher`, `_rollback_period`, `_unlock`

### 4.3 Thứ tự cài đặt

Xem `00_install_all.sql`:

```
FaPrepaid/01_helpers.sql
FaPrepaid/02 … 05
Common/01_helpers.sql
Common/02_transfer_helpers.sql
CogsSummary/01_sp_period_lock_cogs.sql
ProfitLoss/01_sp_period_lock_pl.sql
```

---

## 5. Backend C# (`PeriodLockRepository`)

### 5.1 Job khóa sổ — SP được gọi

| Bước | validate | calculate | create_voucher |
|------|----------|-----------|----------------|
| 1 FA | `sp_period_lock_fa_prepaid_validate` | `_calculate` | `_create_voucher` |
| 2 COGS | `sp_period_lock_cogs_validate` (+ `p_RULE_CODE`) | `_calculate` | `_create_voucher` |
| 3 PL | `sp_period_lock_pl_validate` (+ `p_BALANCE_METHOD`) | `_calculate` | `_create_voucher` |

Sau mỗi sub-step: `sp_period_lock_task_upsert`, `sp_period_lock_step_upsert`, `sp_period_lock_job_update_progress`.

### 5.2 Unlock — SP được gọi

| Bước | SP dữ liệu | Metadata (C# thêm) |
|------|------------|---------------------|
| 1 | `sp_period_lock_fa_prepaid_unlock` | `UpsertStepAsync` + `UpsertMonthAsync` |
| 2 | `sp_period_lock_cogs_unlock` | idem (SP cũng gọi `step_upsert` → trùng nhưng OK) |
| 3 | `sp_period_lock_pl_unlock` (+ xóa `closingmonth_detail`) | idem |

Generic: `sp_period_lock_period_unlock(@COMPANY, @PERIOD, @STEP_CODE, @REASON, @USER)`

### 5.3 Progress units

- FA: **3** units/tháng (validate, calculate, create)
- COGS: **3** units/tháng
- PL: **3** units/tháng  
→ Tổng tối đa **9** units/tháng nếu chạy đủ 3 bước (`PeriodLockProgressUnit.cs`, `PeriodLockPage.tsx`)

### 5.4 Background job

- Job queue truyền `DatabaseName` vào repository (tránh lỗi thiếu HttpContext).
- HTTP API: `databaseName = null` → DapperContext dùng HttpContext.

---

## 6. Frontend (`PeriodLockPage.tsx`)

| Option | Giá trị |
|--------|---------|
| COGS rule | `154_TO_155`, `154_TO_155_TO_632`, `154_TO_632` (RadioGroup string) |
| PL balance | `BALANCE_ACCOUNT`, `BALANCE_ACCOUNT_TWO_SIDE` |
| Unlock order | 3 → 2 → 1 |

API: `PeriodLockController` → job queue → polling progress.

---

## 7. Checklist trước khi test bước 2/3

- [ ] Deploy SP mục §4.2
- [ ] Bảng `closingmonth_detail` tồn tại (`CLOSE_YMD` char(8))
- [ ] `chitinfo.INPUT_TYPE`, `LOCK_STEP_CODE` có trên DB
- [ ] Chart TK: tồn tại con `ISABLEINPUT='1'` cho prefix 154, 155, 632, 511, 911, 4212, 8212
- [ ] `sys_code_sequence` cấu hình OT (bước 1 đã chạy được → OK)
- [ ] CT nguồn có `chitdetailinfo_ext.DEPARTMENT_ID` nếu cần tách phòng ban
- [ ] Bước 1 **DONE** trước khi chạy bước 2; bước 2 **DONE** trước bước 3

---

## 8. Rủi ro / hạn chế hiện tại

| # | Mô tả | Ghi chú khi sửa sau |
|---|--------|---------------------|
| 1 | Không lọc `acclist_info.DECISION` | Thêm filter nếu DN dùng nhiều thông tư |
| 2 | `ISABLETYPE=2` = TWO_SIDE snapshot | Xác nhận mapping TK công nợ trên từng DN |
| 3 | `ACC_CD varchar(7)` | Mã TK > 7 ký tự cần ALTER bảng |
| 4 | Stub `run_cogs` / `run_profit_loss` trên DB | Chạy `99_drop_legacy_stubs.sql` sau deploy |
| 5 | `sp_period_lock_latest_unlockable_period_get` tham chiếu `period_lock_month` | Legacy; không dùng bởi repo mới |
| 6 | Unlock COGS/PL: `step_upsert` gọi 2 lần (SP + C#) | Refactor nếu muốn gọn |
| 7 | Snapshot chưa rollup TK mẹ | Đúng spec — lưu TK chi tiết như trên sổ |
| 8 | CT LOCK **có** vào PS tháng T khi snapshot (sau bước 3) | Đúng spec |

---

## 9. Tham chiếu ngoài repo

| File | Nội dung |
|------|----------|
| `rpt_trial_balance_s06dn` | Cách tính đầu kỳ / PS / số dư; dùng `CLOSE_YMD` |
| `new 26.txt` (Downloads) | Mapping kết chuyển, Q&A nghiệp vụ |
| `closingmonth_detail` DDL | Đã khớp store (`CLOSE_YMD` char(8)) |

---

## 10. File map trong repo

```
Database/StoredProcedures/PeriodLock/
├── SPEC.md                          ← tài liệu này
├── README.md                        ← tóm tắt + link
├── 00_install_all.sql
├── 99_drop_legacy_stubs.sql         ← DROP stub/legacy trên DB (optional)
├── Common/
│   ├── 01_helpers.sql               ← snapshot, delete CT, step require
│   └── 02_transfer_helpers.sql      ← resolve TK, tạo CT theo nhóm
├── FaPrepaid/                       ← bước 1
├── CogsSummary/01_sp_period_lock_cogs.sql
└── ProfitLoss/01_sp_period_lock_pl.sql

API_AMNOTE_WEB/
├── Repositories/PeriodLockRepository.cs
├── Controllers/PeriodLockController.cs
├── Services/BackgroundJobs/PeriodLock/
└── Helpers/PeriodLockProgressUnit.cs

AMNOTE_WEB_APP/
└── src/pages/Module/ClosingMonth/PeriodLockPage.tsx
```

---

## 11. Dọn dư thừa (đã rà 2026)

### Đã xóa khỏi repo

| Thành phần | Lý do |
|------------|--------|
| `sp_period_lock_get_period_debit` | Không còn caller sau rewrite `build_xfer` |
| `sp_period_lock_get_period_credit` | Idem |
| `RunFaPrepaidLockAsync()` (C#) | Dead code; gọi stub `run_fa_prepaid` |
| `UnlockPeriodAsync()` (C# interface + repo) | Không được gọi; signature cũ thiếu `p_STEP_CODE` |

### Chạy trên DB (optional)

`99_drop_legacy_stubs.sql`: DROP stub `run_*`, helper cũ, SP metadata không dùng bởi `PeriodLockRepository`.

### Giữ lại (có lý do)

| Thành phần | Lý do giữ |
|------------|-----------|
| `*_calculate` + `*_create_voucher` | Job progress 3 bước |
| `step_upsert` trong `*_unlock` SP | An toàn khi gọi SP trực tiếp (C# cũng upsert) |
| `sp_period_lock_fa_prepaid_rollback_period` | Khác `delete_lock_vouchers` (xóa thêm `fa_asset_depre_posted`) |
| `FaPrepaid/00_install_all.sql` | Install cục bộ bước 1 |
| `pl_insert_*` nhánh `ELSE` | Nhánh gom theo dept (chưa dùng) |

---

*Cập nhật lần cuối: theo spec chốt trong phiên triển khai bước 2/3 (CLOSE_YMD, PS Nợ/Có, ISABLEINPUT resolve, snapshot sau bước 3).*
