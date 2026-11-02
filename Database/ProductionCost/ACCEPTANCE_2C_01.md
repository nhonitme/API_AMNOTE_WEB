# Nghiệm thu Giai đoạn 2C-01 — Backend API đọc G-K

Ngày: 2026-11-02 (rà soát hoàn thiện sau báo cáo build 0 error / 16 test)  
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
- DB: `Common.GetDatabaseName()` **bắt buộc** — không nuốt lỗi / không fallback DB ẩn.
- Validation lỗi → `ValidationError`; thiếu COMPANY/DB → `BusinessError`.

### Sáu bước (catalog cố định — có/không dữ liệu cùng mã)

| # | StepCode (G-K / DDL) | BiStepCode (PeriodLock) | Engine | Ghi chú |
|---|----------------------|-------------------------|--------|---------|
| 1 | `FA_PREPAID_LOCK` | `FA_PREPAID_LOCK` | BI | Khóa B-I |
| 2 | `COST_ALLOCATION` | — | GK | Workflow G-K |
| 3 | `EXP_TO_154_ONLY` | `COGS_SUMMARY` | BI | `CogsRule` kỳ = `EXP_TO_154_ONLY` |
| 4 | `COST_CALC` | — | GK | |
| 5 | `VALUATION` | — | GK | |
| 6 | `PROFIT_LOSS_REPORT` | `PROFIT_LOSS_REPORT` | BI | Khóa B-I |

Ánh xạ bước 3: **StepCode** `EXP_TO_154_ONLY` ↔ **BiStepCode** `COGS_SUMMARY` ↔ **CogsRule** `EXP_TO_154_ONLY` — ba khái niệm tách biệt, không đổi mã theo “có/không dòng”.

### Đồng bộ B-I / G-K
- B1/B3/B6: trạng thái khóa từ `acc_period_lock_step`; workflow/preview/REOPENED từ `production_cost_step`.
- Lệch trạng thái → `StatusMismatch=true`; **không** hiển thị DONE khi B-I chưa DONE.
- Lỗi truy vấn B-I → `InvalidOperationException` (không coi như `NOT_STARTED`).
- `REOPENED` giữ `PreviousConfirmedSummary` từ `production_cost_history`; version CONFIRMED cũ vẫn đọc được qua `runVersion`.

### Schema / kỳ / version
- `SchemaStatus`: `MISSING` | `INCOMPLETE` | `READY` (+ `MissingTables`).
- Kỳ chưa tạo: `PeriodExists=false`, GET `/steps` vẫn trả **đủ 6 bước** catalog, **không INSERT**.
- `RUN_ID` phải thuộc `COMPANY_CD + PERIOD_YM`.
- `RunVersionExists` / `HasDetailData` phân biệt version chưa tồn tại vs version hợp lệ nhưng rỗng.

---

## 3. Bảng / SP sử dụng

### Bảng (SELECT only)
`production_cost_period`, `run`, `step`, `object`, `alloc_pool`, `alloc_detail`, `nvl_basis`, `nvl_basis_line`, `nvl_orphan`, `source`, `wip`, `receipt`, `voucher_ref`, `history`  
+ `acc_period_lock_step` (trạng thái B-I)

Probe schema: đủ 14 bảng `production_cost_*` ở trên → `READY`; thiếu một phần → `INCOMPLETE`; không có bảng nào → `MISSING`.

### Stored procedure
**Không** CALL SP ghi trong GET. Không dùng SP đọc riêng 2C-01.

---

## 4. Build & unit test (2026-11-02 — sau rà soát)

```text
dotnet build API_AMNOTE_WEB.csproj -o _build_2c01_out
→ Build succeeded. 0 Error(s) (warnings pre-existing CA1416 / reporting)

dotnet test --filter FullyQualifiedName~ProductionCost
→ Passed: 30, Failed: 0 (Validation + ServiceRead, không DB)
```

### Unit test bổ sung (checklist rà soát)
| Case | Kết quả |
|------|---------|
| G-K DONE nhưng B-I OPEN → không hiện DONE + `StatusMismatch` | PASS |
| Kỳ chưa tạo vẫn trả catalog 6 bước + `PeriodExists=false` | PASS |
| `RUN_VERSION` không tồn tại | PASS |
| `RUN_ID` thuộc công ty/kỳ khác | PASS |
| Schema `INCOMPLETE` / `MISSING` | PASS |
| Thiếu database name / COMPANY_CD | PASS |
| REOPENED vẫn đọc kết quả đã xác nhận (+ version cũ) | PASS |
| Lỗi truy vấn B-I không bị nuốt thành NOT_STARTED | PASS |
| StepCode / BiStepCode / CogsRule tách bạch bước 3 | PASS |

---

## 5. Chờ migration 2B DEV

| Hạng mục | Trạng thái |
|----------|------------|
| Runtime GET trên DB thật | **Chưa** — schema chưa migrate DEV |
| `SchemaStatus=MISSING/INCOMPLETE` khi thiếu bảng | Đã code |
| FE mock → API thật | **Ngoài** 2C-01 |
| POST/confirm/lock/unlock | **2C-02+** |

API runtime **chưa** được coi PASS cho đến khi 2B nghiệm thu DEV và smoke GET.

---

## 6. Dừng

Hoàn thành **2C-01** (đã rà soát hoàn thiện). Không tự chuyển 2C-02 / không triển khai database / không đụng production.
