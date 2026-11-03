# Nghiệm thu Giai đoạn 2C-06 — Backend Integration Review (B2–B5 Confirm + History)

Ngày: 2026-11-03  
Phạm vi: **Source review only** — rà soát module ProductionCost hiện có, lập kế hoạch tích hợp Confirm B2–B5 và lifecycle history.  

**Không** kết nối DB · **Không** chạy migration · **Không** bật `EnableWrites` · **Không** sửa số liệu KT/kho · **Không** deploy.

Tham chiếu nghiệm thu trước: `ACCEPTANCE_2C_01` … `ACCEPTANCE_2C_05`.

---

## 0. Verdict

| Lớp | Trạng thái |
|-----|------------|
| Engine nghiệp vụ C# (B2–B5 + REOPEN/RECALC/COMPARE) | **Hoàn thành** — unit-tested, pure / dry-run |
| API đọc GET | **Hoàn thành** (2C-01) |
| API Preview (DryRun payload) | **Hoàn thành** — `Persisted=false` |
| API Confirm + Preview-from-DB | **B2–B5 Confirm (source)** — gate OFF; B2/B4/B5 source guard; B3 PeriodLock cùng TX; B5 INPUT_ID capability OFF |
| Repository ghi / TX session G-K | **2C-06A Implemented** + B2/B3/B4 persist + B5 surface (capability-gated) |
| SP persist B2 (confirm/load/assert) | **Script bàn giao** — `sp_pc_alloc_confirm` **không** INSERT history; chưa SOURCE DEV |
| SP calculate / WIP / valuation / reopen | **Skeleton SIGNAL** hoặc **Pending** — B4 persist bằng C# UPSERT; B5 INPUT_ID SQL skeleton; REOPEN persist C# (không SP) |
| History idempotency (`OperationId` + `STABLE_KEY`) | **Logic C# + DDL script 2C-06A** — Confirm B2–B5 + REOPEN ghi history từ C#; chưa SOURCE DEV |
| Tái sử dụng kho / PeriodLock | **B3 gắn PeriodLock** — B5 apply kho/632 **capability-gated** (production OFF); REOPEN block side-effect |

**Kết luận (cập nhật 2C-06F):** REOPEN + history + integration readiness source-tested; WriteGate OFF; không DB. Dừng nghiệm thu source toàn đoạn 2C-06 — chưa EnableWrites / production.

---

## 1. Current implementation status

### 1.1 Completed business logic (pure / dry-run)

| Bước | Thành phần | Hành vi giữ nguyên khi Confirm |
|------|------------|--------------------------------|
| B2 | `AllocationEngine`, `PoolAccountRules`, `NvlBasisCalculator`, `AllocDetailKey`, `AllocLoadGuard`, `GkRunStatus` | Type 0/1, byDepartment, REDUCTION, làm tròn dòng cuối; Confirm → `ALLOC_CONFIRMED` **không** `CONFIRMED` toàn kỳ |
| B3 | `ExpTo154Rules`, `CostEngineExclusive`, PeriodLock `EXP_TO_154_ONLY` (source đã vá) | Chỉ N154/C621–627; cấm 154→155/632; exclusive GK↔BI |
| B4 | `WipCostEngine`, `CostSourceRules`, `ReceiptPricePrep`, `ReceiptReconcile`, `CostRunCompare`, `WipRecalcGuard` | Loại XFER khỏi incurred; khóa PRODUCT+DEPT; `COST_CALC=DONE` only |
| B5 | `ValuationAverageCost` → `InventoryOutPriceEngine`, `ValuationPrerequisite`, `Cogs632PreviewRules`, `ValuationApplyPlan` | 4 PP; nhóm PRODUCT+STORE+CHIT_TYPE; apply theo `INPUT_ID`+`COMPANY_CD`; không post 632 trùng |
| Lifecycle | `ReopenRules`, `RecalcRules`, `RunVersionRules`, `HistoryPlan`, `SessionCompare`, `LifecycleRequestGuard`, `ConfirmIdempotency` | Version +1 lần đầu; giữ version khi đã REOPENED; OperationId idempotent; không DELETE snapshot CONFIRMED |

### 1.2 Placeholder / blocked paths

`ProductionCostWriteService` — mọi Confirm và Preview-from-DB:

| Method | Hành vi hiện tại |
|--------|------------------|
| `PreviewAllocation` (không DryRun) | Throw — “chưa kích hoạt từ DB” |
| `ConfirmAllocation` | **2C-06C:** `AllocationConfirmSourceGuard` chặn ghi thật khi chưa load pool/basis từ DB (độc lập EnableWrites) |
| `PreviewTransfer` (gate OFF) | Rule-only preview |
| `ConfirmTransfer` | **2C-06C Implemented (source)** — PeriodLock EXP_TO_154_ONLY cùng TX G-K |
| `PreviewCostCalc` (không WipDryRun) | Throw |
| `ConfirmCostCalc` | **2C-06D Implemented (source)** — `CostCalcConfirmSourceGuard` chặn khi chưa load nguồn từ DB; UPSERT source/wip/receipt; COST_CALC=DONE only |
| `PreviewValuation` (không ValuationDryRun) | Throw |
| `ConfirmValuation` | **2C-06E Implemented (source)** — `ValuationConfirmSourceGuard` + INPUT_ID capability; production blocked |
| `PreviewReopen` (không ReopenDryRun) | Throw |
| `ConfirmReopen` | **2C-06F Implemented (source)** — impact B3/B5 block; snapshot bất biến; gate OFF |
| `PreviewRecalculate` / `PreviewCompare` | DryRun only — **không** có `ConfirmRecalculate` trên interface (cố ý: persist qua Confirm từng bước) |

`IProductionCostWriteGate` / `ProductionCost:EnableWrites` mặc định **false** (`Program.cs`).

### 1.3 Missing repository operations

`IProductionCostRepository` / `ProductionCostRepository`: **chỉ đọc**. Thiếu (tối thiểu cho 2C-06+):

