# Nghiệm thu Giai đoạn 2C-04 — VALUATION (giá nhập TP + Preview 632)

Ngày: 2026-11-02  
Phạm vi: Tiền đề B2–B4, Receipt reconcile Preview/ReadyToApply, bình quân xuất kho thuần, Preview 632, API gate OFF.  
**Không** DB · **Không** SOURCE · **Không** EnableWrites · **Không** cập nhật kho · **Không** production.

---

## 0. Implemented vs skeleton vs pending DEV

| Hạng mục | Trạng thái |
|----------|------------|
| `ValuationPrerequisite` (B2/B3/B4 DONE; cấm SUPERSEDED) | **Implemented** + test |
| `ReceiptReconcile` Preview (cảnh báo thiếu phiếu) / ReadyToApply (bắt buộc đủ) | **Implemented** + test |
| `ReceiptPricePrep` + plan old/new/delta + COMPANY_CD trên plan | **Implemented** |
| `ValuationAverageCost` PERIOD_END_AVG / MOVING_AVG (công thức InventoryValuation) | **Implemented** + test |
| `Cogs632PreviewRules` — không EXP; không trùng N632/C155 nguồn | **Implemented** + test |
| `ValuationApplyPlan` — atomic flag, rollback map, STABLE_KEY, IsApplied=false | **Implemented** + test |
| API `POST .../valuation/preview` | **DryRun** — không ghi DB |
| API `POST .../valuation/confirm` | **Skeleton + WriteGate OFF** |
| `sp_pc_valuation_preview/confirm` | **Skeleton SIGNAL** |
| Apply `INPUT_ID` kho thật / post 632 / tính xuất persist | **Pending DEV** |
| RUN → CONFIRMED toàn kỳ | **Chưa** (cần đủ bước; B5 chỉ VALUATION=DONE) |

DDL 2C-03 (vá kèm 2C-04): `20261102_2c03_ddl_wip_receipt_key.sql` — precheck ISDEL; skip DROP/ADD nếu UNIQUE đã mới.

---

## 1. Phạm vi nghiệp vụ đã khóa

1. Chỉ VALUATION khi COST_ALLOCATION / EXP_TO_154_ONLY / COST_CALC = DONE cùng RUN_ID+VERSION.
2. Giá nhập theo PRODUCT_ID + DEPARTMENT_ID + INPUT_ID; không nhầm COMPANY_CD.
3. Preview: thiếu phiếu → Warning; ReadyToApply: thiếu đối tượng bắt buộc (Q&gt;0) → Error.
4. Không tự điều chỉnh lệch tiền/SL; Q=0 không bắt buộc phiếu nhập.
5. Preview 632 từ bình quân sau đổi giá nhập — không cộng EXP_TO_154; không INSERT trùng N632/C155.
6. Không đổi nghiệp vụ B-I; B3 vẫn cấm downstream.

---

## 2. File chính

- `Services/ProductionCost/Valuation/*`
- `Services/ProductionCost/Wip/ReceiptReconcile.cs` (mở rộng mode)
- `Models/ValuationEngineModels.cs`
- `ProductionCostWriteService` / Controller / Interface
- `migrations/20261102_2c04_sp_valuation.sql`, `2c04_README.md`
- Tests: `ProductionCostValuationTests.cs`

---

## 3. Build & test

```text
dotnet build → 0 Error(s)
dotnet test --filter FullyQualifiedName~ProductionCost
→ Passed: 70, Failed: 0
```

---

## 4. Dừng

2C-04 source xong để duyệt trước 2C-05.  
Không thực thi SQL · không bật ghi · không cập nhật kho.
