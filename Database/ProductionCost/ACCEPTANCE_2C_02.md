# Nghiệm thu Giai đoạn 2C-02 — Phân bổ 622/627 + EXP_TO_154_ONLY

Ngày: 2026-11-02 (tích hợp engine/DB — 4 mục review)  
Phạm vi: Engine C#, quy tắc Bước 3, script SQL, API POST **gate chặn ghi**.  
**Không** kết nối DB · **Không** chạy migration · **Không** bật `EnableWrites` · **Không** 2C-03.

---

## 0. Phân biệt trạng thái triển khai (bắt buộc)

| Hạng mục | Trạng thái thật |
|----------|----------------|
| `AllocationEngine` C# (type 0/1, byDepartment, REDUCTION, làm tròn, Σ=P_A) | **Đã implement** + unit test |
| `GkRunStatus` / `AllocLoadGuard` / `AllocDetailKey` | **Đã implement** + unit test |
| `ExpTo154Rules` (cấm N155/C154, N632/C155, N632/C154; cho N154/C621–627) | **Đã implement** + unit test |
| `CostEngineExclusive` / `ConfirmIdempotency` (NOOP + atomic history) | **Đã implement** (logic thuần) |
| API `POST .../allocation/preview` | **Chỉ DryRun payload** — `Persisted=false`; chưa đọc/ghi DB |
| API `POST .../allocation/confirm` | **Chặn gate** — skeleton TX owner / SP call (chưa thực thi DB) |
| API `POST .../transfer/preview\|confirm` | Preview: kiểm tra rule; Confirm: **chặn gate** |
| `sp_pc_alloc_load_pool` / `load_basis_qty` / `alloc_confirm` / `assert_…_exp` | **Script bàn giao** — chưa coi là đã chạy DEV |
| `sp_pc_alloc_calculate` | **Skeleton SIGNAL** — chưa body SQL |
| PeriodLock `EXP_TO_154_ONLY` (nhận `ALLOC_CONFIRMED`) | **Đã vá nguồn 2b_04** — user tự cài / patch DEV |
| DDL `POOL_DEPARTMENT_ID` + UNIQUE mới | **Script bàn giao** `20261102_2c02_ddl_alloc_detail_key.sql` |

**Không báo cáo** Preview/Confirm DB hay `sp_pc_alloc_calculate` là đã vận hành thật.

---

## 1. Bốn mục tích hợp (review)

### 1.1 Đồng bộ Confirm B2 ↔ B3
- B2 Confirm → `RUN.STATUS=ALLOC_CONFIRMED` + `COST_ALLOCATION=DONE` (không nhảy `CONFIRMED` toàn kỳ).
- B3 `EXP_TO_154_ONLY` gọi `sp_pc_assert_allocation_ready_for_exp` — chấp nhận `ALLOC_CONFIRMED` hoặc `CONFIRMED`, đúng `RUN_ID+RUN_VERSION`, step DONE, có tổng phân bổ.
- Ba rule B-I **không đổi**.

### 1.2 Transaction owner
- `sp_pc_alloc_confirm` **không** `START TRANSACTION/COMMIT` — C# caller sở hữu TX.
- Idempotent: `NOOP_ALREADY_CONFIRMED` khi DONE + khóa phân bổ + cùng version (không history).
- History chỉ khi step thực sự đổi; fail giữa chừng → caller ROLLBACK giữ số liệu/status cũ.
- C# `ConfirmIdempotency.PlanConfirm` / `ShouldWriteSuccessHistory` map hành vi trên (unit test, chưa đường DB).

### 1.3 Bảo vệ load_pool
- `sp_pc_alloc_load_pool` SIGNAL tuyệt đối nếu `RUN.STATUS ∈ {ALLOC_CONFIRMED, CONFIRMED}` — **không phụ thuộc STEP** (thiếu / chưa DONE vẫn chặn DELETE).
- Chỉ DRAFT/PREVIEWED/REOPENED (và version khớp header) được tính lại.
- Unit: `AllocLoadGuard` — mid-fail / STEP null không DELETE → pool cũ còn.
- PeriodLock canonical: `migrations/20261102_2b_04_…` **và** `StoredProcedures/PeriodLock/CogsSummary/01_sp_period_lock_cogs.sql` đồng bộ.
- DDL UNIQUE: chỉ cần khi schema cũ thiếu cột — xem `2c02_README.md`.

