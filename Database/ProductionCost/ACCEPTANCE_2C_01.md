# Nghiệm thu Giai đoạn 2C-01 — Backend API đọc G-K

Ngày: 2026-11-02  
Phạm vi: Controller / Service / Repository / DTO / DI / unit test — **chỉ đọc**.  
**Không** chạy migration, **không** kết nối DEV/production, **không** ghi dữ liệu.

---

## 1. File tạo mới / chỉnh sửa

### Mới
| File |
|------|
| `Controllers/ProductionCostController.cs` |
| `Interfaces/IProductionCostRepository.cs` |
| `Interfaces/IProductionCostService.cs` |
| `Repositories/ProductionCostRepository.cs` |
| `Services/ProductionCost/ProductionCostService.cs` |
| `Services/ProductionCost/ProductionCostValidation.cs` |
| `Models/ProductionCostModels.cs` |
| `API_AMNOTE_WEB.Tests/ProductionCostValidationTests.cs` |
| `API_AMNOTE_WEB.Tests/ProductionCostServiceReadTests.cs` |
| `Database/ProductionCost/ACCEPTANCE_2C_01.md` |

### Chỉnh
| File | Thay đổi |
|------|----------|
| `Program.cs` | Đăng ký `IProductionCostRepository` / `IProductionCostService` |
| `Data/DapperExecutor.cs` (+ API_Build) | Thêm `QueryFirstOrDefaultAsync` |
| `API_AMNOTE_WEB.Tests.csproj` | Link source G-K cho unit test |

---

## 2. Endpoint (prefix `/api/production-cost`)

| Method | Path | Query | Response |
|--------|------|-------|----------|
| GET | `/period` | `periodYm` | `ProductionCostPeriodDto` |
| GET | `/steps` | `periodYm`, `runId?`, `runVersion?` | `ProductionCostStepsResponse` |
| GET | `/objects` | idem | `ProductionCostObjectsResponse` |
| GET | `/allocation/pool` | idem | `ProductionCostAllocPoolResponse` |
| GET | `/allocation/basis` | idem | `ProductionCostAllocBasisResponse` |
| GET | `/allocation/detail` | idem | `ProductionCostAllocDetailResponse` |
| GET | `/wip` | idem | `ProductionCostWipResponse` |
| GET | `/receipts` | idem | `ProductionCostReceiptsResponse` |
| GET | `/history` | idem | `ProductionCostHistoryResponse` |
| GET | `/comparison` | `periodYm`, `baseRunId?`, `baseRunVersion?`, `compareRunId?`, `compareRunVersion?` | `ProductionCostComparisonResponse` |

- Auth: `[Authorize]`; `COMPANY_CD` từ `Common.GetCompanyCode()` (không nhận company từ FE).
- DB: `Common.GetDatabaseName()` khi có.
- Validation lỗi → `ValidationError`; thiếu COMPANY → `BusinessError`.

### Sáu bước
1. `FA_PREPAID_LOCK` (BI)  
2. `COST_ALLOCATION` (GK)  
3. `COGS_SUMMARY` / rule `EXP_TO_154_ONLY` (PeriodLock dùng chung)  
4. `COST_CALC` (GK)  
5. `VALUATION` (GK)  
6. `PROFIT_LOSS_REPORT` (BI)  

B1/B3/B6 đọc `acc_period_lock_step` — **không** tạo khóa sổ B-I thứ hai.  
`REOPENED` giữ `PreviousConfirmedSummary` từ `production_cost_history`.

---

## 3. Bảng / SP sử dụng

### Bảng (SELECT only)
`production_cost_period`, `run`, `step`, `object`, `alloc_pool`, `alloc_detail`, `nvl_basis`, `nvl_basis_line`, `nvl_orphan`, `source`, `wip`, `receipt`, `history`  
+ `acc_period_lock_step` (trạng thái B-I)

### Stored procedure
**Không** CALL SP ghi trong GET. Không dùng SP đọc riêng 2C-01.

---

## 4. Build & unit test (2026-11-02)

```text
dotnet build API_AMNOTE_WEB.csproj -o _build_2c01_out
→ Build succeeded. 0 Error(s) (warnings pre-existing + 1 nullable trên ProductionCostController)

dotnet test --filter FullyQualifiedName~ProductionCost
→ Passed: 16, Failed: 0 (Validation + ServiceRead, không DB)
```

Kiểm thử không DB: PeriodYm / RunId / RunVersion; schema missing; period missing; 6 steps + BI map; COMPANY_CD bắt buộc; pool rỗng khi chưa có run.

---

## 5. Chờ migration 2B DEV

| Hạng mục | Trạng thái |
|----------|------------|
| Runtime GET trên DB thật | **Chưa** — schema chưa migrate DEV |
| `SchemaAvailable=false` khi thiếu bảng | Đã code |
| FE mock → API thật | **Ngoài** 2C-01 |
| POST/confirm/lock/unlock | **2C-02+** |

API runtime **chưa** được coi PASS cho đến khi 2B nghiệm thu DEV và smoke GET.

---

## 6. Dừng

Hoàn thành **2C-01**. Không tự chuyển 2C-02 / không triển khai database.
