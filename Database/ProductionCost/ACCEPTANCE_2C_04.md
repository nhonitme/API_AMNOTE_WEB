# Nghiệm thu Giai đoạn 2C-04 — VALUATION (điều chỉnh tái sử dụng engine Kho)

Ngày: 2026-11-02  
**Không** DB · **Không** SOURCE · **Không** EnableWrites · **Không** cập nhật kho · **Không** đổi menu Kho / B-I.

---

## 0. Quyết định nghiệp vụ (cuối cùng)

1. **Giữ nguyên** quy tắc tách nhóm `InventoryValuationService`:
   `PRODUCT_CD + PRODUCT_NAME + STORE_CD + STORE_NAME + CHIT_TYPE` (gồm `CHIT_TYPE`).
2. **Hủy** yêu cầu bắt buộc chuyển khóa nhóm sang `PRODUCT_ID + STORE_ID`.
3. **Không** xây engine tính giá xuất kho thứ hai. G-K Bước 5 dùng `InventoryOutPriceEngine` (logic tách từ menu Kho) qua adapter `ValuationAverageCost`.
4. Bốn phương pháp: `PERIOD_END_AVG` · `MOVING_AVG` · `FIFO` · `SPECIFIC` — cùng mã với menu `/inventory/calc-out-price`.
5. Không tự sửa công thức / nhóm / sắp xếp của bất kỳ phương pháp nào.

### Vai trò G-K

| Bước | Việc làm |
|------|----------|
| B4 COST_CALC | Tính & xác nhận giá thành TP |
| B5 VALUATION | Chuẩn bị cập nhật giá nhập theo INPUT_ID; đối chiếu SL/GT; **tái sử dụng** tính giá xuất kho; Preview ảnh hưởng 632; không tạo bút toán trùng |

---

## 1. Báo cáo thực trạng engine Kho (chỉ đọc — chưa bổ sung persist)

| Câu hỏi | Kết quả rà soát source |
|---------|-------------------------|
| Engine chỉ tính hay cập nhật dữ liệu? | **Chỉ tính toán in-memory.** `CalculateAsync` → `InventoryOutPriceEngine.ComputeByGroups` → trả `ProcessedGroups` / `MovementCount`. **Không** gọi repository UPDATE. |
| Bảng lưu kết quả giá xuất kho? | **Không có bảng kết quả valuation** trong luồng `api/inventory/valuation`. |
| Có cập nhật ĐG / TT chứng từ xuất? | **Không** trong job valuation. Cập nhật tay/hàng loạt nằm ở luồng riêng `InventoryPriceUpdate` → SP `apply_inventory_update_out_price` → `chit_inventory_output.UNIT_PRICE_CC` / `AMOUNT_CC`. |
| Giá vốn 632 cập nhật ở đâu? | (1) Phiếu xuất OT: `setInventoryCogs` / `InventoryVoucherRepository.Cogs` → `chitinfo` + `chitdetailinfo` gắn `CHIT_ID_COGS`. (2) Khi đổi giá xuất hàng loạt: `apply_inventory_update_out_price` cộng dồn lại `chitdetailinfo` / `chitinfo` COGS đã link. **G-K VALUATION_COGS (N632/C155) chưa persist trong 2C-04.** |

**Kết luận:** Menu tính giá xuất kho hiện là **compute + progress job**; persist đơn giá xuất / 632 thuộc luồng khác. Bổ sung ghi kho cho G-K = việc **sau nghiệm thu**, không làm trong báo cáo này.

---

## 2. Implemented vs skeleton

| Hạng mục | Trạng thái |
|----------|------------|
| `InventoryOutPriceEngine` — 4 PP, nhóm/sort như Kho | **Implemented** (dùng chung) |
| `InventoryValuationService` ủy thác engine chung (không đổi nghiệp vụ) | **Implemented** |
| `ValuationAverageCost` = adapter G-K (ApplyReceiptPrices + map kết quả) | **Implemented** |
| Preview 632 theo nhóm PRODUCT_CD+STORE+CHIT; không EXP | **Implemented** |
| STABLE_KEY + COMPANY ownership | **Implemented** |
| DryRun B2/B3/B4 = mô phỏng client | **Implemented** |
| Confirm DB + apply kho + post 632 | **Skeleton / WriteGate OFF** |

---

## 3. Build & test

```text
dotnet build (Tests + linked sources) → 0 Error(s)
dotnet test --filter FullyQualifiedName~ProductionCost
→ Passed: 72, Failed: 0
```

Test then chốt: parity adapter ↔ `InventoryOutPriceEngine` (4 PP); nhóm gồm CHIT_TYPE; ApplyReceiptPrices; Preview 632; Confirm bị WriteGate.

---

## 4. Dừng

Điều chỉnh 2C-04 theo quyết định tái sử dụng engine Kho — chờ duyệt trước 2C-05.
