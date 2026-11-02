# Nghiệm thu Giai đoạn 2C-03 — Tính dở dang và giá thành thành phẩm

Ngày: 2026-11-02 (fix logic chống cộng trùng / so sánh / B4 status)  
Phạm vi: Engine WIP/GT thuần C#, Preview dry-run, so sánh phiên, chuẩn bị giá INPUT_ID, script SQL skeleton.  
**Không** kết nối DB · **Không** chạy SQL · **Không** bật `EnableWrites` · **Không** cập nhật giá nhập kho thật · **Không** production.

---

## 0. Phân biệt trạng thái triển khai

| Hạng mục | Trạng thái thật |
|----------|----------------|
| `WipCostEngine` (PERCENT / Qdd / SL_TP_SX / ID2720) | **Đã implement** + unit test |
| `CostSourceRules.DistributeToProducts` — DEPT=0 dùng 1 lần; Σ trước=sau; loại XFER | **Đã implement** + unit test |
| `CostRunCompare` — khóa PRODUCT+DEPT; CONFIRMED_ONLY / PREVIEW_ONLY / BOTH | **Đã implement** + unit test |
| `ReceiptPricePrep` — PRODUCT+DEPT+INPUT_ID; không chọn đơn giá tùy ý | **Đã implement** + unit test |
| `GkRunStatus.AfterCostCalcConfirm` — COST_CALC=DONE ≠ CONFIRMED toàn kỳ | **Đã implement** + unit test |
| `WipRecalcGuard` — Preview không đụng snapshot CONFIRMED | **Đã implement** + unit test |
| API `POST .../cost-calc/preview` | **DryRun** — `Persisted=false` |
| API `POST .../cost-calc/confirm` | **Chặn gate** — skeleton |
| `sp_pc_source_rebuild` / `wip_calculate` / `cost_confirm` | **Skeleton SIGNAL** |
| Cập nhật giá nhập kho thật | **Chưa** |

**Không báo cáo** Confirm DB / apply inventory là đã vận hành.

---

## 1. Fix logic (sau rà soát)

### 1.1 CostSourceRules
- Nguồn `DEPARTMENT_ID=0` chia theo Quantity giữa đối tượng khớp — **không** cộng nguyên khoản vào mỗi phòng.
- Nguồn `DEPARTMENT_ID>0` chỉ vào đúng phòng.
- Assert tổng nguồn trước = sau (sai lệch làm tròn ≤ 1).
- `XFER_EXP_TO_154` vẫn bị loại / ném lỗi nếu đưa vào incurred.

### 1.2 ReceiptPricePrep
- Khóa giá: `(PRODUCT_ID, DEPARTMENT_ID)` + `INPUT_ID`.
- Nhiều UNIT_COST cùng PRODUCT mà receipt thiếu DEPT → lỗi (không chọn tùy ý).
- INPUT_ID trùng / Qty≤0 → lỗi; kiểm tra tổng Delta vs New−Old.

### 1.3 CostRunCompare
- Khóa `(PRODUCT_ID, DEPARTMENT_ID)`.
- Dòng chỉ CONFIRMED / chỉ PREVIEW / chênh BOTH — không mất dòng multi-dept.

### 1.4 Trạng thái Bước 4
- Confirm B4 → `COST_CALC=DONE` only.
- `AfterCostCalcConfirm`: **không** tự `RUN.STATUS=CONFIRMED` (cần B5/B6).
- Preview không DELETE/ghi đè snapshot CONFIRMED cũ.
- B-I và B2/B3 source không đổi trong fix này.

---

## 2. File chính

### C# (đã sửa)
- `Services/ProductionCost/Wip/CostSourceRules.cs`
- `Services/ProductionCost/Wip/ReceiptPricePrep.cs`
- `Services/ProductionCost/Wip/CostRunCompare.cs`
- `Services/ProductionCost/Wip/WipRecalcGuard.cs`
- `Services/ProductionCost/Wip/WipCostEngine.cs` (dùng Distribute)
- `Services/ProductionCost/Allocation/GkRunStatus.cs` (`AfterCostCalcConfirm`)
- `Models/WipCostEngineModels.cs` (DepartmentId / Presence)
- Tests: `ProductionCostWipEngineTests.cs`

### SQL bàn giao (skeleton — không bắt buộc DDL mới)
- `migrations/20261102_2c03_sp_wip.sql` — comment COST_CALC=DONE only
- Không có script DDL riêng cho fix này (chỉ DTO/C#/comment SP).

---

## 3. Build & unit test

```text
dotnet build → 0 Error(s)
dotnet test --filter FullyQualifiedName~ProductionCost
→ Passed: 62, Failed: 0
```

| Case | Kết quả |
|------|---------|
| Shared DEPT=0 cùng PRODUCT nhiều DEPT — tổng nguồn 1 lần | PASS |
| Compare CONFIRMED_ONLY / PREVIEW_ONLY / BOTH + multi-dept | PASS |
| Receipt bắt DEPT khi multi UNIT_COST; INPUT trùng; Qty≤0 | PASS |
| B4 không promote CONFIRMED; Preview giữ snapshot | PASS |
| Doc % / Qdd / XFER excluded / gate confirm | PASS |

---

## 4. Chờ nghiệm thu DEV

1. Body SQL `source_rebuild` / `wip_calculate` / `cost_confirm`.
2. EnableWrites trên DEV (không production).
3. Apply giá kho theo INPUT_ID (sau 2C-03).
4. Điều kiện RUN → CONFIRMED toàn kỳ khi B5/B6 đủ.

---

## 5. Dừng

Fix logic 2C-03 xong để duyệt trước giai đoạn tiếp theo.  
Không bật ghi · không SQL trên môi trường thật.