### 1.4 Khóa phân bổ C# ↔ MySQL
- UNIQUE: `(RUN_ID, RUN_VERSION, PRODUCT_ID, DEPARTMENT_ID, ACCOUNT_CD, POOL_DEPARTMENT_ID)`.
- DTO/Repository đọc `PoolDepartmentId`; không `MIN(DEPARTMENT_ID)`.
- Cùng `PRODUCT_ID` nhiều `DEPARTMENT_ID` + shared/dept pool giữ đủ dòng.

---

## 2. File tạo mới / chỉnh sửa

### Logic C#
- `Services/ProductionCost/Allocation/GkRunStatus.cs` — **mới**
- `Services/ProductionCost/ConfirmIdempotency.cs` — NOOP / atomic history
- `Services/ProductionCost/Transfer/ExpTo154Rules.cs` — assert theo `ALLOC_CONFIRMED`
- `Models/ProductionCostModels.cs` — `PoolDepartmentId` trên DTO
- `Repositories/ProductionCostRepository.cs` — SELECT + ORDER BY khóa mới
- `ProductionCostWriteService` — comment skeleton TX owner (Confirm vẫn gate)

### SQL bàn giao
| File | Vai trò |
|------|---------|
| `migrations/20261102_2c02_ddl_alloc_detail_key.sql` | **DDL migration** — cột + UNIQUE |
| `migrations/20261102_2c02_sp_alloc.sql` | load_pool bảo vệ, confirm→ALLOC_CONFIRMED, assert EXP, calculate skeleton |
| `migrations/20261102_2c02_patch_period_lock_exp_status.sql` | Ghi chú patch nếu 2b_04 cũ đã cài |
| `migrations/20261102_2b_04_sp_period_lock_cogs.sql` | **Đã vá** nhánh EXP (B-I nguyên) |
| `01_production_cost_tables.sql` / `2b_01_tables.sql` | Schema chuẩn mới |
| `migrations/20261102_2c02_verify_readonly.sql` | SELECT kiểm tra |

---

## 3. Build & unit test

```text
dotnet build API_AMNOTE_WEB.csproj -o _build_2c02_fix_out
→ Build succeeded. 0 Error(s)

dotnet test --filter FullyQualifiedName~ProductionCost
→ Passed: 49, Failed: 0
```

Checklist tích hợp (không DB):
| Case | Kết quả |
|------|---------|
| B2 → ALLOC_CONFIRMED; B3 nhận ALLOC_CONFIRMED; không hạ CONFIRMED | PASS |
| Confirm trùng version → NOOP; history chỉ khi voucher+step OK | PASS |
| load_pool / guard chặn CONFIRMED; mid-fail giữ pool cũ | PASS |
| UNIQUE key shared+dept; cùng PRODUCT nhiều DEPT | PASS |
| Ba rule B-I / ExpTo154 forbid matrix (giữ nguyên) | PASS |

---

## 4. Script user tự chạy (DEV/TEST — sau 2B)

Thứ tự đề xuất:
1. `SOURCE 20261102_2c02_ddl_alloc_detail_key.sql` *(nếu bảng đã tạo theo UNIQUE cũ)*
2. `SOURCE 20261102_2c02_sp_alloc.sql` *(gồm `sp_pc_assert_allocation_ready_for_exp`)*
3. Cài lại `20261102_2b_04_sp_period_lock_cogs.sql` bản đã vá **hoặc** làm theo `20261102_2c02_patch_period_lock_exp_status.sql`
4. `SOURCE 20261102_2c02_verify_readonly.sql`

---

## 5. Dừng

Tích hợp 2C-02 xong để duyệt trước 2C-03.  
Không tự chuyển 2C-03 · không bật ghi DB · không thực thi SQL trên môi trường thật.
