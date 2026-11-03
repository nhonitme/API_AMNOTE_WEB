# Nghiệm thu Giai đoạn 2C-04 — VALUATION (final review trước 2C-05)

Ngày: 2026-11-02  
**Không** DB · **Không** SOURCE · **Không** EnableWrites · **Không** apply giá kho · **Không** post 632 · **Không** sửa B-I.

---

## 0. Quyết định (giữ nguyên)

1. Engine chung `InventoryOutPriceEngine` cho menu Kho và G-K.
2. Khóa nhóm Kho: `PRODUCT_CD + PRODUCT_NAME + STORE_CD + STORE_NAME + CHIT_TYPE`.
3. Bốn PP: `PERIOD_END_AVG` · `MOVING_AVG` · `FIFO` · `SPECIFIC`.
4. Không copy công thức sang Service / adapter độc lập.

---

## 1. Final review — đã xử lý

### 1.1 Progress job Kho thực tế

- `BuildOrderedGroups` tách nhóm trước khi tính.
- `ComputeByGroups` tính **từng nhóm**, gọi `onGroupComputed` **sau** `ComputeMethod` của nhóm đó.
- `CancellationToken` kiểm tra **giữa** các nhóm.
- `InventoryValuationService` ủy thác progress qua callback engine — không giả lập sau khi tính xong hết.

### 1.2 Metadata adapter G-K

- `ComputeByGroup(..., companyCd)` kiểm tra COMPANY_CD request / không trộn công ty.
- Trong cùng nhóm Kho: PRODUCT_ID và STORE_ID phải nhất quán; xung đột → lỗi (không lấy ID dòng đầu).
- Khóa nhóm Kho không đổi.

### 1.3 Audit persist (không đổi)

Menu valuation vẫn **chỉ tính** (không persist). Cập nhật ĐG xuất / 632 nằm luồng `apply_inventory_update_out_price` / `setInventoryCogs`. G-K Confirm persist = sau nghiệm thu.

---

## 2. Kiểm thử (báo cáo tách)

### Host build (output riêng — tránh DLL lock)

```text
dotnet build API_AMNOTE_WEB.csproj -o artifacts/host-2c04-check
→ 0 Error(s)  (warnings sẵn có / NU advisory — không lỗi biên dịch mới)
HOST_EXIT=0
```

### Unit test (chạy riêng sau host)

```text
dotnet test --filter "FullyQualifiedName~ProductionCost|FullyQualifiedName~InventoryOutPrice"
→ Passed: 76, Failed: 0
TEST_EXIT=0
```

Gồm: progress nhiều nhóm · cancel giữa nhóm · COMPANY/ID xung đột · parity 4 PP Kho↔G-K.

---

## 3. Dừng

Final review 2C-04 xong — chờ nghiệm thu lần cuối trước khi triển khai 2C-05.