| Nhóm | Operation cần thêm |
|------|-------------------|
| Session / TX | `CreateWriteSessionAsync` (hoặc inject `DapperExecutor.CreateSessionAsync`) — C# **owner** TX |
| Run lock | `LockRunForUpdateAsync(companyCd, runId)` |
| Steps | `GetStepStatusesFromDbAsync`, `UpsertStepStatusAsync` |
| Alloc B2 | `CallAllocLoadPool`, `CallAllocLoadBasis`, `SaveAllocDetailDraft` (C# engine), `CallAllocConfirm` |
| Transfer B3 | Ủy quyền PeriodLock COGS `EXP_TO_154_ONLY` trong cùng TX / session pattern hiện có |
| WIP B4 | `CallSourceRebuild` / persist source+wip+receipt draft; `CallCostConfirm` (**không** apply kho ở B4 theo skeleton 2C-03) |
| Valuation B5 | Load movements; apply giá theo `INPUT_ID`; UPSERT `VALUATION_COGS`; mark `VALUATION=DONE` |
| Reopen | Persist version bump / SUPERSEDED / step cascade; **không** xóa version cũ |
| History | `GetPersistedOperationIdsAsync`, `InsertHistoryIfNewAsync` (UNIQUE theo OperationId/STABLE_KEY) |
| Voucher ref | Read/UPSERT `production_cost_voucher_ref` theo `(COMPANY_CD, STABLE_KEY)` |

### 1.4 Inventory / PeriodLock integrations to reuse

| Thành phần sẵn có | Dùng cho G-K | Lưu ý |
|-------------------|--------------|-------|
| `InventoryOutPriceEngine` + `InventoryValuationService` | B5 tính giá xuất (adapter đã parity 4 PP) | **Không** copy công thức; Confirm chỉ gọi lại engine |
| `InventoryPriceUpdateRepository.ApplyAsync` → `apply_inventory_update_in_price` / `_out_price` | Tham khảo TX session + CALL SP | SP hiện theo **PRODUCT_CD + STORE + khoảng ngày** — **không** đủ cho plan G-K theo **INPUT_ID**. Cần wrapper/SP mới hoặc UPDATE trực tiếp theo `INPUT_ID`+`COMPANY_CD` trong TX G-K |
| `InventoryVoucherRepository` / `setInventoryCogs` | Post/UPDATE chứng từ 632 | UPSERT theo `STABLE_KEY=VALUATION_COGS:{COMPANY}:{PERIOD}` — không INSERT trùng |
| PeriodLock COGS (`PeriodLockRepository` + `sp_period_lock_cogs_*`) | B3 `EXP_TO_154_ONLY` | Đã gọi `sp_pc_assert_allocation_ready_for_exp`; TX owner PeriodLock — đồng bộ với Confirm B3 G-K |
| `DapperExecutor.CreateSessionAsync` | Mọi Confirm G-K | Pattern chuẩn: Begin → CALL/SQL → Commit/Rollback; SP **không** `START/COMMIT` |

### 1.5 Transaction boundaries & consistency (bắt buộc giữ)

1. **TX owner = C#** cho mọi Confirm B2–B5 / REOPEN. SP bàn giao (ví dụ `sp_pc_alloc_confirm`) đã ghi chú không mở TX nội bộ — giữ nguyên.
2. **Khóa RUN** `FOR UPDATE` theo `COMPANY_CD + RUN_ID`; đối chiếu `RUN_VERSION` header — lệch → abort.
3. **Không tin DryRun client** cho trạng thái bước / quyền / OperationId đã persist — Confirm đọc lại DB.
4. **Idempotent Confirm**: đã DONE + cùng khóa/version → `NOOP` / không history trùng (`ConfirmIdempotency`, `HistoryPlan`).
5. **Snapshot CONFIRMED**: REOPEN lần đầu `version+1` + SUPERSEDED; không DELETE/overwrite UNIQUE version cũ.
6. **B2 ≠ toàn kỳ**: Confirm B2 → `ALLOC_CONFIRMED`; B4/B5 → step DONE; `RUN.STATUS=CONFIRMED` chỉ khi đủ điều kiện sau B5/B6 (ngoài phạm vi enable sớm).
7. **B5 atomic**: cập nhật giá nhập (`INPUT_ID`) + tính lại xuất + UPSERT 632 trong **một** TX; fail → rollback / `RollbackRestoreLines`.
8. **B3 exclusive**: `CostEngineExclusive` + PeriodLock — không song song BI COGS trên cùng kỳ GK.

### 1.6 Test coverage gaps

Đã có (unit, mock/dry — báo cáo 2C-05: ~85 pass kèm `InventoryOutPrice`):

- Engine B2/B3/B4/B5 + lifecycle rules + WriteGate chặn Confirm  
- Read service (schema/period/steps/BI mismatch)

**Thiếu (integration / sắp tới):**

| Gap | Mức |
|-----|-----|
| Confirm B2–B5 với session giả / in-memory TX + CALL SP mock | Integration |
| Idempotent retry OperationId **đọc từ bảng history thật** | Integration |
| REOPEN → Recalc cascade → Confirm lại không đụng version CONFIRMED cũ | Integration |
| B5 apply `INPUT_ID` + rollback nửa chừng | Integration |
| B3 PeriodLock + G-K assert trong cùng luồng API | Integration |
| Preview-from-DB (load pool/basis/wip từ repo) | Service test |
| Schema history thiếu `OPERATION_ID` | Migration + repo test |
| Không có test repository write | Unit/integration |

---

## 2. Integration design — remaining B2–B5 Confirm + history

### 2.1 Principles (không đổi rule)

- Giữ nguyên toàn bộ engine C# đã nghiệm thu 2C-02…05.
- Tính toán nặng (alloc/wip/valuation) **ưu tiên C#**; SP lo load nguồn, khóa trạng thái, persist khóa, assert — trừ khi body SQL đã đủ và đã nghiệm thu parity.
- `sp_pc_alloc_calculate` / `sp_pc_wip_calculate` / `sp_pc_valuation_*` hiện skeleton → Confirm DEV gọi **C# engine** rồi ghi bảng; chỉ gọi SP khi body hoàn chỉnh + parity test.
- WriteGate vẫn OFF cho đến khi DEV SOURCE + integration test pass.

### 2.2 End-to-end Confirm flow (mỗi bước)

```
Auth COMPANY_CD + DB
  → Validate period/run/version
  → WriteGate.EnsureWritable
  → CreateSession (BEGIN)
  → SELECT RUN … FOR UPDATE (COMPANY_CD, RUN_ID)
  → Assert RUN_VERSION + STATUS writable
  → Load authoritative steps / history OperationIds from DB
  → Compute (C# engine) hoặc CALL SP đã nghiệm thu
  → Persist draft/confirm + voucher_ref STABLE_KEY
  → Insert history nếu WritesHistory
  → COMMIT | ROLLBACK
```

### 2.3 Per-step Confirm plan

#### B2 — `ConfirmAllocationAsync`

1. Session TX; lock run.
2. Guard: `GkRunStatus.AllowsPoolRecalc` / không load_pool khi `ALLOC_CONFIRMED|CONFIRMED`.
3. Optional: `sp_pc_alloc_load_pool` + `load_basis_qty` (script 2C-02) **hoặc** SELECT nguồn tương đương rồi `AllocationEngine.Run`.
4. UPSERT `alloc_detail` theo UNIQUE `(RUN_ID, RUN_VERSION, PRODUCT_ID, DEPARTMENT_ID, ACCOUNT_CD, POOL_DEPARTMENT_ID)`.
5. `CALL sp_pc_alloc_confirm` → step DONE + `ALLOC_CONFIRMED` + voucher_ref `ALLOC:…` + history (NOOP nếu đã confirm).
6. Không tạo CT N154 ở B2.

#### B3 — `ConfirmTransferAsync`

1. `ExpTo154Rules` + `CostEngineExclusive`.
2. Ủy quyền PeriodLock COGS rule `EXP_TO_154_ONLY` (đã assert `sp_pc_assert_allocation_ready_for_exp`).
3. STABLE_KEY `EXP_TO_154_ONLY:{COMPANY}:{PERIOD_YM}` — UPSERT voucher_ref; không downstream 155/632.
4. Đồng bộ `production_cost_step` EXP = DONE từ kết quả PeriodLock / mirror.
5. TX: thống nhất owner (PeriodLock session vs G-K session) — **một** atomic boundary hoặc saga có rollback rõ; không double-commit lệch trạng thái.

#### B4 — `ConfirmCostCalcAsync`

1. Rebuild source (C# `CostSourceRules` / SP `sp_pc_source_rebuild` khi có body) — loại XFER.
2. `WipCostEngine` → persist `production_cost_wip` + `receipt` (PRODUCT+DEPT+INPUT_ID).
3. `ReceiptReconcile` mode Confirm.
4. `sp_pc_cost_confirm` / C#: step `COST_CALC=DONE` only; **không** apply giá kho ở B4 (đúng skeleton 2C-03; apply ở B5).
5. Không promote `CONFIRMED` toàn kỳ.

#### B5 — `ConfirmValuationAsync`

1. Đọc lại B2/B3/B4 DONE + RUN từ DB (`ValuationPrerequisite`) — bỏ qua DryRun status.
2. `ReceiptPricePrep` từ WIP/receipt DB; reconcile ReadyToApply.
3. Trong TX: UPDATE `chit_inventory_input` theo **INPUT_ID + COMPANY_CD** (không dùng bulk PRODUCT_CD menu Kho trừ khi map 1-1 đã chứng minh).
4. Load movements → `ValuationAverageCost` / `InventoryOutPriceEngine` → apply out price + UPSERT VALUATION_COGS (`STABLE_KEY`).
5. Step `VALUATION=DONE`; `WouldApplyInventory=true` chỉ khi gate ON + DEV approve.
6. Rollback map = `ValuationApplyPlan.RollbackRestoreLines`.

#### REOPEN — `ConfirmReopenAsync`

1. Quyền + OperationId từ DB.
2. `ReopenRules.Build` với dữ liệu DB (không tin client history hint).
3. Persist: version mode CreateNew / Keep / IdempotentRetry; SUPERSEDED chỉ khi tạo version mới; cascade reverse steps.
4. History insert nếu `WritesHistory`.

#### RECALCULATE

- Preview lập `RecalcPlan` (engines B2–B5).
- Persist thực tế: gọi lại Preview-from-DB + Confirm từng bước affected trên **working version** — không cần endpoint ConfirmRecalculate riêng trừ khi FE yêu cầu atomic multi-step (khi đó một TX bọc chuỗi Confirm).

#### HISTORY / COMPARE

- Compare: tiếp tục dry + GET `/comparison` (repo đã JOIN PRODUCT+DEPT+COMPANY).
- History: bổ sung schema + repo trước khi Confirm lifecycle tin OperationId.

### 2.4 Schema gap — history (blocking cho idempotency thật)

DDL hiện tại (`01_production_cost_tables.sql`):

- Có: `ACTION_CODE`, `RUN_VERSION`, `REASON`, …  
- **Không có:** `OPERATION_ID`, `STABLE_KEY`, UNIQUE idempotency.

C# `HistoryPlan` yêu cầu OperationId ≥ 8 ký tự và `PC_HIST:…:op:{OperationId}`.

**Plan migration (DEV sau duyệt — không chạy trong 2C-06):**

```text
ALTER production_cost_history
  ADD OPERATION_ID varchar(64) NULL,
  ADD STABLE_KEY varchar(160) NULL,
  ADD STEP_CODE varchar(40) NULL,
  -- UNIQUE (COMPANY_CD, STABLE_KEY) WHERE STABLE_KEY IS NOT NULL
  -- hoặc UNIQUE (COMPANY_CD, OPERATION_ID)
```

Cập nhật `ProductionCostHistoryDto` + `GetHistoryAsync` SELECT; probe schema có thể thêm cột bắt buộc khi READY cho write.

`sp_pc_alloc_confirm` hiện INSERT history **không** OperationId — khi tích hợp phải align C# history writer **hoặc** mở rộng SP cùng contract.

---

## 3. File-by-file integration plan

### 3.1 API / contracts

| File | Việc cần làm |
|------|----------------|
| `Interfaces/IProductionCostWriteService.cs` | Giữ chữ ký; optional thêm `ConfirmRecalculate` **chỉ nếu** FE cần atomic multi-step |
| `Interfaces/IProductionCostRepository.cs` | Tách hoặc mở rộng `IProductionCostWriteRepository` với session + write ops |
| `Models/ProductionCostWriteModels.cs` | Thêm `OperationId` / idempotency fields trên Confirm request (không DryRun) |
| `Models/LifecycleModels.cs` | Giữ plan DTOs; map DB → `PersistedOperationIdsFromDb` |
| `Models/ProductionCostModels.cs` | History DTO + OperationId/StableKey |
| `Controllers/ProductionCostController.cs` | Không đổi route; Confirm trả `Persisted` / `IdempotencyAction` thật khi gate ON |

### 3.2 Services

| File | Việc cần làm |
|------|----------------|
| `ProductionCostWriteService.cs` | Inject repo + executor; thay throw bằng orchestration TX; Preview-from-DB load qua repo |
| `ProductionCostWriteGate.cs` | Giữ mặc định OFF; document DEV-only |
| `Allocation/*`, `Wip/*`, `Valuation/*`, `Lifecycle/*`, `Transfer/*` | **Không đổi công thức** — chỉ gọi từ Confirm |
| `ConfirmIdempotency.cs` / `HistoryPlan.cs` | Wire với repo history |

### 3.3 Repository / data

| File | Việc cần làm |
|------|----------------|
| `Repositories/ProductionCostRepository.cs` | Giữ read; hoặc partial class write |
| **Mới** `ProductionCostWriteRepository.cs` | CALL SP + UPSERT + history trong `DapperSession` |
| `Data/DapperExecutor.cs` / `DapperSession.cs` | Tái sử dụng — không fork TX |
| `Repositories/InventoryPriceUpdateRepository.cs` | Tham chiếu; **không** dùng nguyên `PRODUCT_CD` cho B5 trừ khi có adapter INPUT_ID |
| `Repositories/PeriodLockRepository.cs` | Điểm nối B3 — document exclusive session |
| `Repositories/InventoryVoucherRepository.Cogs.cs` | Điểm nối UPSERT 632 |

### 3.4 SQL bàn giao (user SOURCE sau duyệt — không chạy trong 2C-06)

| File | Vai trò tích hợp |
|------|------------------|
| `migrations/20261102_2c02_sp_alloc.sql` | load_pool / basis / **alloc_confirm** / assert EXP — ưu tiên dùng confirm |
| `migrations/20261102_2c02_ddl_alloc_detail_key.sql` | UNIQUE alloc_detail |
| `migrations/20261102_2c03_sp_wip.sql` | Thay skeleton bằng body **hoặc** bỏ qua — persist từ C# |
| `migrations/20261102_2c03_ddl_wip_receipt_key.sql` | WIP/receipt DEPT |
| `migrations/20261102_2c04_sp_valuation.sql` | Skeleton → body hoặc C#-only persist |
| `migrations/20261102_2c05_README.md` | SP reopen/history — **Pending DEV** |
| **Mới (kế hoạch)** `…_2c06_ddl_history_operation.sql` | OPERATION_ID / STABLE_KEY |
| **Mới (kế hoạch)** `…_2c06_sp_reopen_history.sql` | Persist reopen + history idempotent |
| `StoredProcedures/PeriodLock/CogsSummary/01_sp_period_lock_cogs.sql` | B3 — giữ đồng bộ 2b_04 |
| `02_sp_specifications.sql` | Chuẩn hành vi — đối chiếu khi viết body |

### 3.5 Tests

| File | Việc cần làm |
|------|----------------|
| `ProductionCost*Tests.cs` hiện có | Giữ regression engines |
| **Mới** `ProductionCostConfirmIntegrationTests.cs` | Session fake / Testcontainers (sau khi cho phép DB DEV) |
| **Mới** `ProductionCostHistoryIdempotencyTests.cs` | OperationId DB round-trip |
| InventoryOutPrice tests | Giữ parity khi Confirm gọi engine |

### 3.6 DI

| File | Việc cần làm |
|------|----------------|
| `Program.cs` | Đăng ký write repository; **không** đổi default `EnableWrites=false` |

---

## 4. Required API and repository changes (checklist)

### API

- [ ] Confirm B2/B3/B4/B5/Reopen: bỏ throw; orchestration + `Persisted=true` khi thành công  
- [ ] Preview-from-DB (không DryRun) khi gate ON: load pool/basis/wip/receipts từ repo rồi engine  
- [ ] Request Confirm: `OperationId` bắt buộc (lifecycle + ideally mọi Confirm)  
- [ ] Response: `IdempotencyAction`, `RunStatus`, step statuses **từ DB** sau commit  
- [ ] Giữ `StepStatusesAreClientSimulated=true` chỉ trên Preview DryRun  

### Repository

- [ ] Write session scoped to company DB  
- [ ] Lock run + read steps/history OperationIds  
- [ ] Persist alloc/wip/receipt/source/voucher_ref  
- [ ] CALL `sp_pc_alloc_confirm` (+ assert EXP path B3)  
- [ ] INPUT_ID price apply + COGS UPSERT (B5)  
- [ ] Reopen version/SUPERSEDED/history  
- [ ] Schema probe: phát hiện thiếu cột history trước khi bật write  

### Config / safety

- [ ] `ProductionCost:EnableWrites` chỉ DEV/TEST sau SOURCE + verify_readonly  
- [ ] Không enable production trong 2C-06  

---

## 5. Integration test scenarios (đề xuất — chưa chạy DB trong 2C-06)

### 5.1 B2 Allocation Confirm

1. DRAFT + pool/detail → Confirm → `ALLOC_CONFIRMED`, step2 DONE, history CONFIRM.  
2. Confirm lần 2 cùng version → NOOP, không history mới.  
3. Fail giữa UPSERT detail và confirm → ROLLBACK, status cũ.  
4. `ALLOC_CONFIRMED` → load_pool bị chặn (SP/guard).  

### 5.2 B3 Transfer Confirm

1. Sau B2 → EXP_TO_154_ONLY → CT N154/C62x, STABLE_KEY đúng, không 155/632.  
2. ENGINE=BI đã claim → exclusive fail.  
3. B2 chưa ALLOC_CONFIRMED → assert fail.  

### 5.3 B4 Cost Calc Confirm

1. Source không gồm XFER; WIP PRODUCT+DEPT; receipt INPUT_ID.  
2. Reconcile lệch SL/tiền → abort, không DONE.  
3. Confirm → COST_CALC=DONE, RUN vẫn ALLOC_CONFIRMED/REOPENED — không CONFIRMED toàn kỳ.  

### 5.4 B5 Valuation Confirm

1. B2–B4 DONE → apply INPUT_ID; out price parity engine; UPSERT 632.  
2. Đã có VALUATION_COGS → UPDATE không INSERT đôi.  
3. Fail sau apply nhập trước post 632 → rollback giá nhập.  
4. DryRun status fake DONE nhưng DB thiếu B4 → Confirm reject.  

### 5.5 Lifecycle / History

1. REOPEN từ CONFIRMED → version+1, SUPERSEDED, snapshot cũ đọc được.  
2. REOPEN khi đã REOPENED → giữ version.  
3. Retry cùng OperationId → Noop.  
4. Recalc B4 khi B5 còn DONE → blocked; cascade reopen trước.  
5. ALLOC_CONFIRMED → Recalc B2 blocked; B4 allowed nếu tiền đề OK.  
6. Compare CONFIRMED vs PREVIEW — presence PRODUCT+DEPT.  

### 5.6 Gate / auth

1. EnableWrites=false → Confirm business error gate.  
2. Thiếu COMPANY_CD / DB → không fallback.  

---

## 6. Outstanding issues

| ID | Mức | Nội dung |
|----|-----|----------|
| O-01 | **Process** | DDL history 2C-06A đã có script — **chưa SOURCE DEV**; GET `/history` tương thích schema cũ (probe); ghi history cần SOURCE |
| O-02 | **Blocker Confirm B5/REOPEN production** | B5 + REOPEN **source done** — Preview-from-DB + SQL INPUT_ID + reverse B3/B5 + post 632 thật pending |
| O-03 | High | `apply_inventory_update_*` theo PRODUCT_CD ≠ plan B5 theo INPUT_ID — cần SP/wrapper riêng |
| O-04 | High | SP calculate/wip/valuation/reopen còn skeleton hoặc pending — quyết định C#-persist vs hoàn thiện SQL trước EnableWrites |
| O-05 | High | B3 TX boundary: PeriodLock session vs G-K session chưa thiết kế atomic thống nhất |
| O-06 | Medium | ~~SP history lệch OpId~~ → **đã bỏ INSERT history trong SP (2C-06B)**; C# sở hữu history |
| O-07 | Medium | Preview-from-DB chưa implement — FE/API phụ thuộc DryRun payload |
| O-08 | Medium | Chưa có integration test gắn DB/SP |
| O-09 | Low | Không có `ConfirmRecalculate` API — chấp nhận nếu Confirm từng bước; document cho FE |
| O-10 | Process | Migration 2B/2C SQL **chưa** coi là đã SOURCE trên DEV — WriteGate phải giữ OFF |
| O-11 | Out of scope 2C-06 | `RUN.STATUS=CONFIRMED` toàn kỳ / B6 PL — sau B5 ổn định |

---

## 7. Đề xuất thứ tự triển khai (slice)

| Slice | Nội dung | Trạng thái |
|-------|----------|------------|
| **2C-06A** | History DDL + WriteRepository + TX infra | **Implemented (source)** — đã nghiệm thu source |
| **2C-06B** | Allocation Confirm B2 + safety fix | **Implemented (source)** — UPSERT/config OK; Preview-from-DB pending |
| **2C-06C** | EXP_TO_154_ONLY Confirm B3 | **Implemented (source)** — đã nghiệm thu source |
| **2C-06D** | CostCalc Confirm B4 | **Implemented (source)** |
| **2C-06E** | Valuation Confirm B5 | **Implemented (source-tested)** |
| **2C-06F** | Reopen / History / Integration readiness | **Implemented (source-tested)** — dừng source toàn 2C-06 |

Sau 2C-06F + DEV SOURCE + verify + reverse B3/B5 → mới xét `EnableWrites=true` (DEV/TEST only).

---

## 8. 2C-06A Implementation (+ Final Safety Review)

Ngày: 2026-11-03  
Phạm vi: History DDL, Write Repository, TX ownership C#, final safety (idempotency / key length / GET History tương thích).  
**Không** Confirm thật · **Không** EnableWrites · **Không** SOURCE · **Không** kết nối DB · **Không** đổi KT/kho.

### 8.1 Deliverables

| Hạng mục | File / ghi chú |
|----------|----------------|
| DDL (MySQL 5.6/8.0, không `innodb_large_prefix`) | `STABLE_KEY` **VARCHAR(190)**; UNIQUE full `(COMPANY_CD, STABLE_KEY)` = 630 utf8 bytes ≤ 767; `CONTENT_FINGERPRINT` VARCHAR(64); UNIQUE OpId |
| Verify / README | `20261103_2c06a_verify_readonly.sql`, `20261103_2c06a_README.md` |
| Schema cài mới | `01` / `2b_01` đồng bộ VARCHAR(190) + CONTENT_FINGERPRINT |
| Idempotency C# | `HistoryPlan` — Noop / Conflict / ResolveAfterDuplicateKey + fingerprint require |
| Key length | OpId 8–64; StableKey ≤**190** (hash `PC_HIST_H:` nếu dài); không prefix unique |
| Write repo | Đọc `CONTENT_FINGERPRINT` thật; INSERT cột riêng; race 1062 → đọc lại |
| GET History | Probe 4 cột; schema cũ = SQL legacy |
| Unit test | Index 767 budget · fingerprint Conflict/Noop · race sau commit |

### 8.2 Idempotency (final + fingerprint)

| Tình huống | Kết quả |
|------------|---------|
| Cùng OpId + StableKey + **cùng** `CONTENT_FINGERPRINT` | **Noop** |
| Cùng OpId + StableKey nhưng fingerprint khác / Confirm thiếu fingerprint DB | **Conflict** (thiếu ≠ khớp) |
| Cùng OpId khác RUN/STEP/ACTION | **Conflict** |
| Race UNIQUE 1062 | Đọc lại hàng đã commit (gồm fingerprint) → Noop/Conflict |
| Client hint | **Không** tin |

### 8.3 Backward compatibility GET History

- Schema **trước** 2C-06A: SQL legacy → API không lỗi.  
- Schema **sau**: SELECT thêm OPERATION_ID / STABLE_KEY / STEP_CODE / CONTENT_FINGERPRINT.  
- Write từ chối nếu probe thiếu cột/index (gồm CONTENT_FINGERPRINT).

### 8.4 TX ownership

1. `BeginWriteSessionAsync` → `DapperExecutor.CreateSessionAsync` (BEGIN).  
2. SP **không** START/COMMIT.  
3. `ProductionCostWriteSession` → `CompleteAsync` Commit/Rollback.  
4. History insert: PlanWrite từ DB **trước** INSERT; race dựa UNIQUE, không chỉ SELECT-before-INSERT.

### 8.5 Không làm trong 2C-06A

- Confirm B2–B5 / Reopen persist · EnableWrites · SOURCE SQL · apply kho / 632 · sửa B-I  

### 8.6 Kiểm thử

#### Host build

```text
dotnet build API_AMNOTE_WEB.csproj -o artifacts/host-2c06a-check
→ 0 Error(s)
HOST_EXIT=0
```

#### Unit test

```text
dotnet test --filter "FullyQualifiedName~ProductionCost|FullyQualifiedName~InventoryOutPrice"
→ Passed: 99, Failed: 0
TEST_EXIT=0
```

Cases: utf8 unique 630≤767 vs 825>767 · StableKey 190/hash · fingerprint Noop/Conflict/thiếu≠khớp · race 1062 đọc FP · History SQL legacy vs mới · WriteGate OFF.

### 8.7 Dừng 2C-06A

Fix 2 blockers (UNIQUE 5.6 + CONTENT_FINGERPRINT) xong — **đã nghiệm thu source trước 2C-06B**.

---

## 9. 2C-06B — Allocation Confirm B2 (622/627) + FINAL CONFIRM SAFETY FIX

Ngày: 2026-11-03 (safety fix cùng ngày)  
Phạm vi: Confirm Bước 2 only — `ConfirmAllocationAsync` trên hạ tầng 2C-06A.  
**Không** B3/B4/B5 · **Không** EnableWrites · **Không** SOURCE · **Không** Confirm thật trên DB · **Không** N154 · **Không** `CONFIRMED` toàn kỳ · **Không** đổi AllocationEngine · **Không** sửa B-I/kho.

### 9.1 Implemented (source)

| Hạng mục | Chi tiết |
|----------|----------|
| Orchestration | Gate → TX C# → lock RUN FOR UPDATE → **assert RUN_VERSION trước mọi ghi** → assert PeriodYm ↔ locked RUN → config từ RUN DB → `AllocationEngine` → planner → **UPSERT** pool/detail → `CALL sp_pc_alloc_confirm` → history C# → Commit/Rollback |
| Config authority | `AllocationRunConfig.FromLockedRun` — AllocateType / ByDepartment / NumberDigit / Include6212 từ DB; DryRun lệch → stale client reject |
| Persist | `ReplaceAllocPoolAsync` / `ReplaceAllocDetailAsync` = **UPSERT** `ON DUPLICATE KEY UPDATE` (UNIQUE không gồm ISDEL) + soft-delete khóa thiếu; chặn `ALLOC_CONFIRMED`/`CONFIRMED`; không đụng RUN_VERSION khác |
| Planner | Fingerprint **canonical / InvariantCulture** (period + config + pool + lines); Noop/Conflict; `WouldPostAccountingVoucher=false`; ExpectedStatus=`ALLOC_CONFIRMED` |
| SP contract | `sp_pc_alloc_confirm` **không** INSERT history; **không** START/COMMIT; voucher_ref `ALLOC:…`; STEP2 DONE; RUN→`ALLOC_CONFIRMED` only |
| Safety | Race UNIQUE history 1062 → `ResolveAfterDuplicateKey`; preflight `STABLE_KEY` >190 SELECT-only |
| Tests | Confirm lần đầu (+ existing ISDEL='1') / legacy INSERT fails UNIQUE / retry OpId / FP conflict+rollback / period mismatch / config mismatch / ALLOC_CONFIRMED chặn / RUN_VERSION sai / mid-fail Rollback / fingerprint InvariantCulture / UPSERT soft-delete missing / gate OFF |
| Gate | `ProductionCost:EnableWrites` mặc định **OFF** |

### 9.2 Skeleton / NOT production-ready

| Hạng mục | Ghi chú |
|----------|---------|
| Preview-from-DB (load pool/basis) | **Pending** — Confirm không DryRun → throw |
| Confirm phụ thuộc DryRun pool/products | **Interim only** — config đã từ DB; nguồn tài chính vẫn DryRun → **không** coi Confirm production-ready; WriteGate OFF |
| Confirm B3 / B4 / B5 / Reopen persist | Chưa (2C-06C+) |

### 9.3 Pending DEV/TEST (user tự làm — agent không chạy)

| Script / việc | Ghi chú |
|---------------|---------|
| `20261103_2c06a_ddl_history_operation.sql` | SOURCE nếu chưa |
| `20261103_2c06b_preflight_stable_key_len.sql` | SELECT — xử lý tay nếu `ROWS_OVER_190` > 0; **không** auto-fix |
| `20261103_2c06b_sp_alloc_confirm_no_history.sql` hoặc SOURCE lại `20261102_2c02_sp_alloc.sql` | Patch SP bỏ history |
| Integration test gắn MySQL (UPSERT + ISDEL='1') | Sau SOURCE; vẫn **EnableWrites=OFF** |
| Preview-from-DB / load pool+basis | Cần trước khi coi Confirm production-ready |
| EnableWrites | **Giữ OFF** |

### 9.4 Files changed (2C-06B + safety fix)

- `Services/ProductionCost/ProductionCostWriteService.cs` — Confirm: DB config + PeriodYm + UPSERT args
- `Services/ProductionCost/Allocation/AllocationConfirmPlanner.cs` — fingerprint InvariantCulture đầy đủ
- `Services/ProductionCost/Allocation/AllocationRunConfig.cs` — mới
- `Services/ProductionCost/Allocation/AllocUpsertRules.cs` — mới
- `Repositories/ProductionCostWriteRepository.cs` — UPSERT + soft-delete missing + guard
- `Interfaces/IProductionCostWriteRepository.cs` — chữ ký Replace (+ runStatus / lockedRunVersion)
- `Database/.../20261102_2c02_sp_alloc.sql` — bỏ history INSERT (giữ)
- `Database/.../20261103_2c06b_*.sql` + README
- `API_AMNOTE_WEB.Tests/ProductionCostAllocationConfirmTests.cs` + stubs
- `ACCEPTANCE_2C_06.md` — mục này

### 9.5 Build / test

#### Host build

```text
dotnet build API_AMNOTE_WEB.csproj -o artifacts/host-2c06b-safety-check
→ 0 Error(s)
HOST_EXIT=0
```

#### Unit test

```text
dotnet test --filter "FullyQualifiedName~ProductionCost|FullyQualifiedName~InventoryOutPrice"
→ Passed: 121, Failed: 0
TEST_EXIT=0
```

### 9.6 Quy tắc B2 đã giữ

1. Không bút toán N154 trong B2.  
2. Không `RUN.STATUS=CONFIRMED` toàn kỳ — chỉ `ALLOC_CONFIRMED` + STEP2 DONE.  
3. Không đổi công thức AllocationEngine.  
4. Không overwrite khi `ALLOC_CONFIRMED` (trừ Noop cùng OpId+FP).  
5. History chỉ từ C# — không double SP+C#.  
6. SP không tự START/COMMIT.  
7. Fail bất kỳ bước (persist / SP / history) → Rollback.  
8. Không tin DryRun cho config/status/PeriodYm; fingerprint InvariantCulture.  
9. Persist = UPSERT theo khóa nghiệp vụ (kể cả hàng ISDEL='1').

### 9.7 Verdict safety fix / dừng

| Phần | Trạng thái |
|------|------------|
| UPSERT pool/detail + UNIQUE/ISDEL | **Implemented** |
| Config/PeriodYm/RUN_VERSION từ DB | **Implemented** |
| Fingerprint canonical | **Implemented** |
| SP no TX / history C# / ALLOC_CONFIRMED only | **Implemented** (script bàn giao) |
| Unit tests safety | **Implemented** |
| Preview-from-DB / Confirm production-ready | **Pending DEV** |
| SOURCE + integration MySQL | **Pending DEV/TEST** |
| EnableWrites | **OFF** |

**Dừng nghiệm thu 2C-06B xong — tiếp 2C-06C bên dưới.** Không Confirm thật / không SOURCE / không bật WriteGate.

---

## 10. 2C-06C — EXP_TO_154_ONLY Confirm B3

Ngày: 2026-11-03  
Phạm vi: Confirm Bước 3 only — `ConfirmTransferAsync` + B2 authoritative-source guard + safety fix (CURRENT_RUN_ID / B-I COGS / B3 DONE idempotency).  
**Không** B4/B5 · **Không** EnableWrites · **Không** SOURCE · **Không** Confirm thật · **Không** post CT thật · **Không** sửa B-I/kho · **Không** đổi AllocationEngine.

### 10.1 Implemented (source-tested)

| Hạng mục | Chi tiết |
|----------|----------|
| B2 guard | `AllocationConfirmSourceGuard` — chặn Confirm B2 ghi thật khi pool/basis chưa từ DB; **độc lập** EnableWrites |
| Orchestration B3 | Gate → TX C# → lock RUN + Period → **assert CURRENT_RUN_ID + PERIOD_COST_ID** → VERSION/PeriodYm → đọc `acc_period_lock_step.COGS_SUMMARY` → B2 DONE + alloc → planner (Noop/Conflict/AlreadyConfirmed) → `CostEngineExclusive` → PeriodLock cùng session → STEP B3 DONE → voucher_ref → history → Commit/Rollback |
| CURRENT_RUN_ID | `AssertPeriodCurrentRunMatches` — reject trước mọi SP ghi nếu request ≠ `PERIOD.CURRENT_RUN_ID` hoặc `RUN.PERIOD_COST_ID` ≠ period |
| B-I COGS status | `GetBiCogsStepStatusForUpdateAsync` (FOR UPDATE) → `CostEngineExclusive`; COGS_SUMMARY=DONE chặn đường ghi (khớp `sp_period_lock_cogs_validate` / `require_open`) |
| B3 DONE + OpId mới | Không PeriodLock / không tạo-xóa CT; nhất quán → `NoopAlreadyConfirmed`; thiếu/mâu thuẫn history/voucher/engine → reject (cần REOPEN) |
| PeriodLock reuse | `CallPeriodLockExpTo154Async` = validate + calculate + create_voucher trên `DapperSession` G-K — **không** dual-commit |
| Atomicity | `SupportsSharedGkSession=true`; nested COGS SP **không** START/COMMIT; `CREATE TEMPORARY` không implicit commit (source review) |
| Rules | Chỉ N154/C621–627; cấm 154→155/632; không `CONFIRMED` toàn kỳ |
| Idempotency | Cùng OpId+FP → Noop; OpId cũ khác FP → Conflict; STABLE_KEY `EXP_TO_154_ONLY:{COMPANY}:{PERIOD}` |
| Tests | CURRENT_RUN_ID / PERIOD_COST_ID / BI COGS DONE / B3 DONE+OpId mới / retry OpId / FP conflict / fail transfer↔history Rollback / không downstream / gate OFF |
| Gate | `ProductionCost:EnableWrites` mặc định **OFF** |

### 10.2 Skeleton

| Hạng mục | Ghi chú |
|----------|---------|
| Preview transfer từ DB | Throw khi gate ON |
| Confirm B5 | **2C-06E** — xem §12 |
| Reopen persist | Chưa (2C-06F) |

### 10.3 Pending MySQL integration (DEV/TEST — agent không chạy)

| Việc | Ghi chú | Phân loại |
|------|---------|-----------|
| SOURCE PeriodLock COGS + `sp_pc_assert_allocation_ready_for_exp` | Nếu chưa trên DEV | **Pending integration** |
| Integration MySQL Confirm B3 | Sau SOURCE; **EnableWrites=OFF** đến nghiệm thu | **Pending integration** |
| Runtime TX: nested PeriodLock không COMMIT / không implicit commit | Source đã xác nhận; chờ chứng minh trên MySQL DEV | **Pending integration** |
| Preview-from-DB B2 | Cần trước khi bỏ `AllocationConfirmSourceGuard` | **Pending** |
| EnableWrites | **Giữ OFF** | **OFF** |

### 10.4 Files changed (2C-06C + safety fix)

- `AllocationConfirmSourceGuard.cs` — mới
- `TransferConfirmPlanner.cs` / `TransferConfirmAtomicity.cs` / `CostEngineExclusive.cs`
- `ProductionCostWriteContext.cs` — `AssertPeriodCurrentRunMatches`
- `ProductionCostWriteService.cs` — ConfirmTransferAsync + B2 guard
- `IProductionCostWriteRepository.cs` / `ProductionCostWriteRepository.cs` — BI COGS status, voucher/history probe, PeriodLock cùng session
- `ProductionCostModels.cs` — `ProductionCostPeriodEngineSnapshot`
- `ProductionCostTransferConfirmTests.cs` + stubs
- `ACCEPTANCE_2C_06.md` — mục này

### 10.5 Build / test

#### Host build (source-tested)

```text
dotnet build API_AMNOTE_WEB.csproj -o artifacts/host-2c06c-check
→ 0 Error(s)
HOST_EXIT=0
```

#### Unit test (source-tested)

```text
dotnet test --filter "FullyQualifiedName~ProductionCost|FullyQualifiedName~InventoryOutPrice"
→ Passed: 140, Failed: 0
TEST_EXIT=0
```

### 10.6 Quy tắc B3 đã giữ

1. B2 phải ALLOC_CONFIRMED / COST_ALLOCATION=DONE trước B3.  
2. request RUN_ID = PERIOD.CURRENT_RUN_ID; PERIOD_COST_ID khớp — không STEP B3 DONE cho RUN không current.  
3. PeriodLock EXP_TO_154_ONLY trên cùng TX G-K — không dual-commit.  
4. Không N155/C154, N632/C154, 154→155/632.  
5. CostEngineExclusive + COGS_SUMMARY authoritative — khớp validate.  
6. History/OperationId idempotent; B3 DONE + OpId mới không PeriodLock lại.  
7. STEP B3 DONE; không CONFIRMED toàn kỳ.  
8. Fail giữa kết chuyển và history → Rollback.

### 10.7 Verdict / dừng

| Phần | Trạng thái |
|------|------------|
| B2 authoritative source guard | **Implemented (source-tested)** |
| ConfirmTransferAsync B3 + safety fix | **Implemented (source-tested)** |
| Shared-session PeriodLock (no COMMIT in COGS SP) | **Implemented (source-reviewed)** |
| Unit tests B3 safety | **Implemented (source-tested)** |
| SOURCE + integration MySQL | **Pending DEV/TEST** |
| EnableWrites | **OFF** |

**Dừng nghiệm thu 2C-06C (safety fix) trước 2C-06D.** Không Confirm thật / không SOURCE / không bật WriteGate.

---

## 11. 2C-06D — Confirm COST_CALC B4 (WIP / giá thành + receipt)

Ngày: 2026-11-03  
Phạm vi: Confirm Bước 4 only — `ConfirmCostCalcAsync` + `CostCalcConfirmSourceGuard` + UPSERT source/wip/receipt.  
**Không** B5/REOPEN · **Không** EnableWrites · **Không** SOURCE · **Không** Confirm thật · **Không** apply giá kho · **Không** post 632 · **Không** `CONFIRMED` toàn kỳ · **Không** đổi WipCostEngine · **Không** sửa B-I/kho.

### 11.1 Implemented (source-tested)

| Hạng mục | Chi tiết |
|----------|----------|
| B4 guard | `CostCalcConfirmSourceGuard` — chặn Confirm B4 ghi thật khi nguồn chưa từ DB; **độc lập** EnableWrites; không tin WipDryRun |
| Orchestration | Gate → TX C# → lock RUN + Period → **CURRENT_RUN_ID + PERIOD_COST_ID** → VERSION/PeriodYm → config WIP từ RUN → B2/B3 DONE → engine → ReceiptReconcile ReadyToApply → UPSERT source/wip/receipt → COST_CALC=DONE → history → Commit/Rollback |
| Config authority | `WipRunConfig.FromLockedRun` — WipMethod / IncompleteMode / IncompletePercent / EnableProductReduce từ DB |
| Engine | `WipCostEngine` + `CostSourceRules` (loại XFER; 154 direct 1 lần; DEPT=0 không nhân đôi) — **không đổi công thức** |
| Receipt | `ReceiptPricePrep` theo PRODUCT+DEPT+INPUT_ID; nhiều INPUT cùng PRODUCT; reconcile Confirm; **IS_APPLIED=0**; không UPDATE `chit_inventory_input` |
| Persist | UPSERT theo UNIQUE (không ISDEL); soft-delete khóa thiếu; không đụng RUN_VERSION khác |
| Planner | Fingerprint canonical; Noop/Conflict; COST_CALC DONE + OpId mới → `NoopAlreadyConfirmed`; `WouldApplyInventory=false`; `WouldPost632=false`; ExpectedStatus=`AfterCostCalcConfirm` |
| Tests | B2/B3 chưa DONE · RUN_VERSION/CURRENT_RUN_ID · 154≠XFER · WIP method · nhiều INPUT_ID · reconcile · UNIQUE/ISDEL · retry OpId · FP conflict · mid-fail Rollback · không kho/632 · gate OFF · source guard |
| Gate | `ProductionCost:EnableWrites` mặc định **OFF** |

### 11.2 Skeleton / NOT production-ready

| Hạng mục | Ghi chú |
|----------|---------|
| Preview-from-DB / source rebuild | **Pending** — Confirm phụ thuộc WipDryRun chỉ khi test hook; production bị guard |
| `sp_pc_source_rebuild` / `sp_pc_wip_calculate` / `sp_pc_cost_confirm` | Skeleton SIGNAL — C# sở hữu persist B4 |
| Confirm B5 | **2C-06E Implemented (source)** — xem §12 |
| Reopen persist | Chưa (2C-06F) |

### 11.3 Pending MySQL integration (DEV/TEST — agent không chạy)

| Việc | Ghi chú | Phân loại |
|------|---------|-----------|
| SOURCE history DDL 2C-06A nếu chưa | Cần trước EnableWrites | **Pending integration** |
| Preview-from-DB load objects/source/receipt/opening | Cần trước khi bỏ `CostCalcConfirmSourceGuard` | **Pending** |
| Integration MySQL Confirm B4 | Sau Preview-from-DB; **EnableWrites=OFF** đến nghiệm thu | **Pending integration** |
| EnableWrites | **Giữ OFF** | **OFF** |

### 11.4 Files changed (2C-06D)

- `CostCalcConfirmSourceGuard.cs` / `WipRunConfig.cs` / `CostCalcConfirmPlanner.cs` / `WipUpsertRules.cs` — mới
- `ProductionCostWriteService.cs` — `ConfirmCostCalcAsync`
- `IProductionCostWriteRepository.cs` / `ProductionCostWriteRepository.cs` — Replace source/wip/receipt + HasB4 history
- `WipCostEngineModels.cs` — `Cogs632Posted` / `IdempotencyAction` trên response
- `ProductionCostCostCalcConfirmTests.cs` + stubs + test csproj links
- `migrations/20261103_2c06d_README.md`
- `ACCEPTANCE_2C_06.md` — mục này

### 11.5 Build / test

#### Host build (source-tested)

```text
dotnet build API_AMNOTE_WEB.csproj -o artifacts/host-2c06d-check
→ 0 Error(s)
HOST_EXIT=0
```

#### Unit test (source-tested)

```text
dotnet test --filter "FullyQualifiedName~ProductionCost|FullyQualifiedName~InventoryOutPrice"
→ Passed: 165, Failed: 0
TEST_EXIT=0
```

### 11.6 Quy tắc B4 đã giữ

1. B2 COST_ALLOCATION=DONE và B3 EXP_TO_154_ONLY=DONE trước Confirm B4.  
2. request RUN_ID = PERIOD.CURRENT_RUN_ID; PERIOD_COST_ID khớp.  
3. Không dùng WipDryRun client làm nguồn tài chính quyết định ghi (production).  
4. Không cộng XFER / INPUT_TYPE=LOCK vào incurred; 154 direct đúng 1 lần.  
5. Persist source/wip/receipt theo RUN_VERSION; UPSERT UNIQUE không gồm ISDEL.  
6. Chỉ `COST_CALC=DONE`; RUN giữ ALLOC_CONFIRMED/REOPENED — không CONFIRMED toàn kỳ.  
7. Không UPDATE giá `chit_inventory_input`; không InventoryOutPriceEngine; không post 632.  
8. History/OperationId idempotent; COST_CALC DONE + OpId mới không tính lại.  
9. Fail giữa WIP/receipt/history → Rollback.

### 11.7 Verdict / dừng

| Phần | Trạng thái |
|------|------------|
| CostCalcConfirmSourceGuard | **Implemented (source-tested)** |
| ConfirmCostCalcAsync B4 | **Implemented (source-tested)** |
| UPSERT source/wip/receipt + COST_CALC=DONE | **Implemented (source-tested)** |
| Unit tests B4 safety | **Implemented (source-tested)** |
| Preview-from-DB / Confirm production-ready | **Pending / Skeleton** |
| SOURCE + integration MySQL | **Pending DEV/TEST** |
| EnableWrites | **OFF** |

**Dừng nghiệm thu 2C-06D trước 2C-06E.** Không Confirm thật / không SOURCE / không bật WriteGate.

---

## 12. 2C-06E — Confirm VALUATION B5 (định giá + xuất kho + 632)

Ngày: 2026-11-03  
Phạm vi: Confirm Bước 5 only — `ConfirmValuationAsync` + `ValuationConfirmSourceGuard` + apply INPUT_ID + InventoryOutPriceEngine + VALUATION_COGS.  
**Không** REOPEN (2C-06F) · **Không** EnableWrites · **Không** SOURCE · **Không** Confirm thật · **Không** cập nhật kho thật · **Không** post 632 thật · **Không** đổi WIP / AllocationEngine / InventoryOutPriceEngine · **Không** `CONFIRMED` toàn kỳ · **Không** đưa B-I vào G-K khi ENGINE=GK.

### 12.1 Implemented (source-tested)

| Hạng mục | Chi tiết |
|----------|----------|
| B5 guard | `ValuationConfirmSourceGuard` — chặn khi nguồn chưa từ DB **hoặc** `SupportsInputIdSafeInventoryUpdate=false`; **độc lập** EnableWrites; không tin ValuationDryRun |
| Orchestration | Gate → source guard → INPUT_ID capability → TX C# → lock RUN + Period → **CURRENT_RUN_ID + PERIOD_COST_ID** → VERSION/PeriodYm → B2/B3/B4 DONE từ DB → đối chiếu INPUT_ID → engine xuất → apply plan → IS_APPLIED → VALUATION=DONE → history → Commit/Rollback |
| Inventory | Bắt buộc INPUT_ID + COMPANY_CD; `ValuationInputIdMatch`; không bulk PRODUCT_CD; không `apply_inventory_update_in_price` |
| Engine | `ValuationAverageCost` → `InventoryOutPriceEngine` (4 PP) — **không đổi công thức**; nhóm PRODUCT+STORE+CHIT_TYPE; loại EXP_TO_154_ONLY |
| COGS 632 | `Cogs632PreviewRules` N632/C155; STABLE_KEY `VALUATION_COGS:{COMPANY}:{PERIOD}`; InsertOnce hoặc UpdateExisting — không INSERT trùng |
| Planner | Fingerprint; Noop/Conflict; VALUATION DONE + OpId mới → `NoopAlreadyConfirmed`; `WouldPromoteFullPeriodConfirmed=false` |
| Tests | B2/B3/B4 chưa DONE · RUN_VERSION/CURRENT_RUN_ID · INPUT_ID COMPANY/PRODUCT · nhiều INPUT_ID · nhiều SP/kho · 4 PP · 632 tồn tại/chưa · retry OpId · FP conflict · B5 DONE · mid-fail Rollback · gate OFF · source guard · capability OFF |
| Gate | `ProductionCost:EnableWrites` mặc định **OFF** |

### 12.2 Skeleton / NOT production-ready

| Hạng mục | Ghi chú |
|----------|---------|
| Preview-from-DB load receipt/WIP/movements | **Pending** — Confirm phụ thuộc ValuationDryRun chỉ khi test hook; production bị guard |
| SQL UPDATE `chit_inventory_input` theo INPUT_ID+COMPANY_CD | **Skeleton** — `20261103_2c06e_sp_valuation_input_id.sql`; production `SupportsInputIdSafeInventoryUpdate=false` |
| Apply out price + post chứng từ 632 thật | **Skeleton** — orchestration C# sẵn; SQL/API chứng từ pending DEV |
| `sp_pc_valuation_confirm` | Skeleton SIGNAL — C# sở hữu TX B5 |
| Confirm REOPEN | **2C-06F Implemented (source)** — xem §13 |

### 12.3 Pending MySQL / API / repository (DEV/TEST — agent không chạy)

| Việc | Ghi chú | Phân loại |
|------|---------|-----------|
| SOURCE history DDL 2C-06A nếu chưa | Cần trước EnableWrites | **Pending integration** |
| Preview-from-DB load receipt B4 + movements + WIP | Cần trước khi bỏ `ValuationConfirmSourceGuard` | **Pending** |
| SQL INPUT_ID an toàn + bật `SupportsInputIdSafeInventoryUpdate` | Không dùng SP PRODUCT_CD | **Pending DEV** |
| UPSERT chứng từ kế toán VALUATION_COGS thật | Cùng TX; UpdateExisting nếu đã có key | **Pending DEV** |
| Integration MySQL Confirm B5 | Sau Preview-from-DB + SQL INPUT_ID | **Pending integration** |
| EnableWrites | **Giữ OFF** | **OFF** |

### 12.4 Files changed (2C-06E)

- `ValuationConfirmSourceGuard.cs` / `ValuationConfirmPlanner.cs` / `ValuationInputIdMatch.cs` — mới
- `ValuationApplyPlan.cs` — `BuildForConfirm`
- `ProductionCostWriteService.cs` — `ConfirmValuationAsync`
- `IProductionCostWriteRepository.cs` / `ProductionCostWriteRepository.cs` — B5 surface (capability=false)
- `FakeProductionCostWriteRepository.cs` — B5 fake apply
- `ValuationEngineModels.cs` — `IdempotencyAction` trên response
- `ProductionCostValuationConfirmTests.cs` + test csproj links
- `migrations/20261103_2c06e_README.md` / `20261103_2c06e_sp_valuation_input_id.sql`
- `ACCEPTANCE_2C_06.md` — mục này

### 12.5 Build / test

#### Host build (source-tested)

```text
dotnet build API_AMNOTE_WEB.csproj -o artifacts/host-2c06e-check
→ 0 Error(s)
HOST_EXIT=0
```

#### Unit test (source-tested)

```text
dotnet test --filter "FullyQualifiedName~ProductionCost|FullyQualifiedName~InventoryOutPrice"
→ Passed: 194, Failed: 0
TEST_EXIT=0
```

### 12.6 Quy tắc B5 đã giữ

1. B2/B3/B4 = DONE từ DB trước Confirm B5 — không tin DryRun status.  
2. request RUN_ID = PERIOD.CURRENT_RUN_ID; PERIOD_COST_ID khớp.  
3. Không dùng ValuationDryRun client làm nguồn quyết định ghi (production).  
4. Cập nhật nhập theo INPUT_ID + COMPANY_CD; đối chiếu PRODUCT/STORE/kỳ/đối tượng.  
5. Tính xuất bằng InventoryOutPriceEngine đã cấu hình; không trộn PRODUCT/STORE/CHIT_TYPE; không EXP_TO_154_ONLY.  
6. VALUATION_COGS STABLE_KEY — UpdateExisting nếu đã có; không INSERT trùng; không tự N632/C154.  
7. Chỉ `VALUATION=DONE`; RUN giữ ALLOC_CONFIRMED/REOPENED — không CONFIRMED toàn kỳ.  
8. Atomic TX: input → out → 632 → IS_APPLIED → step → history; fail → Rollback.  
9. Idempotency OperationId + fingerprint; B5 DONE + OpId mới không tính lại.  
10. Production Confirm blocked đến khi SQL INPUT_ID + Preview-from-DB READY.

### 12.7 Verdict / dừng

| Phần | Trạng thái |
|------|------------|
| ValuationConfirmSourceGuard + INPUT_ID capability | **Implemented (source-tested)** |
| ConfirmValuationAsync B5 | **Implemented (source-tested)** — NOT production-ready |
| Unit tests B5 safety | **Implemented (source-tested)** |
| SQL INPUT_ID / post 632 thật / Preview-from-DB | **Skeleton / Pending DEV/TEST** |
| SOURCE + integration MySQL | **Pending DEV/TEST** |
| EnableWrites | **OFF** |
| REOPEN (2C-06F) | **Implemented (source-tested)** — §13 |

**2C-06E đã nghiệm thu source; 2C-06F hoàn tất bên dưới.** Không Confirm thật / không SOURCE / không bật WriteGate / không cập nhật kho / không post 632.

---

## 13. 2C-06F — REOPEN / History / Integration readiness (+ tổng kết source 2C-06)

Ngày: 2026-11-03  
Phạm vi: Persist REOPEN trên hạ tầng 2C-06A; impact B3/B5; history/idempotency; harden RECALCULATE; bộ integration readiness (không chạy DB).  
**Không** EnableWrites · **Không** SOURCE · **Không** kết nối DB · **Không** Confirm/REOPEN thật · **Không** cập nhật CT/giá kho · **Không** deploy · **Không** đổi engine công thức / B-I / Kho cũ.

### 13.1 Deliverables 2C-06F

| Hạng mục | Chi tiết |
|----------|----------|
| Confirm REOPEN | Gate → TX C# → lock RUN+PERIOD → CURRENT_RUN_ID/PERIOD_COST_ID → VERSION/PeriodYm → steps+history DB → planner → impact guard → PersistReopen → history → Commit/Rollback |
| Version lifecycle | CONFIRMED/ALLOC_CONFIRMED → version+1 + SUPERSEDED (bất biến snapshot cũ); đã REOPENED → giữ version; không DELETE history/version cũ |
| Side-effect B3/B5 | Probe EXP_TO_154 voucher, PeriodLock claim, IS_APPLIED, VALUATION_COGS, giá kho — **block** nếu `SupportsSafeReopenSideEffectReverse=false` |
| Idempotency | Cùng OpId+FP → Noop; khác FP → Conflict; retry sau bump version nhận từ history — không REOPEN lần hai |
| History | PREVIEW/CONFIRM/REOPEN/RECALC theo thiết kế; đọc COMPANY_CD+RUN_ID+RUN_VERSION; C# sở hữu INSERT; không trùng SP |
| RECALCULATE | Preview-only + `RecalcPersistGuard`; không Confirm B2–B5 tự động; không DryRun tài chính ghi thật; không overwrite CONFIRMED |
| Integration readiness | Catalog IT-01…IT-14 trong `ProductionCostConfirmIntegrationReadinessTests` — **Skip MySQL** |
| Gate | `ProductionCost:EnableWrites` mặc định **OFF**; `SupportsSafeReopenSideEffectReverse=false` |

### 13.2 Trạng thái 2C-06A→F

| Slice | Trạng thái |
|-------|------------|
| 2C-06A History DDL + WriteRepository + TX | **Implemented (source-tested)** |
| 2C-06B Allocation Confirm B2 | **Implemented (source-tested)** — Preview-from-DB pending |
| 2C-06C EXP_TO_154 Confirm B3 | **Implemented (source-tested)** — PeriodLock shared-session |
| 2C-06D CostCalc Confirm B4 | **Implemented (source-tested)** — không apply kho |
| 2C-06E Valuation Confirm B5 | **Implemented (source-tested)** — INPUT_ID capability OFF |
| 2C-06F REOPEN / History / Integration readiness | **Implemented (source-tested)** |

### 13.3 Skeleton còn tồn tại

| Hạng mục | Ghi chú |
|----------|---------|
| Preview-from-DB B2 (pool/basis) | **Pending** — `AllocationConfirmSourceGuard` |
| Preview-from-DB B4 (source/WIP/receipt) | **Pending** — `CostCalcConfirmSourceGuard` |
| Preview-from-DB B5 (receipt/WIP/movements) | **Pending** — `ValuationConfirmSourceGuard` |
| SQL INPUT_ID + apply out + post 632 | **Skeleton** — `SupportsInputIdSafeInventoryUpdate=false` |
| Reverse B3/B5 trong TX khi REOPEN | **Skeleton** — `SupportsSafeReopenSideEffectReverse=false` |
| `sp_pc_valuation_confirm` / `sp_pc_reopen` | Không dùng — C# TX owner |
| `sp_pc_source_rebuild` / wip_calculate | Skeleton SIGNAL — C# persist B4 |

### 13.4 Còn thiếu trước EnableWrites

| Hạng mục | Phân loại |
|----------|-----------|
| Preview-from-DB B2/B4/B5 | **Pending** |
| SQL INPUT_ID inventory capability | **Pending DEV** |
| SOURCE migration/SP (history 2C-06A, alloc_confirm, PeriodLock…) | **Pending DEV** |
| Integration MySQL IT-01…IT-14 | **Pending DEV/TEST** (catalog sẵn, chưa chạy) |
| Reverse an toàn EXP_TO_154 / IS_APPLIED / 632 | **Pending** trước REOPEN production khi còn side-effect |
| Điều kiện EnableWrites | SOURCE + verify_readonly + Preview-from-DB + capability INPUT_ID + reverse REOPEN + integration PASS — **DEV/TEST only** |

### 13.5 Integration tests prepared but NOT executed

| ID | Scenario |
|----|----------|
| IT-01 | Migration/schema preflight (history DDL + indexes) |
| IT-02 | MySQL 5.6/8.0 UNIQUE utf8 630≤767 |
| IT-03 | RUN_VERSION + CURRENT_RUN_ID consistency |
| IT-04 | Transaction rollback B2→B5 |
| IT-05 | PeriodLock shared-session atomicity |
| IT-06 | UPSERT UNIQUE/ISDEL |
| IT-07 | Race / duplicate OperationId |
| IT-08 | INPUT_ID ownership + COMPANY_CD isolation |
| IT-09 | Không ghi trùng VALUATION_COGS/632 |
| IT-10 | REOPEN + snapshot version bất biến |
| IT-11 | B-I không bị ảnh hưởng |
| IT-12 | Reconcile số liệu KT/kho trước/sau |
| IT-13 | REOPEN blocked khi thiếu reverse B3/B5 |
| IT-14 | History PREVIEW/CONFIRM/REOPEN/RECALC |

### 13.6 Files changed (2C-06F)

- `Lifecycle/ReopenImpactGuard.cs`, `ReopenConfirmPlanner.cs`, `ReopenIdempotency.cs`, `RecalcPersistGuard.cs` — mới
- `ProductionCostWriteService.cs` — `ConfirmReopenAsync` + harden `PreviewRecalculateAsync`
- `IProductionCostWriteRepository.cs` / `ProductionCostWriteRepository.cs` — probe + PersistReopen + history SELECT mở rộng
- `FakeProductionCostWriteRepository.cs` — REOPEN fake
- `LifecycleModels.cs` / `ProductionCostModels.cs` — PersistedHistoryRef + IdempotencyAction + ReopenedBy
- `ProductionCostReopenConfirmTests.cs` / `ProductionCostConfirmIntegrationReadinessTests.cs`
- `migrations/20261103_2c06f_README.md`
- `ACCEPTANCE_2C_06.md` — mục này

### 13.7 Build / test

#### Host build (source-tested)

```text
dotnet build API_AMNOTE_WEB.csproj -o artifacts/host-2c06f-check
→ 0 Error(s)
HOST_EXIT=0
```

#### Unit test (source-tested)

```text
dotnet test --filter "FullyQualifiedName~ProductionCost|FullyQualifiedName~InventoryOutPrice"
→ Passed: 212, Failed: 0, Skipped: 1 (integration readiness MySQL)
TEST_EXIT=0
```

### 13.8 Quy tắc REOPEN đã giữ

1. Xác thực COMPANY_CD, database, RUN_ID, RUN_VERSION, PERIOD_YM, quyền (userId).  
2. Khóa RUN + PERIOD cùng DapperSession; CURRENT_RUN_ID + PERIOD_COST_ID.  
3. REOPEN từ CONFIRMED/ALLOC_CONFIRMED → version+1; từ REOPENED → không tăng.  
4. Snapshot version đã xác nhận bất biến — không xóa/ghi đè.  
5. Ghi REOPEN_REASON / BY / AT + history cùng TX.  
6. Cùng OperationId → Noop sau kiểm DB; khác payload/FP → Conflict.  
7. Retry sau đổi version nhận từ history — không REOPEN lần hai.  
8. Không tự hủy CT EXP_TO_154 / VALUATION_COGS / giá kho; thiếu reverse → block.  
9. Không trả REOPEN success khi chứng từ/giá kho chưa nhất quán (block trước persist).  
10. WriteGate OFF; không SOURCE; không DB thật trong slice này.

### 13.9 Verdict / dừng toàn 2C-06 (source)

| Phần | Trạng thái |
|------|------------|
| Confirm B2–B5 + REOPEN (source) | **Implemented (source-tested)** |
| WriteGate | **OFF** |
| Preview-from-DB / SQL INPUT_ID / reverse REOPEN | **Skeleton / Pending** |
| SOURCE + integration MySQL | **Pending DEV/TEST** |
| EnableWrites | **OFF — chưa đủ điều kiện** |
| Engine công thức / B-I / Kho cũ | **Không đổi** |

**Dừng sau 2C-06F để nghiệm thu source toàn đoạn.** Chưa chuyển triển khai production / EnableWrites / SOURCE / Confirm thật.
