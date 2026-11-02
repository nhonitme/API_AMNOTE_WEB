# G-K 2B — Final safety gate (pre-DEV)

**Chưa execute SQL trên database.** APPROVED_ENV còn placeholder → preflight sẽ FAIL cho đến khi bạn điền DB/host đã duyệt.

## 1. Preflight trước mọi DDL — đã xử lý

- `00_preflight_dev.sql` + `00_guard_inline.sql` + `APPROVED_ENV.sql`: **0 DROP/CREATE**.
- Khớp `DATABASE()`, `@@hostname` (exact hoặc CONTAINS), `ENV_TAG∈{DEV,TEST}`; chặn tên prod-like.
- Không chỉ tin `@GK_2B_ALLOW_DB` — nếu set phải trùng allowlist committed.
- Install/rollback: `SOURCE preflight` **trước** mọi DDL/DROP; FAIL → `PREPARE` SQL không hợp lệ sinh ERROR 1064 để dừng chain (`--abort-source-on-error`, cấm `--force`).
- Orchestrator: `Invoke-Gk2bInstall.ps1` từ chối khi còn placeholder.

## 2. Transaction bảo toàn chứng từ — đã xử lý

- Xác nhận `CreateSessionAsync` gọi `conn.BeginTransaction()` (`API_Build/Data/DapperExecutor.cs`).
- `RunCogsCreateVoucherAndCompleteAsync`: **Claim→Build→Delete→Post→Step DONE** trong một session; `Commit` / `Rollback`.
- `create_voucher` handler: chỉ `RESIGNAL` — **không** xóa CT khi lỗi.
- Hook `@GK_2B_FAIL_POINT`: `AFTER_DELETE` | `DURING_POST`.
- Test T10/T11 trong `98_test_scenarios_dev.sql` (cần DEV).

## 3. Rollback migration — đã xử lý

- `rollback_dev.sql`: preflight trước; inventory tables/routines; DROP IF EXISTS chỉ object 2B; restore COGS B-I; ưu tiên backup.

## Static check + build

Chạy local (không DB): `Invoke-Gk2bStaticCheck.ps1` + `dotnet build`.

## Test còn cần DEV

| ID | Cần DB |
|----|--------|
| Preflight PASS với APPROVED_ENV thật | Có |
| Install end-to-end + verify | Có |
| T9 BI reject giữ CT G-K | Có |
| **T10** AFTER_DELETE → rollback CT | Có |
| **T11** DURING_POST → rollback CT | Có |
| Rollback inventory + COGS B-I | Có (sau backup) |

**Không production. Không 2C** đến khi bạn duyệt kết quả DEV.
