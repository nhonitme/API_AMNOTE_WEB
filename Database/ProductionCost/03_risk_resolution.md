# Hoàn thiện 2A — Giải quyết rủi ro R1 / R2 / R3

> Cập nhật sau rà soát schema Web thực tế. **Chưa thực thi SQL. Chưa sửa B-I.**

---

## R1. Liên kết xuất NVL → thành phẩm (RESOLVED)

### Quan hệ đã xác nhận trên DB Web

```
chit_inventory_info (INVENTORY_ID, COMPANY_CD)
        │
        ├── chit_inventory_bom_finished
        │     FINISHED_LINE_ID, FINISHED_PRODUCT_ID, FINISHED_QUANTITY
        │
        ├── chit_inventory_bom_material
        │     FINISHED_LINE_ID → finished
        │     MATERIAL_PRODUCT_ID
        │     OUTPUT_ID → chit_inventory_output
        │     CONFIRMED_QUANTITY
        │
        └── chit_inventory_output (OUTPUT_ID, PRODUCT_ID=NVL, AMOUNT_CC, CHITDETAIL_ID)
                  │
                  └── chitdetailinfo (DEBIT LIKE '621%', CREDIT LIKE '152%', AMOUNT)
```

Join **chỉ** bằng `COMPANY_CD` + `*_ID` (`FINISHED_PRODUCT_ID`, `FINISHED_LINE_ID`, `OUTPUT_ID`, `CHITDETAIL_ID`). Không dùng `PRODUCT_CD` / `FINISHED_CD`.

### Công thức tiêu thức allocate_type = 0

Với mỗi `FINISHED_PRODUCT_ID`:

\[
\text{AMOUNT\_QUANTITY}
=
\sum \text{AMOUNT}(\text{chitdetail N621/C152})
\quad\text{qua}\quad
\text{bom\_material.OUTPUT\_ID}
\]

Pseudo SQL (đặc tả SP `sp_pc_alloc_load_basis`):

```sql
SELECT
  f.FINISHED_PRODUCT_ID AS PRODUCT_ID,
  SUM(d.AMOUNT) AS AMOUNT_QUANTITY
FROM chit_inventory_bom_finished f
INNER JOIN chit_inventory_bom_material m
  ON m.COMPANY_CD = f.COMPANY_CD
 AND m.FINISHED_LINE_ID = f.FINISHED_LINE_ID
 AND m.ISDEL = '0'
INNER JOIN chit_inventory_output o
  ON o.COMPANY_CD = m.COMPANY_CD
 AND o.OUTPUT_ID = m.OUTPUT_ID
 AND o.ISDEL = '0'
INNER JOIN chitdetailinfo d
  ON d.COMPANY_CD = o.COMPANY_CD
 AND d.CHITDETAIL_ID = o.CHITDETAIL_ID
 AND d.ISDEL = '0'
INNER JOIN chitinfo c
  ON c.COMPANY_CD = d.COMPANY_CD
 AND c.CHIT_ID = d.CHIT_ID
 AND c.ISDEL = '0'
WHERE f.COMPANY_CD = ?
  AND f.ISDEL = '0'
  AND c.CHIT_YMD BETWEEN ? AND ?
  AND TRIM(d.DEBIT)  LIKE '621%'
  AND TRIM(d.CREDIT) LIKE '152%'
  AND IFNULL(m.OUTPUT_ID, 0) > 0
GROUP BY f.FINISHED_PRODUCT_ID;
```

### Trường hợp đặc biệt

| Tình huống | Xử lý thiết kế |
|------------|----------------|
| 1 phiếu IO, nhiều TP | Mỗi material gắn `FINISHED_LINE_ID` riêng → tiền theo `OUTPUT_ID` về đúng TP |
| 1 `OUTPUT_ID` gắn nhiều `FINISHED_LINE_ID` | **Tách theo tỷ lệ** `CONFIRMED_QUANTITY / ΣQ` trên các dòng cùng `OUTPUT_ID`; nếu ΣQ=0 → loại khỏi basis + warning |
| Xuất NVL không có BOM / `OUTPUT_ID` NULL | **Không vào** `AMOUNT_QUANTITY`; ghi `production_cost_nvl_orphan` (warning); **cấm** gán theo mã CD hoặc BOM lý thuyết `product_material_info` |
| Material `CONFIRMED_QUANTITY=0` nhưng có output | Vẫn lấy tiền CT nếu `OUTPUT_ID` hợp lệ (tiền theo chứng từ, không theo SL BOM) |

