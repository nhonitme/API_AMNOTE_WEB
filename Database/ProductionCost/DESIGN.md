# Thiết kế Database G-K — Giai đoạn 2A (bản hoàn thiện để duyệt)

> **Trạng thái:** Thiết kế hoàn thiện R1–R3 · **Không BLOCKER** · Có **GATE 2B** · Chưa EXECUTE SQL · Chưa sửa B-I.

Chi tiết giải quyết rủi ro: [`03_risk_resolution.md`](./03_risk_resolution.md).

## 1. Tái sử dụng vs tạo mới

### Tái sử dụng (DB hiện có)

| Thành phần | Vai trò |
|------------|---------|
| `acc_period_lock_month/step/task` | Khóa chung; B1/B6 G-K = B-I; B3 G-K = `COGS_SUMMARY` + `EXP_TO_154_ONLY` |
| `chitinfo` (+ `LOCK_STEP_CODE`, `INPUT_TYPE`) | CT khóa / EXP |
| `chitdetailinfo` + `_ext.DEPARTMENT_ID` | PS 621/622/627/154 |
| `chit_inventory_bom_finished/material` | Gắn NVL → `FINISHED_PRODUCT_ID` |
| `chit_inventory_output` (`OUTPUT_ID`) | Tiền xuất NVL |
| `chit_inventory_input` (`INPUT_ID`) | SL/giá NK TP (155) |
| `product_info` / `department_info` | `PRODUCT_ID` / `DEPARTMENT_ID` |
| `apply_inventory_update_in_price` | Cập nhật giá NK |
| PeriodLock FA / PL SP | Engine bước 1 và 6 |

### Tạo mới (`production_cost_*`)

`period` (+ `COST_ENGINE`, `COGS_RULE`), `run`, `step`, `object`, `alloc_pool`, `alloc_detail`, `nvl_basis`, `nvl_basis_line`, `nvl_orphan`, `source`, `wip`, `receipt`, `voucher_ref`, `history`.

## 2. Sơ đồ quan hệ (logic)

```
acc_period_lock_step ◀── mirror / exclusive ──▶ production_cost_period
                                                      │ CURRENT_RUN_ID
                                                      ▼
                                              production_cost_run
                   ┌──────────────┬─────────────┼──────────────┬────────────┐
                   ▼              ▼             ▼              ▼            ▼
                 step          object      alloc_pool    nvl_basis     source
                   │                            │             │            │
                   │                            ▼             ▼            ▼
                   │                      alloc_detail   nvl_basis_line   wip
                   │                            │             │            │
                   │                            │        nvl_orphan     receipt → INPUT_ID
                   └──────── voucher_ref (STABLE_KEY → CHIT_ID) ───────────┘
                                      history (FE-04)
```

## 3. Luồng 621/622/627 → 154 → 155 → 632

```
Nợ 621/C152 ──(BOM+OUTPUT_ID)──▶ AMOUNT_QUANTITY theo FINISHED_PRODUCT_ID
Nợ 622/627  ──(G-K B2 phân bổ)──▶ SRC_622/627_ALLOC theo PRODUCT_ID
Nợ 154 trực tiếp                ──▶ SRC_154_DIRECT

        │
        ▼ G-K B3 = PeriodLock COGS_SUMMARY + EXP_TO_154_ONLY
Nợ 154 / Có 621..627   (XFER — KHÔNG cộng vào incurred B4)

        │
        ▼ G-K B4 đọc production_cost_source + opening
TOTAL_COST_PRICE / UNIT_COST
Cập nhật giá chit_inventory_input (155) theo INPUT_ID

        │
        ▼ G-K B5 (xuất kho)
Giá vốn 632 theo BQGQ đơn giá NK — không dùng B-I 154→632 trên kỳ G-K

        │
        ▼ G-K B6 = B-I PROFIT_LOSS_REPORT
632 → 911 …
```

## 4. FE-04 ghi đè

- `RUN_VERSION` tăng khi CONFIRM.
- `UNIQUE (COMPANY_CD, STABLE_KEY)` trên `voucher_ref`.
- Child tables UNIQUE theo `(RUN_ID, RUN_VERSION, …)`.
- Mở lại: giữ số CONFIRMED; preview không đổi giá chính thức; confirm ghi đè.

## 5. MySQL 5.6 / 8.0

InnoDB, utf8, LONGTEXT, không JSON typed / CHECK / CTE bắt buộc trong đặc tả SP.

## 6. BLOCKER / GATE

| Mã | Loại | Nội dung |
|----|------|----------|
| — | BLOCKER | **Không có** |
| G2B-01 | GATE 2B | Thêm rule `EXP_TO_154_ONLY` vào PeriodLock DEV |
| G2B-02 | GATE 2B | Exclusive check GK↔BI khi start COGS |
| WARN-01 | Vận hành | IO thiếu BOM → orphan NVL |

## 7. File DDL

- `01_production_cost_tables.sql` — toàn bộ bảng
- `02_sp_specifications.sql` — đặc tả SP (chưa body)
- `00_install_all.sql` — thứ tự cài khi được duyệt DEV
- `03_risk_resolution.md` — chi tiết R1–R3
