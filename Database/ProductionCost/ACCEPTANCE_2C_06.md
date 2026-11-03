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
| API Confirm + Preview-from-DB | **Placeholder** — gate OFF hoặc throw sau `EnsureWritable` |
| Repository ghi / TX session G-K | **2C-06A Implemented** (infra) — Confirm chưa gắn |
| SP persist B2 (confirm/load/assert) | **Script bàn giao** — body sẵn; chưa coi đã SOURCE DEV |
| SP calculate / WIP / valuation / reopen | **Skeleton SIGNAL** hoặc **Pending** |
| History idempotency (`OperationId` + `STABLE_KEY`) | **Logic C# + DDL script 2C-06A** — chưa SOURCE DEV |
| Tái sử dụng kho / PeriodLock | **Đã xác định điểm nối** — Confirm chưa gắn |

**Kết luận (cập nhật 2C-06A):** Hạ tầng ghi + DDL history đã có trong source. Confirm B2–B5 vẫn placeholder; WriteGate OFF. Chờ nghiệm thu 2C-06A trước 2C-06B.

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
| `ConfirmAllocation` | `EnsureWritable` → throw skeleton |
| `PreviewTransfer` (gate ON) / `ConfirmTransfer` | Throw skeleton (gate OFF: preview rule-only) |
| `PreviewCostCalc` (không WipDryRun) | Throw |
| `ConfirmCostCalc` | Throw skeleton |
| `PreviewValuation` (không ValuationDryRun) | Throw |
| `ConfirmValuation` | Throw skeleton |
| `PreviewReopen` (không ReopenDryRun) | Throw |
| `ConfirmReopen` | Throw skeleton |
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
| O-01 | **Process** | DDL history 2C-06A đã có script — **chưa SOURCE DEV**; GET `/history` cần cột mới sau khi SOURCE hoặc cài mới |
| O-02 | **Blocker Confirm** | Write repository/TX infra **đã có (2C-06A)** — Confirm B2–B5 vẫn placeholder (2C-06B+) |
| O-03 | High | `apply_inventory_update_*` theo PRODUCT_CD ≠ plan B5 theo INPUT_ID — cần SP/wrapper riêng |
| O-04 | High | SP calculate/wip/valuation/reopen còn skeleton hoặc pending — quyết định C#-persist vs hoàn thiện SQL trước EnableWrites |
| O-05 | High | B3 TX boundary: PeriodLock session vs G-K session chưa thiết kế atomic thống nhất |
| O-06 | Medium | `sp_pc_alloc_confirm` history INSERT chưa gắn OperationId — lệch `HistoryPlan` |
| O-07 | Medium | Preview-from-DB chưa implement — FE/API phụ thuộc DryRun payload |
| O-08 | Medium | Chưa có integration test gắn DB/SP |
| O-09 | Low | Không có `ConfirmRecalculate` API — chấp nhận nếu Confirm từng bước; document cho FE |
| O-10 | Process | Migration 2B/2C SQL **chưa** coi là đã SOURCE trên DEV — WriteGate phải giữ OFF |
| O-11 | Out of scope 2C-06 | `RUN.STATUS=CONFIRMED` toàn kỳ / B6 PL — sau B5 ổn định |

---

## 7. Đề xuất thứ tự triển khai (slice)

| Slice | Nội dung | Trạng thái |
|-------|----------|------------|
| **2C-06A** | History DDL + WriteRepository + TX infra | **Implemented (source)** — chờ nghiệm thu |
| 2C-06B | Allocation Confirm B2 | Chưa |
| 2C-06C | EXP_TO_154_ONLY Confirm B3 | Chưa |
| 2C-06D | CostCalc Confirm B4 | Chưa |
| 2C-06E | Valuation Confirm B5 | Chưa |
| 2C-06F | Reopen / History / Integration tests | Chưa |

Sau 2C-06F + DEV SOURCE + verify → mới xét `EnableWrites=true` (DEV/TEST only).

---

## 8. 2C-06A Implementation

Ngày: 2026-11-03  
Phạm vi: History DDL, Write Repository, TX ownership C#.  
**Không** Confirm thật · **Không** EnableWrites · **Không** SOURCE · **Không** kết nối DB · **Không** đổi KT/kho.

### 8.1 Deliverables

| Hạng mục | File / ghi chú |
|----------|----------------|
| DDL migration (MySQL 5.6/8.0) | `migrations/20261103_2c06a_ddl_history_operation.sql` — ADD `OPERATION_ID`, `STABLE_KEY`, `STEP_CODE`; UNIQUE `uk_pc_hist_stable`; INDEX `idx_pc_hist_operation` |
| Verify readonly | `migrations/20261103_2c06a_verify_readonly.sql` |
| README bàn giao | `migrations/20261103_2c06a_README.md` |
| Schema cài mới | `01_production_cost_tables.sql`, `migrations/20261102_2b_01_tables.sql` đồng bộ cột/index |
| DTO | `ProductionCostHistoryDto` (+ OperationId/StableKey/StepCode); `ProductionCostHistoryInsert` / `WriteResult` / schema probe; `WriteRequest.OperationId` |
| Context / TX | `ProductionCostWriteContext.cs`, `ProductionCostWriteTransaction.cs` (pure), `ProductionCostWriteSession.cs` (`DapperSession`) |
| Interface + repo | `IProductionCostWriteRepository`, `ProductionCostWriteRepository` — BeginSession, LockRun FOR UPDATE, step statuses, persisted OperationIds, TryInsertHistory idempotent |
| DI | `Program.cs` đăng ký `IProductionCostWriteRepository` |
| Read history | `GetHistoryAsync` SELECT cột mới (cần DDL 2C-06A trên DB cũ) |
| Unit test | `ProductionCostWriteInfrastructureTests.cs` |

### 8.2 TX ownership (giữ nguyên rule)

1. Caller C# mở `BeginWriteSessionAsync` → `DapperExecutor.CreateSessionAsync` (BEGIN).  
2. Work: SQL / CALL SP — SP **không** START/COMMIT.  
3. `ProductionCostWriteSession.ExecuteAsync` → `CompleteAsync` Commit hoặc Rollback.  
4. Trước ghi history: `ProbeHistoryWriteSchemaAsync` + `AssertHistoryWriteSchemaReady`.  
5. Idempotency: `HistoryStableKeyExists` + `HistoryPlan.PlanWrite(OperationId)` — không tin client hint.

### 8.3 Không làm trong 2C-06A

- Confirm B2–B5 / Reopen persist  
- Bật `EnableWrites`  
- Đổi `AllocationEngine` / WIP / Valuation / PeriodLock / menu Kho  
- SOURCE migration hoặc gọi DB  

### 8.4 Kiểm thử

#### Host build (output riêng — tránh DLL lock)

```text
dotnet build API_AMNOTE_WEB.csproj -o artifacts/host-2c06a-check
→ 0 Error(s)  (warnings sẵn có / nullable / CA1416 — không lỗi biên dịch mới chặn)
HOST_EXIT=0
```

#### Unit test

```text
dotnet test --filter "FullyQualifiedName~ProductionCost|FullyQualifiedName~InventoryOutPrice"
→ Passed: 97, Failed: 0
TEST_EXIT=0
```

Gồm thêm: WriteContext COMPANY/RUN/OperationId · locked run version · history schema gate · HistoryPlan noop · TX CompleteAsync commit/rollback · WriteGate vẫn OFF.

### 8.5 Dừng 2C-06A

Hạ tầng 2C-06A xong trong source — **chờ nghiệm thu trước 2C-06B (Allocation Confirm).**
)