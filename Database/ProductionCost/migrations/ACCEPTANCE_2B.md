# Báo cáo nghiệm thu G-K Giai đoạn 2B (DEV/TEST)

Ngày: 2026-11-02  
Phạm vi: DDL + SP + exclusive lock + rule `EXP_TO_154_ONLY` + C# constant.  
**Chưa** chạy migration trên DB thật (không có DSN DEV xác nhận — `ConnectionStrings:AMManagerConnection = FromGlobalData`).  
**Chưa** production / **chưa** API tính giá thành thật / **chưa** cập nhật giá nhập kho TP.

---

## 1. Danh sách file đã sửa / thêm

### Migration (mới)

- `Database/ProductionCost/migrations/20261102_2b_00_install_dev.sql`
- `Database/ProductionCost/migrations/20261102_2b_01_tables.sql`
- `Database/ProductionCost/migrations/20261102_2b_02_sp_engine.sql`
- `Database/ProductionCost/migrations/20261102_2b_03_sp_nvl_basis.sql`
- `Database/ProductionCost/migrations/20261102_2b_04_sp_period_lock_cogs.sql`
- `Database/ProductionCost/migrations/20261102_2b_98_test_scenarios_dev.sql`
- `Database/ProductionCost/migrations/20261102_2b_99_verify_readonly.sql`
- `Database/ProductionCost/migrations/20261102_2b_rollback_dev.sql`
- `Database/ProductionCost/migrations/20261102_2b_rollback_cogs_bi_only.sql`
- `Database/ProductionCost/migrations/20261102_2b_README.md`
- `Database/ProductionCost/migrations/ACCEPTANCE_2B.md`

### Schema / SP canonical

- `Database/ProductionCost/01_production_cost_tables.sql` — UNIQUE nullable → `NOT NULL DEFAULT 0`
- `Database/StoredProcedures/PeriodLock/CogsSummary/01_sp_period_lock_cogs.sql` — đồng bộ 2B

### C# (đường gọi trực tiếp + background job PeriodLock)

- `Models/PeriodLockModels.cs` — `CogsTransferRuleCode.ExpTo154Only`
- `Repositories/PeriodLockRepository.cs` — tên rule hiển thị

**Không** sửa `Database/database.sql` như migration.

---

## 2. Stored procedure thay đổi

| SP | Thay đổi |
|----|----------|
| `sp_pc_engine_claim` | **Mới** — claim exclusive `GK`/`BI` theo `COMPANY_CD+PERIOD_YM` (`FOR UPDATE` + xử lý race UNIQUE) |
| `sp_pc_engine_assert_for_cogs` | **Mới** — map rule → engine (`EXP_TO_154_ONLY`→GK; 3 rule B-I→BI) |
| `sp_pc_engine_release_cogs` | **Mới** — unlock COGS → `COST_ENGINE=NONE` |
| `sp_pc_nvl_basis_build` | **Mới** — tiêu thức NVL; chia `CHITDETAIL`/`OUTPUT` chống nhân đôi |
| `sp_period_lock_cogs_validate` | Thêm `EXP_TO_154_ONLY` + `CALL sp_pc_engine_assert_for_cogs` |
| `sp_period_lock_cogs_build_xfer` | Nhánh G-K: 621(+6212 tách)/623 từ GL; 622/627 từ `production_cost_alloc_detail`; đối chiếu tổng; **không** 154→155/632. 3 rule B-I giữ nguyên |
| `sp_period_lock_cogs_create_voucher` | Idempotent xóa CT sót trước post; G-K chỉ post `EXP_TO_154` |
| `sp_period_lock_cogs_unlock` | Thêm `sp_pc_engine_release_cogs` |
| `sp_period_lock_cogs_calculate` / `rollback_period` | Giữ; đi qua validate mới |

Luồng khóa B-I bước 1 (`FA_PREPAID_LOCK`) và bước 3 (`PROFIT_LOSS_REPORT`): **không đổi**.

---

## 3. Cơ chế exclusive lock

1. Mọi COGS (API sync / background job PeriodLock) gọi `sp_period_lock_cogs_validate` → `sp_pc_engine_assert_for_cogs`.
2. `production_cost_period` 1 dòng / `(COMPANY_CD, PERIOD_YM)`:
   - `COST_ENGINE` ∈ `NONE|GK|BI`
   - `COGS_RULE` lưu rule đang giữ
3. Claim: `SELECT … FOR UPDATE`; nếu engine khác → `SIGNAL 45000`.
4. Hai request đồng thời tạo kỳ mới: UNIQUE + handler 1062 → một thắng, một đi tiếp assert.
5. Unlock COGS: xóa CT LOCK + step OPEN + `COST_ENGINE=NONE`.

---

## 4. Câu lệnh kiểm tra chỉ đọc

Chạy sau install:

```bash
mysql -D <dev_db> < 20261102_2b_99_verify_readonly.sql
```

Hoặc các SELECT trong file đó (tables, UNIQUE nullable risk, routines, flag `EXP_TO_154_ONLY`).  
Template đối chiếu phân bổ vs CT LOCK nằm trong comment cuối file verify + `20261102_2b_98_test_scenarios_dev.sql`.

---

## 5. Kết quả test DEV/TEST

| Case | Kết quả |
|------|---------|
| G-K khóa `EXP_TO_154_ONLY` | **Chưa execute** — chờ DSN DEV + dữ liệu alloc CONFIRMED |
| CT chỉ Nợ 154 / Có 621–627 | Script sẵn (T1) |
| Tổng 622/627 alloc = kết chuyển | Assert trong `build_xfer` (sai lệch >1đ → lỗi) + SELECT verify |
| Không 154→155/632 từ G-K | Logic SP + SELECT forbidden |
| B-I 3 rule kỳ non-GK | Logic giữ nguyên; template T6 |
| B-I chặn khi kỳ GK | `sp_pc_engine_claim` |
| G-K chặn khi kỳ BI | tương tự |
| Concurrent exclusive | `FOR UPDATE` + UNIQUE race |
| Re-run không trùng CT | delete vouchers trước post + unlock release |
| Lỗi/rollback nhất quán | `EXIT HANDLER` → `rollback_period` xóa CT; step không DONE nếu API chưa đánh dấu |

**Gate an toàn:** dừng ghi dữ liệu thật cho đến khi người duyệt xác nhận tên DB DEV/TEST + backup.

---

## 6. Phần còn chờ giai đoạn Backend API (sau 2B)

- API G-K All-in-One: tạo/confirm RUN, bước 2 phân bổ → ghi `production_cost_alloc_*`, set `CURRENT_RUN_ID`, gọi COGS với `ExpTo154Only`.
- Preview/calculate trước khóa (nếu tách khỏi PeriodLock job).
- Bước 4–5: tính giá thành / cập nhật giá nhập kho TP (**ngoài phạm vi 2B**).
- FE production wiring (hiện mock FE-01→04).
- CI/monitor Postman cho rule mới (tuỳ chọn).

---

## 7. Cách vận hành khi có DEV

1. Xác nhận `SELECT DATABASE();` = DEV/TEST đã backup.  
2. `SOURCE 20261102_2b_00_install_dev.sql` từ thư mục migrations.  
3. Deploy API kèm `ExpTo154Only`.  
4. Seed/confirm alloc G-K kỳ thử → chạy T1–T6.  
5. Gửi lại kết quả test thực tế để duyệt trước production / API costing.
