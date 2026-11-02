# Nghiệm thu Giai đoạn 2C-03 — Tính dở dang và giá thành thành phẩm

Ngày: 2026-11-02 (final schema & reconciliation)  
Phạm vi: Engine WIP/GT, DDL khóa PRODUCT+DEPT, so sánh/reconcile phiếu nhập, script bàn giao.  
**Không** kết nối DB · **Không** chạy SQL · **Không** bật `EnableWrites` · **Không** cập nhật kho thật · **Không** production.

---

## 0. Phân biệt trạng thái triển khai

| Hạng mục | Trạng thái thật |
|----------|----------------|
| `WipCostEngine` + ngoại lệ SL_TP_SX / ID2720 | **Đã implement** + unit test |
| `CostSourceRules` — DEPT=0 một lần; cấm chia đều khi ΣQ=0; round bảo toàn tổng | **Đã implement** + unit test |
| `CostRunCompare` / `ReceiptPricePrep` / `ReceiptReconcile` | **Đã implement** + unit test |
| `GetComparisonLinesAsync` JOIN PRODUCT+DEPT+COMPANY | **Đã sửa** (SQL đọc) |
| DTO/Repo receipt có `DEPARTMENT_ID` | **Đã đồng bộ** |
| Schema gốc + 2B: `uk_pc_wip` + receipt.DEPT | **Đã cập nhật source** |
| Migration nâng cấp schema cũ | **Script bàn giao** (user tự chạy) |
| API Preview dry-run / Confirm gate | Preview dry-run; Confirm **skeleton gate** |
| SP `source_rebuild` / `wip_calculate` / `cost_confirm` | **Skeleton SIGNAL** |
| Apply giá kho thật | **Chưa** |

---

## 1. Schema & reconciliation (final fix)

### 1.1 `production_cost_wip`
- UNIQUE: `(RUN_ID, RUN_VERSION, PRODUCT_ID, DEPARTMENT_ID)`.
- Đồng bộ `01_production_cost_tables.sql`, `2b_01_tables.sql`.
- Migration: `20261102_2c03_ddl_wip_receipt_key.sql` — precheck trùng PRODUCT+DEPT trước ALTER.

### 1.2 `production_cost_receipt`
- Thêm `DEPARTMENT_ID` (không tự suy đoán khi thiếu).
- UNIQUE giữ `(RUN_ID, RUN_VERSION, INPUT_ID)`.
- `ProductionCostReceiptDto` + `GetReceiptsAsync` đọc `DEPARTMENT_ID`.

### 1.3 So sánh phiên
- Repo JOIN: `PRODUCT_ID + DEPARTMENT_ID + COMPANY_CD` (full outer).
- Engine compare: khóa PRODUCT+DEPT; CONFIRMED_ONLY / PREVIEW_ONLY / BOTH.

### 1.4 Reconcile giá nhập
- `ReceiptReconcile`: Σ QTY ↔ QUANTITY; Σ NEW_AMOUNT ↔ TOTAL_COST_PRICE theo PRODUCT+DEPT.
- Dung sai tiền = 1 (RoundDigit 0); không tự phân bổ khi lệch / đối tượng không khớp.

### 1.5 Nguồn dùng chung
- Σ Quantity = 0 → lỗi (không chia đều).
- Quantity âm → lỗi.
- Số lẻ: round từng dòng nguồn rồi chia; dòng cuối nhận phần dư — Σ sau = Σ trước.

### 1.6 B4 status (giữ)
- `COST_CALC=DONE` only; không tự `RUN=CONFIRMED`.

---

## 2. Migration DDL (user tự chạy nếu schema cũ)

| File | Khi nào |
|------|---------|
| `migrations/20261102_2c03_ddl_wip_receipt_key.sql` | Schema cũ: UNIQUE WIP thiếu DEPT hoặc receipt thiếu cột |
| Không cần | Cài mới từ `01` / `2b_01` bản repo hiện tại |

Thứ tự gợi ý DEV: precheck trong script → ALTER WIP UNIQUE → ADD receipt.DEPARTMENT_ID → `2c03_verify_readonly.sql`.

---

## 3. Build & unit test

```text
dotnet build → 0 Error(s)
dotnet test --filter FullyQualifiedName~ProductionCost
→ Passed: 65, Failed: 0
```

| Case | Kết quả |
|------|---------|
| Shared DEPT=0 / multi-dept / XFER | PASS |
| Q=0 không chia đều; Q&lt;0 lỗi; số lẻ bảo toàn tổng | PASS |
| Compare multi-dept presence | PASS |
| Receipt DEPT bắt buộc khi multi UNIT; reconcile QTY/AMOUNT | PASS |
| B4 không promote CONFIRMED | PASS |

---

## 4. Skeleton / chờ DEV

- Body SQL SP source/wip/confirm; EnableWrites; apply kho; điều kiện CONFIRMED toàn kỳ (B5/B6).

---

## 5. Dừng

Final fix 2C-03 xong để nghiệm thu và duyệt chuyển 2C-04.  
Không thực thi SQL · không bật ghi · không đụng production.
