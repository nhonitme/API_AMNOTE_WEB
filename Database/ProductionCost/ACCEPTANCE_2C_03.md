# Nghiệm thu Giai đoạn 2C-03 — Tính dở dang và giá thành thành phẩm

Ngày: 2026-11-02  
Phạm vi: Engine WIP/GT thuần C#, Preview dry-run, so sánh phiên, chuẩn bị giá INPUT_ID, script SQL skeleton.  
**Không** kết nối DB · **Không** chạy SQL · **Không** bật `EnableWrites` · **Không** cập nhật giá nhập kho thật · **Không** production.

---

## 0. Phân biệt trạng thái triển khai

| Hạng mục | Trạng thái thật |
|----------|----------------|
| `WipCostEngine` (PERCENT_COMPLETED, CLOSING_WIP_QUANTITY) | **Đã implement** + unit test |
| Ngoại lệ `incomplete_cost_SL_TP_SX` / `incomplete_ValueChange_Calculated_ID2720` | **Đã implement** + unit test |
| `CostSourceRules` — SRC_* incurred, **không** cộng `XFER_EXP_TO_154` | **Đã implement** + unit test |
| `CostRunCompare` — so sánh CONFIRMED vs PREVIEW | **Đã implement** + unit test |
| `ReceiptPricePrep` theo `INPUT_ID` (`IsApplied=false`) | **Đã implement** + unit test |
| `WipRecalcGuard` — bảo toàn version CONFIRMED | **Đã implement** + unit test |
| API `POST .../cost-calc/preview` | **DryRun** — `Persisted=false`, không ghi kho |
| API `POST .../cost-calc/confirm` | **Chặn gate** — skeleton |
| `sp_pc_source_rebuild` / `sp_pc_wip_calculate` / `sp_pc_cost_confirm` | **Skeleton SIGNAL** — chờ DEV |
| Cập nhật giá nhập kho thật | **Chưa** — ngoài 2C-03 |

**Không báo cáo** Confirm DB / apply inventory là đã vận hành.

---

## 1. Tiền đề 2C-02 (đã chỉnh trước khi vào 2C-03)

- `sp_pc_alloc_load_pool`: chặn DELETE tuyệt đối khi `RUN.STATUS ∈ {ALLOC_CONFIRMED, CONFIRMED}` — không phụ thuộc STEP.
- `Database/StoredProcedures/PeriodLock/CogsSummary/01_sp_period_lock_cogs.sql` đồng bộ migration `2b_04` (B3 nhận `ALLOC_CONFIRMED`, B-I nguyên).
- README 2C-02: DDL UNIQUE **chỉ khi** schema cũ thiếu cột/UNIQUE.

---

## 2. File chính 2C-03

### C#
- `Services/ProductionCost/Wip/WipCostEngine.cs`
- `Services/ProductionCost/Wip/CostSourceRules.cs`
- `Services/ProductionCost/Wip/WipRecalcGuard.cs`
- `Services/ProductionCost/Wip/CostRunCompare.cs`
- `Services/ProductionCost/Wip/ReceiptPricePrep.cs`
- `Models/WipCostEngineModels.cs`
- `ProductionCostWriteService` — PreviewCostCalc / ConfirmCostCalc (gate)
- `ProductionCostController` — `cost-calc/preview|confirm`
- Tests: `ProductionCostWipEngineTests.cs`

### SQL bàn giao
- `migrations/20261102_2c03_sp_wip.sql`
- `migrations/20261102_2c03_README.md`
- `migrations/20261102_2c03_verify_readonly.sql`

---

## 3. Công thức đã khóa (engine)

```
TOTAL_AMOUNT_INCURRED = SRC_621 + SRC_6212 + SRC_622_ALLOC + SRC_627_ALLOC + SRC_154_DIRECT
  (không gồm XFER_EXP_TO_154)

PERCENT_COMPLETED:  CLOSING = (BEFORE_154 + 621) × (100 − %) / 100
CLOSING_WIP_QUANTITY: CLOSING = (BEFORE_154 + 621) / (Q + Qdd) × Qdd

STANDARD: TOTAL_COST = ROUND(BEFORE_154 + INCURRED − CLOSING, 0)
UNIT_COST = TOTAL_COST / QUANTITY  (hoặc Q_HT ở chế độ đặc biệt)
```

---

## 4. Build & unit test

```text
dotnet build API_AMNOTE_WEB.csproj -o _build_2c03_out
→ Build succeeded. 0 Error(s)

dotnet test --filter FullyQualifiedName~ProductionCost
→ Passed: 59, Failed: 0
```

Checklist:
| Case | Kết quả |
|------|---------|
| Doc % / Qdd / tương đương | PASS |
| Không cộng trùng EXP/XFER | PASS |
| SL_TP_SX + ID2720 | PASS |
| Guard version CONFIRMED | PASS |
| Compare + Receipt prep IsApplied=false | PASS |
| Confirm cost-calc bị gate | PASS |

---

## 5. Chờ nghiệm thu DEV

1. SOURCE skeleton `2c03_sp_wip.sql` (sau 2B + 2C-02) — chỉ kiểm tra tồn tại SP.
2. Body SQL source_rebuild / wip_calculate / cost_confirm.
3. Bật `EnableWrites` trên DEV không production.
4. Apply giá nhập kho theo INPUT_ID (giai đoạn sau).

---

## 6. Dừng

2C-03 source xong để duyệt. Không tự chuyển giai đoạn tiếp · không bật ghi · không SQL trên môi trường thật.