### Bảng hỗ trợ

- `production_cost_nvl_basis` — tổng theo `PRODUCT_ID` (tiêu thức)
- `production_cost_nvl_basis_line` — audit từng `OUTPUT_ID` / `FINISHED_LINE_ID` / số tiền (sau split nếu có)

**Không BLOCKER** cho thiết kế. Dữ liệu IO thiếu BOM là cảnh báo nghiệp vụ, không fallback sai.

---

## R2. Chống cộng trùng 154 (RESOLVED)

### Ba lớp chi phí — tách tuyệt đối

| Lớp | Mã nguồn (`SOURCE_KIND`) | Nguồn đọc | Dùng ở Bước 4? |
|-----|--------------------------|-----------|----------------|
| A. Chi phí nguồn kỳ | `SRC_621`, `SRC_6212` | CT nghiệp vụ Nợ 621%/6212 — **loại** CT `INPUT_TYPE='LOCK'` / voucher G-K EXP | Có |
| B. Phân bổ 622/627 | `SRC_622_ALLOC`, `SRC_627_ALLOC` | `production_cost_alloc_detail` version CONFIRMED | Có |
| C. 154 phát sinh thẳng | `SRC_154_DIRECT` | Nợ 154%, Có **không** thuộc 621/622/623/627, và không phải CT LOCK EXP | Có |
| D. Bút toán kết chuyển B3 | `XFER_EXP_TO_154` | CT LOCK `EXP_TO_154_ONLY` (Nợ 154 / Có 621..) | **Không** cộng vào TOTAL_INCURRED |

### Công thức Bước 4 (chuẩn)

```
TOTAL_AMOUNT_INCURRED
  = SRC_621 + SRC_6212 + SRC_622_ALLOC + SRC_627_ALLOC + SRC_154_DIRECT

CLOSING_154  = f(BEFORE_154, SRC_621, % hoặc Qdd)   -- theo CONG_THUC_TINH_DO_DANG
TOTAL_COST   = ROUND(BEFORE_154 + TOTAL_AMOUNT_INCURRED - CLOSING_154, 0)
```

`BEFORE_154` = dở dang đầu kỳ (từ `production_cost_wip` / opening), **không** lấy số dư 154 cuối kỳ đã gồm kết chuyển.

### Tính lại nhiều lần

- Preview: ghi đè bản DRAFT theo `RUN_ID` + `RUN_VERSION` hiện tại — không INSERT thêm nguồn.
- Confirm: tăng `RUN_VERSION` hoặc đóng băng version; `STABLE_KEY` chứng từ EXP → UPDATE cùng `CHIT_ID`.
- Không đọc lại PS Nợ 154 kỳ (đã gồm EXP) để cộng vào incurred.

Bảng `production_cost_source` lưu các dòng A/B/C đã chốt cho version — SP wip **chỉ** đọc bảng này + opening, không aggregate lại GL mỗi lần.

**Không BLOCKER.**

---

## R3. Tách G-K / B-I (RESOLVED ở mức thiết kế; GATE 2B cho code B-I)

### Map bước

| UI G-K | Engine | PeriodLock |
|--------|--------|------------|
| B1 Khóa TSCĐ/CPTT | **B-I** | `FA_PREPAID_LOCK` |
| B2 Phân bổ 622/627 | G-K | không khóa B-I |
| B3 Kết chuyển chi phí | **G-K rule** trên B-I step | `COGS_SUMMARY` + rule **`EXP_TO_154_ONLY`** |
| B4 Tính GT / cập nhật 155 | G-K | không = B-I `154_TO_155` (đơn giá qua INPUT_ID) |
| B5 Giá xuất | G-K (+ kho) | — |
| B6 Lãi/lỗ | **B-I** | `PROFIT_LOSS_REPORT` |

### Rule COGS

| Engine | Rule được phép |
|--------|----------------|
| B-I (kỳ **không** dùng G-K) | `154_TO_155` \| `154_TO_155_TO_632` \| `154_TO_632` (giữ nguyên — mỗi rule vẫn gồm EXP_TO_154 rồi bước tiếp) |
| G-K | **`EXP_TO_154_ONLY`** — chỉ 621/622/623/627 → 154; **không** 154→155/632 tự động |

### Exclusive lock cùng `COMPANY_CD + PERIOD_YM`

Trên `production_cost_period`:

- `COST_ENGINE` = `NONE` \| `GK` \| `BI`
- `COGS_RULE` = rule đã/ sẽ áp dụng cho bước COGS

**Luật chặn (API/SP, chưa sửa B-I code ở 2A):**

