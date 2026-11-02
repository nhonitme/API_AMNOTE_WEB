# Giai đoạn 2A — Database G-K Giá thành giản đơn (hoàn thiện)

> **Trạng thái:** Thiết kế đã giải quyết R1–R3 · **Không BLOCKER** · Có GATE 2B · **Chưa thực thi SQL** · **Chưa sửa B-I**.

## Đọc trước khi duyệt

1. [`03_risk_resolution.md`](./03_risk_resolution.md) — NVL↔TP, chống trùng 154, tách G-K/B-I  
2. [`DESIGN.md`](./DESIGN.md) — ER + luồng 621→154→155→632  
3. [`01_production_cost_tables.sql`](./01_production_cost_tables.sql) — DDL  
4. [`02_sp_specifications.sql`](./02_sp_specifications.sql) — Đặc tả SP  

## Kết luận duyệt

| Hạng mục | Kết quả |
|----------|---------|
| BLOCKER thiết kế | **Không** |
| GATE trước khi code 2B trên DEV | `EXP_TO_154_ONLY` + exclusive COGS lock |
| Production execute | **Cấm** cho đến khi duyệt + chạy DEV/TEST |

## Bảng mới (14)

`period`, `run`, `step`, `object`, `alloc_pool`, `alloc_detail`, `nvl_basis`, `nvl_basis_line`, `nvl_orphan`, `source`, `wip`, `receipt`, `voucher_ref`, `history`.
