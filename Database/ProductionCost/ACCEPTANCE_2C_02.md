# Nghiệm thu Giai đoạn 2C-02 — Phân bổ 622/627 + kết chuyển EXP_TO_154_ONLY

Ngày: 2026-11-02  
Phạm vi: Engine phân bổ thuần C#, API POST Bước 2/3 (chặn ghi), quy tắc exclusive, script SQL bổ sung.  
**Không** kết nối DB · **Không** chạy migration · **Không** đụng production · **Không** đổi FE · **Không** Bước 4/5/reopen.

---

## 1. File tạo mới / chỉnh sửa

### Mới
| File |
|------|
| `Services/ProductionCost/Allocation/AllocationMath.cs` |
| `Services/ProductionCost/Allocation/AllocationEngine.cs` |
| `Services/ProductionCost/Allocation/NvlBasisCalculator.cs` |
| `Services/ProductionCost/Allocation/PoolAccountRules.cs` |
| `Services/ProductionCost/Transfer/CostEngineExclusive.cs` |
| `Services/ProductionCost/Transfer/ExpTo154Rules.cs` |
| `Services/ProductionCost/ConfirmIdempotency.cs` |
| `Services/ProductionCost/ProductionCostWriteGate.cs` |
| `Services/ProductionCost/ProductionCostWriteService.cs` |
| `Models/AllocationEngineModels.cs` |
| `Models/ProductionCostWriteModels.cs` |
| `Interfaces/IProductionCostWriteService.cs` |
| `API_AMNOTE_WEB.Tests/ProductionCostAllocationEngineTests.cs` |
| `Database/ProductionCost/migrations/20261102_2c02_sp_alloc.sql` |
| `Database/ProductionCost/migrations/20261102_2c02_verify_readonly.sql` |
| `Database/ProductionCost/migrations/20261102_2c02_README.md` |
| `Database/ProductionCost/ACCEPTANCE_2C_02.md` |

### Chỉnh
| File | Thay đổi |
|------|----------|
| `Controllers/ProductionCostController.cs` | 4 POST endpoint + write error handling |
| `Program.cs` | Đăng ký WriteGate (mặc định OFF) + WriteService |
| `API_AMNOTE_WEB.Tests.csproj` | Link source 2C-02 cho unit test |

---

## 2. API / DTO mới

| Method | Path | Hành vi 2C-02 |
|--------|------|----------------|
| POST | `/api/production-cost/allocation/preview` | Dry-run nếu có `DryRun` payload → `Persisted=false`. Không DryRun + gate OFF → BusinessError chặn. |
| POST | `/api/production-cost/allocation/confirm` | **Luôn chặn** khi `EnableWrites=false` (mặc định). |
| POST | `/api/production-cost/transfer/preview` | Kiểm tra quy tắc EXP_TO_154_ONLY; `Persisted=false`; không tạo CT. |
| POST | `/api/production-cost/transfer/confirm` | **Luôn chặn** khi gate OFF. |

DTO chính: `ProductionCostWriteRequest`, `AllocationDryRunPayload`, `ProductionCostAllocationWriteResponse`, `ProductionCostTransferWriteResponse`.

Cấu hình chặn: `ProductionCost:EnableWrites` (default `false`; production luôn OFF dù flag=true).

---

## 3. Stored procedure — đã có vs cần bổ sung

### Đã implement ở 2B (không tạo lại)
- `sp_pc_engine_check` / `claim` / `claim_for_cogs` / `assert_for_cogs` / `release_cogs`
- `sp_pc_nvl_basis_build` (allocate_type=0, chống nhân đôi CHITDETAIL / orphan)
- PeriodLock COGS nhận `EXP_TO_154_ONLY` + giữ 3 rule B-I

### Đặc tả 2A chưa có body ở 2B → script bàn giao 2C-02
| SP | File | Ghi chú |
|----|------|---------|
| `sp_pc_alloc_load_pool` | `20261102_2c02_sp_alloc.sql` | Aggregate pool; 1 CHITDETAIL_ID / 1 lần |
| `sp_pc_alloc_load_basis_qty` | idem | Basis type=1 (N155/C154) |
| `sp_pc_alloc_calculate` | idem | Skeleton SIGNAL — engine chính = C# đến khi bật ghi |
| `sp_pc_alloc_confirm` | idem | Idempotent `STABLE_KEY=ALLOC:{COMPANY}:{PERIOD}` |

User tự SOURCE sau nghiệm thu 2B DEV. Verify: `20261102_2c02_verify_readonly.sql`.

Bước 3: **tái sử dụng** PeriodLock 2B — không SP 154→155/632 mới.

---

## 4. Build & unit test (không DB)

```text
dotnet build API_AMNOTE_WEB.csproj -o _build_2c02_out
→ Build succeeded. 0 Error(s)

dotnet test --filter FullyQualifiedName~ProductionCost
→ Passed: 42, Failed: 0
  (2C-01: 30 + 2C-02 AllocationEngine/WriteGate: 12)
```

### Checklist test 2C-02
| Case | Kết quả |
|------|---------|
| ALLOCATE_TYPE=0 (NVL) ví dụ tài liệu | PASS |
| ALLOCATE_TYPE=1 (SL TP) ví dụ tài liệu | PASS |
| Chi phí theo phòng ban + fallback dùng chung | PASS |
| REDUCTION + làm tròn + dòng cuối phần dư | PASS |
| NVL orphan NO_BOM / SPLIT_QTY_ZERO | PASS |
| Không nhân đôi CHITDETAIL → nhiều OUTPUT | PASS |
| Không đếm trùng 6212 khi flag off | PASS |
| RUN_VERSION không hợp lệ | PASS |
| Exclusive B-I / G-K conflict | PASS |
| Confirm lặp → UPDATE, không INSERT mới | PASS |
| Preview `Persisted=false` | PASS |
| Confirm bị chặn gate | PASS |
| Bước 3 không 154→155/632 | PASS |

---

## 5. Quy tắc đã chốt trong code

- JOIN/_ID: engine thuần không SQL; script SP JOIN `COMPANY_CD` + `*_ID`.
- Bước 3: `StepCode=EXP_TO_154_ONLY`, `BiStepCode=COGS_SUMMARY`, `CogsRule=EXP_TO_154_ONLY`.
- Exclusive `COST_ENGINE` GK ↔ BI.
- Preview không ghi đè CONFIRMED; Confirm idempotent theo `STABLE_KEY`.

---

## 6. Dừng

Hoàn thành **2C-02**.  
**Không** tự chuyển 2C-03 · **Không** bật `EnableWrites` · **Không** thực thi SQL trên DEV/TEST/production.