1. Bắt đầu G-K All-in-One kỳ → set `COST_ENGINE='GK'`, `COGS_RULE='EXP_TO_154_ONLY'`.
2. Nếu `COST_ENGINE='GK'` → **cấm** job B-I `COGS_SUMMARY` với 3 rule đầy đủ.
3. Nếu `acc_period_lock_step.COGS_SUMMARY=DONE` và `COST_ENGINE='BI'` (hoặc rule ∈ 3 rule B-I) → **cấm** G-K Bước 3.
4. G-K B1/B6 chỉ gọi engine B-I hiện có; mirror STATUS vào `production_cost_step`.

### GATE_2B (không phải BLOCKER thiết kế)

- B-I SP hiện **chưa** có `p_RULE_CODE = 'EXP_TO_154_ONLY'`.  
  2B trên DEV: thêm nhánh rule (chỉ dừng sau EXP_TO_154), **không đổi** hành vi 3 rule cũ.
- Hook exclusive check vào PeriodLock start job (DEV) — ngoài phạm vi “không sửa B-I” của 2A; 2A chỉ chốt hợp đồng dữ liệu.

**Không BLOCKER thiết kế 2A** — có GATE triển khai 2B rõ ràng.

---

## R4. SQL / FE-04 (đã rà)

| Hạng mục | Kết luận |
|----------|----------|
| PK | AUTO_INCREMENT `*_ID` trên mọi bảng mới |
| UNIQUE công ty/kỳ | `uk_pc_period (COMPANY_CD, PERIOD_YM)` |
| UNIQUE ghi đè CT | `uk_pc_voucher (COMPANY_CD, STABLE_KEY)` |
| UNIQUE detail version | `(RUN_ID, RUN_VERSION, …)` trên pool/detail/wip/receipt/source |
| FK vật lý master | Hoãn (legacy dump); JOIN logic bắt buộc `COMPANY_CD+ID` |
| MySQL 5.6/8.0 | InnoDB, utf8, LONGTEXT, không JSON typed / CHECK / CTE bắt buộc |
| RUN_VERSION | Tăng khi CONFIRM ghi đè; history giữ before/after |
| Snapshot full DB | Không yêu cầu |

---

## Luồng chứng từ thực tế (end-to-end)

```
[Nguồn kỳ]
  Nợ 621 / Có 152  ──BOM──▶ AMOUNT_QUANTITY theo FINISHED_PRODUCT_ID
  Nợ 622,627 (dùng chung) ──▶ phân bổ G-K B2 ──▶ SRC_622/627_ALLOC
  Nợ 154 trực tiếp (không từ 621..) ──▶ SRC_154_DIRECT

[G-K B3 / PeriodLock COGS_SUMMARY + EXP_TO_154_ONLY]
  Nợ 154 / Có 621,622,623,627   ← XFER (không vào incurred B4)

[G-K B4]
  TOTAL_COST_PRICE theo TP
  UNIT_COST = TOTAL_COST / Q (hoặc Q_ht)
  Cập nhật chit_inventory_input (INPUT_ID) → giá 155

[G-K B5 / xuất]
  Giá vốn 632 từ đơn giá BQGQ (kho) — không dùng B-I 154→632 trên kỳ G-K

[G-K B6 = B-I]
  PROFIT_LOSS_REPORT (632→911 …)
```

---

## Tái sử dụng vs tạo mới

### Tái sử dụng

`acc_period_lock_*`, `chitinfo/detail/ext`, `chit_inventory_*`, BOM finished/material, `product_info`, `department_info`, `apply_inventory_update_in_price`, engine FA_PREPAID + PROFIT_LOSS B-I.

### Tạo mới (DDL 2A)

`production_cost_period` (+ COST_ENGINE, COGS_RULE), `run`, `step`, `object`, `alloc_pool`, `alloc_detail`, `nvl_basis`, `nvl_basis_line`, `nvl_orphan`, `source`, `wip`, `receipt`, `voucher_ref`, `history`.

---

## BLOCKER?

| Mã | Mức | Nội dung |
|----|-----|----------|
| — | — | **Không có BLOCKER thiết kế** ngăn duyệt 2A |
| G2B-01 | GATE 2B | Implement `EXP_TO_154_ONLY` trong PeriodLock DEV |
| G2B-02 | GATE 2B | Exclusive check B-I↔G-K trên start COGS |
| WARN-01 | Vận hành | IO thiếu BOM → thiếu tiêu thức NVL; cần quy trình nhập BOM |
