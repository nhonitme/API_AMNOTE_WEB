-- =============================================================================
-- Đặc tả Stored Procedure G-K (Giai đoạn 2A)
-- Chỉ mô tả chữ ký + hành vi. KHÔNG triển khai body / KHÔNG CREATE PROCEDURE thật.
-- MySQL 5.6 / 8.0: tránh CTE bắt buộc, tránh JSON functions 8.0-only trong đặc tả.
-- =============================================================================

/*
================================================================================
A. NGUỒN DỮ LIỆU TÁI SỬ DỤNG (không tạo bảng mới)
================================================================================
- chitdetailinfo + chitdetailinfo_ext     : phát sinh 621/622/627/154; DEPARTMENT_ID
- chit_inventory_input                    : SL TP nhập (N155/C154) theo PRODUCT_ID
- chit_inventory_output + BOM finished    : xuất NVL gắn FINISHED_PRODUCT_ID (N621/C152)
- product_info / department_info          : danh mục theo PRODUCT_ID / DEPARTMENT_ID
- acc_period_lock_month / _step / _task   : khóa sổ B-I (FA_PREPAID_LOCK, COGS_SUMMARY,
                                            PROFIT_LOSS_REPORT) — KHÔNG clone hệ khóa
- chitinfo.LOCK_STEP_CODE                 : đánh dấu chứng từ thuộc bước B-I nếu cần
- apply_inventory_update_in_price         : cập nhật giá NK (gọi khi confirm Bước 4)

JOIN bắt buộc:
  ON x.COMPANY_CD = y.COMPANY_CD AND x.<ENTITY>_ID = y.<ENTITY>_ID
Cấm sao chép JOIN chỉ theo *_CD từ WinForms trừ khi đã xác minh thiếu ID.

================================================================================
B. DANH SÁCH SP (đặc tả)
================================================================================

------------------------------------------------------------------------------
1) sp_pc_period_get_or_create
   IN  p_COMPANY_CD, p_PERIOD_YM, p_USER
   OUT / result: PERIOD_COST_ID, CURRENT_RUN_ID, STATUS
   Hành vi:
   - Tạo production_cost_period nếu chưa có (FROM_YMD/TO_YMD từ PERIOD_YM).
   - Nếu chưa có RUN: tạo production_cost_run DRAFT + 6 dòng production_cost_step.
   - Không đụng acc_period_lock_*.

------------------------------------------------------------------------------
2) sp_pc_object_sync
   IN p_COMPANY_CD, p_RUN_ID, p_USER
   Hành vi:
   - Nạp ứng viên TP DIVISION LIKE '155%' có DEPARTMENT_ID (product_info).
   - Upsert production_cost_object theo (RUN_ID, DEPARTMENT_ID, PRODUCT_ID).
   - Cảnh báo TP thiếu DEPARTMENT_ID (IS_SELECTED=0).

------------------------------------------------------------------------------
3) sp_pc_alloc_load_pool
   IN p_COMPANY_CD, p_RUN_ID, p_FROM_YMD, p_TO_YMD,
      p_IS_6212, p_BY_DEPARTMENT, p_USER
   Hành vi:
   - Aggregate chitdetailinfo: DEBIT LIKE '622%'/'627%' (+6212 nếu bật).
   - Tính CREDIT_AMOUNT, DEBIT_AMOUNT_154, REDUCTION, ALLOCATION_COSTS.
   - Company đặc biệt reduction=CREDIT_AMOUNT → đọc từ OPTIONS/config, không hardcode
     trong SP body theo COMPANY_CD rải rác (truyền cờ p_REDUCTION_MODE).
   - Ghi / ghi đè production_cost_alloc_pool theo RUN_ID + RUN_VERSION hiện tại (DRAFT).

------------------------------------------------------------------------------
4) sp_pc_alloc_load_basis
   IN p_COMPANY_CD, p_RUN_ID, p_ALLOCATE_TYPE (0|1), p_FROM_YMD, p_TO_YMD
   Hành vi:
   - type=0 (NVL):
       JOIN bom_finished → bom_material → output → chitdetail
       (COMPANY_CD + FINISHED_LINE_ID + OUTPUT_ID + CHITDETAIL_ID).
       DEBIT LIKE '621%' AND CREDIT LIKE '152%'.
       Nếu 1 OUTPUT_ID → nhiều FINISHED_LINE: split theo CONFIRMED_QUANTITY.
       Ghi production_cost_nvl_basis + nvl_basis_line.
       OUTPUT không BOM / OUTPUT_ID NULL → production_cost_nvl_orphan (không vào basis).
       CẤM dùng product_material_info (BOM lý thuyết) hoặc JOIN theo PRODUCT_CD.
   - type=1: Σ QUANTITY từ chit_inventory_input (N155/C154) group PRODUCT_ID.
   - Chỉ PRODUCT_ID có trong production_cost_object.IS_SELECTED=1.

------------------------------------------------------------------------------
5) sp_pc_alloc_calculate
   IN p_COMPANY_CD, p_RUN_ID, p_USER
   Hành vi:
   - Đọc pool + basis; Round(P_A * T_i / ΣT, NUMBER_DIGIT); dòng cuối = phần dư.
   - byDepartment: chia theo DEPARTMENT_ID.
   - Ghi production_cost_alloc_detail (DRAFT version).
   - Cập nhật step COST_ALLOCATION → PREVIEWED (qua app) / STATUS run.

------------------------------------------------------------------------------
6) sp_pc_alloc_confirm
   IN p_COMPANY_CD, p_RUN_ID, p_USER
   Hành vi:
   - Validate step trước DONE / REOPENED rules (FE-04).
   - Tăng RUN_VERSION (hoặc đóng băng version hiện tại là CONFIRMED).
   - Ghi/ghi đè production_cost_voucher_ref STABLE_KEY='ALLOC:{COMPANY}:{PERIOD}'.
   - Không tạo thêm dòng chứng từ nếu STABLE_KEY đã có CHIT_ID → UPDATE nội dung.
   - Step COST_ALLOCATION = DONE; history CONFIRM.

------------------------------------------------------------------------------
7) sp_pc_engine_claim / sp_pc_engine_assert
   IN p_COMPANY_CD, p_PERIOD_YM, p_COST_ENGINE ('GK'|'BI'), p_COGS_RULE, p_USER
   Hành vi:
   - GK: set production_cost_period.COST_ENGINE='GK', COGS_RULE='EXP_TO_154_ONLY'.
   - BI: set COST_ENGINE='BI' với một trong 3 rule hiện có.
   - Nếu COST_ENGINE đã là engine kia → SIGNAL lỗi exclusive lock.
   - Đọc acc_period_lock_step.COGS_SUMMARY; nếu DONE bởi engine kia → chặn.

------------------------------------------------------------------------------
8) sp_pc_xfer_exp_to_154_only
   IN p_COMPANY_CD, p_RUN_ID, p_USER
   Hành vi:
   - Yêu cầu COST_ENGINE='GK' và COGS_RULE='EXP_TO_154_ONLY'.
   - Kết chuyển 621/622/623/627 → 154 theo DEPARTMENT_ID (giống phần EXP của B-I).
   - Dừng tại đây — KHÔNG 154→155 / 154→632.
   - Ghi/ghi đè CT LOCK; STABLE_KEY='EXP_TO_154_ONLY:{COMPANY}:{PERIOD_YM}'.
   - Đồng bộ acc_period_lock_step COGS_SUMMARY=DONE (GATE 2B: thêm rule vào PeriodLock).
   - Tag CT để sp_pc_source_rebuild loại khỏi SRC_*.

------------------------------------------------------------------------------
8b) sp_pc_source_rebuild
   IN p_COMPANY_CD, p_RUN_ID, p_USER
   Hành vi:
   - Xóa/ghi đè production_cost_source theo RUN_VERSION hiện tại.
   - SRC_621/6212: PS nguồn kỳ, loại INPUT_TYPE=LOCK / voucher EXP_TO_154_ONLY.
   - SRC_622/627_ALLOC: từ alloc_detail CONFIRMED.
   - SRC_154_DIRECT: Nợ 154% Có không thuộc 621/622/623/627 và không phải CT EXP.
   - Không bao giờ ghi SOURCE_KIND từ số dư 154 sau kết chuyển.

------------------------------------------------------------------------------
9) sp_pc_wip_calculate
   IN p_COMPANY_CD, p_RUN_ID, p_WIP_METHOD, p_USER
   Hành vi:
   - Đọc production_cost_source + BEFORE_* opening — KHÔNG aggregate lại GL 154.
   - TOTAL_INCURRED = Σ SRC_* (không gồm XFER).
   - PERCENT_COMPLETED / CLOSING_WIP_QUANTITY theo CONG_THUC_TINH_DO_DANG.
   - Chế độ đặc biệt theo INCOMPLETE_MODE + INCOMPLETE_PERCENT trên run.
   - Validate % [0,100], Qdd≥0, mẫu số ≠0, CLOSING ≤ BEFORE+INCURRED.
   - Ghi production_cost_wip (DRAFT/PREVIEW version).

------------------------------------------------------------------------------
10) sp_pc_cost_confirm
   IN p_COMPANY_CD, p_RUN_ID, p_USER
   Hành vi:
   - UNIT_COST = TOTAL_COST_PRICE / QUANTITY (hoặc Q_HT nếu special).
   - Upsert production_cost_receipt theo INPUT_ID thuộc PRODUCT_ID trong kỳ.
   - Gọi cập nhật giá (apply_inventory_update_in_price hoặc SP wrapper) theo INPUT_ID —
     ghi đè đơn giá, không tạo phiếu mới (thay B-I 154_TO_155 trên kỳ G-K).
   - STABLE_KEY='COST_POST:{COMPANY}:{PERIOD}'.
   - Step COST_CALC = DONE; history.

------------------------------------------------------------------------------
11) sp_pc_step_reopen
    IN p_COMPANY_CD, p_RUN_ID, p_STEP_INDEX, p_REASON, p_USER
    Hành vi:
    - Chặn nếu bước sau còn DONE (phải mở lại ngược).
    - Bước ENGINE=BI: ủy quyền unlock PeriodLock B-I — SP này chỉ ghi REOPENED
      phía G-K + history; không tự unlock B-I trong 2A.
    - Giữ nguyên wip/receipt/alloc/source version đã confirm (không xóa về 0).
    - Cho phép tính thử version mới; chỉ ghi đè khi CONFIRM.

------------------------------------------------------------------------------
12) sp_pc_run_compare
    IN p_COMPANY_CD, p_RUN_ID, p_STEP_INDEX
    Hành vi:
    - So sánh version CONFIRMED trước đó vs PREVIEW hiện tại
      (622/627 totals, UNIT_COST, TOTAL_COST_PRICE, source totals).
    - Trả resultset cho FE bảng chênh lệch.

------------------------------------------------------------------------------
C. QUY TẮC GHI ĐÈ CHỨNG TỪ
================================================================================
UNIQUE (COMPANY_CD, STABLE_KEY) trên production_cost_voucher_ref.
Confirm lần N: UPDATE chitinfo/chitdetailinfo của CHIT_ID đã map; nếu chưa có thì INSERT
một lần rồi lưu CHIT_ID. Cấm INSERT chứng từ mới mỗi lần chạy.

------------------------------------------------------------------------------
D. KHÓA SỔ B-I
================================================================================
Đọc:
  SELECT STATUS FROM acc_period_lock_step
   WHERE COMPANY_CD=? AND PERIOD_YM=? AND STEP_CODE IN ('FA_PREPAID_LOCK','...')
Không tạo bảng lock song song. G-K step 2/4 lưu riêng trong production_cost_step.
*/

-- Placeholder để file hợp lệ khi source control / review (không tạo SP):
SELECT 'production_cost SP specifications — not executable stubs' AS NOTE;
