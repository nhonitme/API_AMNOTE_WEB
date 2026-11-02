# Nghiệm thu Giai đoạn 2C-02 — Phân bổ 622/627 + EXP_TO_154_ONLY

Ngày: 2026-11-02 (cập nhật sau rà soát fix logic)  
Phạm vi: Engine phân bổ thuần C#, quy tắc Bước 3, script SQL bổ sung, API POST **có gate chặn ghi**.  
**Không** kết nối DB · **Không** chạy migration · **Không** bật `EnableWrites` · **Không** 2C-03.

---

## 0. Phân biệt trạng thái triển khai (bắt buộc)

| Hạng mục | Trạng thái thật |
|----------|----------------|
| `AllocationEngine` C# (type 0/1, byDepartment, REDUCTION, làm tròn, Σ=P_A) | **Đã implement** + unit test |
| `ExpTo154Rules` (cấm N155/C154, N632/C155, N632/C154; cho N154/C621–627) | **Đã implement** + unit test |
| `CostEngineExclusive` / `ConfirmIdempotency` | **Đã implement** (logic thuần) |
| API `POST .../allocation/preview` | **Chỉ DryRun payload** — `Persisted=false`; chưa đọc/ghi DB |
| API `POST .../allocation/confirm` | **Chặn gate** — chưa có luồng ghi DB hoàn chỉnh |
| API `POST .../transfer/preview\|confirm` | Preview: kiểm tra rule; Confirm: **chặn gate** |
| `sp_pc_alloc_load_pool` / `load_basis_qty` / `alloc_confirm` | **Script bàn giao** — chưa coi là đã chạy DEV |
| `sp_pc_alloc_calculate` | **Skeleton SIGNAL** — chưa body SQL |
| PeriodLock `EXP_TO_154_ONLY` (2B) | **Có trong migration 2B** — phụ thuộc user tự cài DEV |

**Không báo cáo** Preview/Confirm DB hay `sp_pc_alloc_calculate` là đã vận hành thật.

---

## 1. File tạo mới / chỉnh sửa (sau fix)

### Logic C#
- `Services/ProductionCost/Allocation/AllocationEngine.cs` — **sửa** RunByDepartment (shared pool 1 lần), Σ=P_A, lỗi ΣT=0
- `Services/ProductionCost/Transfer/ExpTo154Rules.cs` — **sửa** downstream Nợ/Có
- `Models/AllocationEngineModels.cs` — thêm `PoolDepartmentId`
- Tests: `ProductionCostAllocationEngineTests.cs`

### SQL bàn giao
- `migrations/20261102_2c02_sp_alloc.sql` — **viết lại** pool (ext.DEPARTMENT_ID, 3619), basis qty, confirm idempotent
- `migrations/20261102_2c02_README.md` — trạng thái rõ
- `migrations/20261102_2c02_verify_readonly.sql`

### API (không kích hoạt ghi)
- Controller 4 POST + `ProductionCostWriteGate` mặc định OFF

---

## 2. Fix nghiệp vụ đã làm

### 2.1 AllocationEngine.RunByDepartment
- Pool `DEPARTMENT_ID=0` phân bổ **một lần** trên toàn bộ đối tượng (không lặp theo từng phòng).
- Pool `DEPARTMENT_ID>0` chỉ phân bổ trong đúng phòng đó.
- Giữ dòng theo `(ProductId, DepartmentId, AccountCd, PoolDepartmentId)`.
- Sau làm tròn: Σ allocated từng pool = `ALLOCATION_COSTS` (assert).
- Σ tiêu thức = 0 nhưng còn `ALLOCATION_COSTS` → **InvalidOperationException** (không âm thầm 0).

### 2.2 ExpTo154Rules
Cấm Step 3 (kể cả TK chi tiết): `N155/C154`, `N632/C155`, `N632/C154`, `N154/C155|C632`, `N155/C632`.  
Cho phép: `N154*/C621*|622*|623*|627*`.

### 2.3 sp_pc_alloc_confirm (script)
- Tham số `p_RUN_VERSION`; khớp header RUN.
- Chỉ confirm khi có đủ pool + detail cùng version.
- Upsert `production_cost_step` step 2.
- Confirm lặp cùng version + ref → `NOOP_ALREADY_CONFIRMED`, không thêm history.
- Không dùng `CHIT_ID IS NULL` làm điều kiện “chưa confirm”.
- `START TRANSACTION` + `EXIT HANDLER` rollback.

### 2.4 Đồng bộ SQL ↔ C#
- REDUCTION / ALLOCATION_COSTS / company **3619** giống `AllocationMath`.
- `DEPARTMENT_ID` từ `chitdetailinfo_ext` (JOIN `COMPANY_CD` + `CHITDETAIL_ID`).
- Load pool / basis tránh nhân dòng JOIN object.

---

## 3. Build & unit test

```text
dotnet build API_AMNOTE_WEB.csproj -o _build_2c02_fix_out
→ Build succeeded. 0 Error(s)

dotnet test --filter FullyQualifiedName~ProductionCost
→ Passed: 45, Failed: 0
```

Checklist bổ sung sau fix:
| Case | Kết quả |
|------|---------|
| Shared pool không nhân theo số phòng ban | PASS |
| Cùng PRODUCT_ID khác DEPARTMENT_ID không mất dòng | PASS |
| ΣT=0 + P_A>0 → lỗi | PASS |
| Σ allocated = ALLOCATION_COSTS | PASS (assert trong Run) |
| Downstream N155/C154, N632/C155, N632/C154 (+ chi tiết) | PASS |
| N154/C6221 hợp lệ | PASS |
| Company 3619 REDUCTION = CREDIT | PASS |

---

## 4. Script user tự chạy (sau 2B DEV)

1. `SOURCE 20261102_2c02_sp_alloc.sql`
2. `SOURCE 20261102_2c02_verify_readonly.sql`

---

## 5. Dừng

Fix logic **2C-02** xong để duyệt.  
Không tự chuyển 2C-03 · không bật ghi DB · không thực thi SQL trên môi trường thật.
