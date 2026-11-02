# Nghiệm thu Giai đoạn 2C-04 — VALUATION (final fix nhóm định giá)

Ngày: 2026-11-02 (final fix trước 2C-05)  
**Không** DB · **Không** SOURCE · **Không** EnableWrites · **Không** cập nhật kho.

---

## 0. Implemented vs skeleton vs pending DEV

| Hạng mục | Trạng thái |
|----------|------------|
| `ValuationAverageCost.ComputeByGroup` — nhóm PRODUCT+STORE+CHIT_TYPE | **Implemented** + test |
| Sort MOVING_AVG = InventoryValuation (TXN_YMD→INVENTORY_ID→CHITDETAIL_ID) | **Implemented** + test |
| Tồn âm / xuất vượt tồn → `IsValid=false` (không âm thầm OK) | **Implemented** + test |
| `ApplyReceiptPrices` — COMPANY/PRODUCT/QTY; chặn INPUT orphan/trùng | **Implemented** + test |
| COGS 632 theo nhóm; OutputUnitCost ≠ EndingUnitCost (MOVING) | **Implemented** + test |
| STABLE_KEY + COMPANY_CD ownership cho VALUATION_COGS | **Implemented** + test |
| DryRun: B2/B3/B4 = mô phỏng client (`StepStatusesAreClientSimulated`) | **Implemented** |
| Confirm: bắt buộc đọc lại DB (skeleton message) | **Skeleton gate** |
| Apply kho / post 632 / SP body | **Pending DEV** |

DDL 2C-03 (đã vá trước đó): `20261102_2c03_ddl_wip_receipt_key.sql` — không đổi thêm trong final fix này.

---

## 1. Fix logic (final)

1. **Không gộp** nhiều sản phẩm/kho thành một kết quả ProductId=0 — dùng `ComputeByGroup`.
2. Khóa nhóm khớp InventoryValuation: Product (+Cd), Store (+Cd/Id), ChitType; CompanyCd.
3. MOVING_AVG bắt buộc TXN_YMD; sắp xếp nhất quán engine tồn kho.
4. ApplyReceiptPrices chỉ áp INPUT thuộc COMPANY + PRODUCT + qty khớp; receipt không có trong movements → lỗi.
5. Preview 632: `OutputAverageUnitCost` vs `EndingAverageUnitCost`; không EXP; không N632/C155 nguồn.
6. Confirm sau này: đọc B2/B3/B4 từ DB — không tin DryRun.

---

## 2. Build & test

```text
dotnet build → 0 Error(s), 3 Warning(s) (nullability cũ — không mới)
dotnet test --filter FullyQualifiedName~ProductionCost
→ Passed: 71, Failed: 0
```

Test bổ sung final fix: nhiều SP/kho, MOVING_AVG sort + tồn thiếu, INPUT_ID sai/orphan, COGS 632 theo nhóm, DryRun mô phỏng + Confirm bị WriteGate.

---

## 3. Dừng

Final fix 2C-04 xong — chờ duyệt trước khi triển khai 2C-05.
