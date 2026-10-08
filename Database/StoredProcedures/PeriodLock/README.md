# Period Lock — Stored Procedures

> **Đặc tả đầy đủ (nghiệp vụ, schema, SP, C#, checklist):** xem [`SPEC.md`](./SPEC.md)

Khóa sổ kỳ kế toán gồm 3 bước, mỗi bước chạy 3 sub-step: **validate → calculate → create_voucher**.

| Step | Code | Thư mục |
|------|------|---------|
| 1 | `FA_PREPAID_LOCK` | `FaPrepaid/` |
| 2 | `COGS_SUMMARY` | `CogsSummary/` |
| 3 | `PROFIT_LOSS_REPORT` | `ProfitLoss/` |

Helper dùng chung: `Common/01_helpers.sql`, `Common/02_transfer_helpers.sql` (xóa CT LOCK, snapshot `closingmonth_detail`, resolve TK hạch toán, tạo CT theo phòng ban).

## Cài đặt

Trên DB công ty (`am_web_xxx`), chạy theo thứ tự trong `00_install_all.sql` hoặc từng file:

1. `FaPrepaid/01_helpers.sql` — `last_day`, `next_chit_no`, rollback
2. `FaPrepaid/02` … `05` — TSCĐ / CP trả trước
3. `Common/01_helpers.sql` — **phụ thuộc** bước 1 (gọi `sp_period_lock_fa_prepaid_last_day`)
4. `Common/02_transfer_helpers.sql` — resolve TK, tạo CT LOCK theo nhóm kết chuyển
5. `CogsSummary/01_sp_period_lock_cogs.sql`
6. `ProfitLoss/01_sp_period_lock_pl.sql`

## SP chính

### COGS (`COGS_SUMMARY`)

- `sp_period_lock_cogs_validate` — kiểm tra bước 1 DONE, rule hợp lệ, resolve TK đích (154/155/632)
- `sp_period_lock_cogs_calculate` — tính dòng kết chuyển theo PS Nợ + phòng ban
- `sp_period_lock_cogs_create_voucher` — tạo **nhiều CT LOCK** (621→154, rồi theo rule)
- `sp_period_lock_cogs_unlock` — xóa CT, mở step

Rule: `154_TO_155` | `154_TO_155_TO_632` | `154_TO_632`. PS Nợ từ TK `621/622/623/627%` (`ISABLEINPUT=1`). TK đích resolve `LIKE '154%'`…

### P&amp;L (`PROFIT_LOSS_REPORT`)

- `sp_period_lock_pl_validate` / `_calculate` / `_create_voucher`
- `_create_voucher` tạo **nhiều CT LOCK** theo từng cặp kết chuyển + phòng ban; gọi `sp_period_lock_closingmonth_snapshot` sau khi tạo CT
- `sp_period_lock_pl_unlock` — xóa `closingmonth_detail` + CT, mở step

Chi phí: PS Nợ. Doanh thu (511/515/711): PS Có. `8212`: net PS. `911→4212`: net sau kết chuyển trong job.

## Unlock

Thứ tự mở: bước 3 → 2 → 1. CT LOCK bị **hard DELETE** theo `LOCK_STEP_CODE`.
