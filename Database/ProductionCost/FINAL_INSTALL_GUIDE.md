# G-K — thứ tự cài đặt schema

Không chạy file này trên database. Đây là thứ tự để người quản trị tự SOURCE sau khi đọc script.

Áp dụng cho database công ty mới và database đã có G-K 2B → 2C-06F. Không DROP bảng `production_cost_*` đã có dữ liệu. Không bật `ProductionCost:EnableWrites` trong bước này.

## 1. Database mới

Chạy lần lượt, dừng nếu một script báo lỗi:

1. `migrations/20261102_2b_00_preflight_dev.sql` — chỉ đọc, xem cảnh báo schema kho/kế toán.
2. `migrations/20261102_2b_01_tables.sql`
3. `migrations/20261102_2b_02_sp_engine.sql`
4. `migrations/20261102_2b_03_sp_nvl_basis.sql`
5. `migrations/20261102_2b_04_sp_period_lock_cogs.sql` — cùng bộ PeriodLock COGS hiện có của B-I; rule G-K là `EXP_TO_154_ONLY`.
6. `migrations/20261102_2c02_ddl_alloc_detail_key.sql` nếu file tồn tại trên máy (khóa detail).
7. `migrations/20261102_2c02_sp_alloc.sql`
8. `migrations/20261102_2c03_ddl_wip_receipt_key.sql` nếu có.
9. `migrations/20261102_2c03_sp_wip.sql` — chỉ khi procedure WIP còn được vận hành; tính giá thành production nằm ở `WipCostEngine` C#.
10. `migrations/20261103_2c06a_ddl_history_operation.sql`
11. `migrations/20261103_2c06b_sp_alloc_confirm_no_history.sql` — bản confirm B2 không tự ghi history (C# giữ history).
12. `migrations/20261103_gk_final_price_audit.sql`

Không SOURCE `20261103_2c06e_sp_valuation_input_id.sql` sau file final. File 2C-06E là skeleton SIGNAL. File final DROP hai procedure đó. Nếu lỡ chạy 2C-06E sau final, chạy lại `20261103_gk_final_price_audit.sql`.

## 2. Database đã có 2B → 2C-06F

Chỉ chạy phần còn thiếu:

1. `migrations/20261103_2c06a_ddl_history_operation.sql` nếu chưa có `OPERATION_ID`, `STABLE_KEY`, `CONTENT_FINGERPRINT` và unique history.
2. `migrations/20261103_2c06b_sp_alloc_confirm_no_history.sql` nếu `sp_pc_alloc_confirm` vẫn INSERT history.
3. `migrations/20261103_gk_final_price_audit.sql` — có guard DEV/TEST của 2B ở đầu file. Sai môi trường thì dừng trước DROP/CREATE. Script chỉ DROP `sp_pc_valuation_apply_input_price` và `sp_pc_valuation_confirm`.

Không tạo lại bảng kỳ/RUN. Không xóa snapshot `RUN_VERSION` cũ.

## 3. Kiểm tra

`migrations/20261103_gk_final_verify_readonly.sql`

Đủ bảng đọc 2C-01, có `production_cost_price_audit`, còn `sp_pc_alloc_load_pool`, `sp_pc_nvl_basis_build`, `sp_pc_alloc_confirm`. Không còn procedure valuation skeleton.

## 4. Trước khi cho ghi

- `ProductionCost:EnableWrites` giữ `false` cho đến khi nghiệm thu trên DB DEV.
- Cờ này bị tắt thêm khi host là Production (`Program.cs`).
- Confirm B5 thiếu bảng audit sẽ lỗi và rollback, không cập nhật kho.
- REOPEN khi `FA_PREPAID_LOCK` hoặc `PROFIT_LOSS_REPORT` đang DONE trên PeriodLock bị chặn. Mở hai bước đó bằng chức năng khóa sổ hiện có, trong cùng giao dịch G-K không hoàn nguyên được khấu hao/lãi lỗ đã khóa.

## 5. Rollback

Không có script xóa dữ liệu kỳ đã tính. Nếu cần gỡ riêng bảng audit trên DEV trống:

```sql
DROP TABLE IF EXISTS production_cost_price_audit;
```

Chỉ làm khi bảng chưa có dòng của kỳ thật.
