# Báo cáo nghiệm thu G-K 2B + rà soát an toàn (pre-DEV)

Ngày cập nhật: 2026-11-02 (safety review — **chưa execute SQL**).

---

## File đã sửa / thêm (safety pass)

### Migration
- `20261102_2b_00_preflight_dev.sql` — **mới**: hard-stop `@GK_2B_ALLOW_DB` + `sp_pc_2b_require_allowed_db`
- `20261102_2b_00_install_dev.sql` — gọi preflight guard trước SOURCE
- `20261102_2b_01_tables.sql` — `CALL sp_pc_2b_require_allowed_db()` trước DDL
- `20261102_2b_02_sp_engine.sql` — tách check/claim; bỏ COMMIT; GET_LOCK; release có expected engine
- `20261102_2b_03_sp_nvl_basis.sql` — `SPLIT_QTY_ZERO` orphan, không chia đều
- `20261102_2b_04_sp_period_lock_cogs.sql` — handler an toàn; claim chỉ ở create; CONFIRMED+REDUCTION; unlock guard; overwrite assert
- `20261102_2b_98_test_scenarios_dev.sql` — assertion tự động (đúng cột schema)
- `20261102_2b_99_verify_readonly.sql` — (giữ)
- `20261102_2b_README.md` / `ACCEPTANCE_2B.md`

### Canonical / C#
- `Database/StoredProcedures/PeriodLock/CogsSummary/01_sp_period_lock_cogs.sql` — đồng bộ
- `Repositories/PeriodLockRepository.cs` — unlock truyền `p_EXPECTED_ENGINE='BI'`

---

## Test case đã bổ sung (98)

| ID | Nội dung | Assertion |
|----|----------|-----------|
| **T9** | G-K đã có CT → B-I `154_TO_632` bị reject | reject=1; CHIT_CNT/AMOUNT/DETAIL/ENGINE/RULE/STEP **không đổi** |
| T1 | CT EXP chỉ Nợ 154 / Có 621–627 | bad_lines=0; forbidden 154→155/632=0 |
| T4 | BI unlock kỳ GK bị reject | reject=1; số CT không đổi |
| T5 | validate không đổi `COST_ENGINE` | engine trước=sau |

Mô tả CT: `chitdescriptioninfo.DESCRIPTION` (không dùng `C.DESCRIPTION`).  
Khóa CT: `INPUT_TYPE='LOCK'`, `LOCK_STEP_CODE='COGS_SUMMARY'`, `CHIT_YMD=last day`.

---

## Vấn đề còn tồn tại / chờ DEV

1. **Chưa chạy** trên DB DEV — cần bạn cung cấp tên DB + backup.
2. **G-K unlock API** chưa có: mở kỳ GK phải `p_EXPECTED_ENGINE='GK'` (PeriodLock UI chỉ `'BI'` → đúng yêu cầu tách engine).
3. **Sau `v_write_started=1`**, nếu post lỗi giữa chừng: handler xóa CT (có thể mất bản cũ) — cần TX caller bao ngoài hoặc 2C staging.
4. **Seed dữ liệu** T9: kỳ `@ym_gk` phải đã có CT EXP + `COST_ENGINE=GK` + RUN CONFIRMED.
5. Chưa 2C / production / tính giá thành thật.

---

## Hướng dẫn preflight DEV/TEST

```sql
-- 1) Backup mysqldump schema+data DEV
-- 2) Trong mysql client (cwd = thư mục migrations):
SET @GK_2B_ALLOW_DB = '<TEN_DB_DEV_DA_DUYET>';
USE <TEN_DB_DEV_DA_DUYET>;
SOURCE 20261102_2b_00_preflight_dev.sql;
-- Nếu PASS:
SOURCE 20261102_2b_00_install_dev.sql;
SOURCE 20261102_2b_99_verify_readonly.sql;

-- 3) Sau khi có kỳ G-K khóa EXP thành công:
SET @cd='...'; SET @ym_gk='yyyyMM'; SET @user='dev_test';
SOURCE 20261102_2b_98_test_scenarios_dev.sql;
-- fail_cnt phải = 0
```

Sai `@GK_2B_ALLOW_DB` / sai `USE` → `SIGNAL 45000` **trước** CREATE TABLE / DROP PROCEDURE nghiệp vụ.

**Không triển khai production. Không chuyển 2C** cho đến khi bạn duyệt kết quả test 2B trên DEV.
