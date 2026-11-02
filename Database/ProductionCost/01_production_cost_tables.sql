-- =============================================================================
-- G-K Giá thành giản đơn — DDL bảng production_cost_*
-- MySQL 5.6 / 8.0 compatible
-- KHÔNG thực thi trên DB production cho đến khi được duyệt (Giai đoạn 2A).
--
-- Quy ước khóa liên kết:
--   - Ưu tiên *_ID + COMPANY_CD (DEPARTMENT_ID, PRODUCT_ID, INPUT_ID, CHIT_ID, ...)
--   - *_CD chỉ lưu denormalize hiển thị / đối chiếu; KHÔNG dùng làm JOIN chính
-- =============================================================================

SET NAMES utf8;

-- -----------------------------------------------------------------------------
-- 1) Kỳ G-K (1 dòng / công ty / kỳ) — trỏ RUN hiện hành
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `production_cost_period` (
  `PERIOD_COST_ID` bigint(20) NOT NULL AUTO_INCREMENT COMMENT 'PK kỳ G-K',
  `COMPANY_CD` varchar(20) NOT NULL COMMENT 'Tenant',
  `PERIOD_YM` char(6) NOT NULL COMMENT 'Kỳ yyyyMM',
  `FROM_YMD` char(8) NOT NULL COMMENT 'Ngày đầu kỳ',
  `TO_YMD` char(8) NOT NULL COMMENT 'Ngày cuối kỳ',
  `CURRENT_RUN_ID` bigint(20) DEFAULT NULL COMMENT 'RUN đang hiệu lực (FE-04 ghi đè cùng kỳ)',
  `STATUS` varchar(20) NOT NULL DEFAULT 'OPEN' COMMENT 'OPEN, IN_PROGRESS, DONE, REOPENED',
  `COST_ENGINE` varchar(10) NOT NULL DEFAULT 'NONE' COMMENT 'NONE|GK|BI exclusive owner of COGS_SUMMARY for this period',
  `COGS_RULE` varchar(50) DEFAULT NULL COMMENT 'GK: EXP_TO_154_ONLY; BI: 154_TO_155 | 154_TO_155_TO_632 | 154_TO_632',
  `ISDEL` char(1) NOT NULL DEFAULT '0',
  `CREATE_BY` varchar(50) DEFAULT NULL,
  `CREATE_AT` datetime DEFAULT NULL,
  `UPDATE_BY` varchar(50) DEFAULT NULL,
  `UPDATE_AT` datetime DEFAULT NULL,
  PRIMARY KEY (`PERIOD_COST_ID`),
  UNIQUE KEY `uk_pc_period` (`COMPANY_CD`,`PERIOD_YM`),
  KEY `idx_pc_period_run` (`CURRENT_RUN_ID`),
  KEY `idx_pc_period_status` (`COMPANY_CD`,`STATUS`),
  KEY `idx_pc_period_engine` (`COMPANY_CD`,`PERIOD_YM`,`COST_ENGINE`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci
  COMMENT='Kỳ tính giá thành giản đơn G-K — 1 dòng / COMPANY_CD+PERIOD_YM';

-- -----------------------------------------------------------------------------
-- 2) Phiên tính (RUN) — mỗi lần xác nhận ghi đè tăng RUN_VERSION
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `production_cost_run` (
  `RUN_ID` bigint(20) NOT NULL AUTO_INCREMENT COMMENT 'PK phiên tính',
  `PERIOD_COST_ID` bigint(20) NOT NULL COMMENT 'FK logic → production_cost_period',
  `COMPANY_CD` varchar(20) NOT NULL,
  `PERIOD_YM` char(6) NOT NULL,
  `RUN_VERSION` int(11) NOT NULL DEFAULT '1' COMMENT 'Tăng khi xác nhận ghi đè (FE-04)',
  `STATUS` varchar(20) NOT NULL DEFAULT 'DRAFT' COMMENT 'DRAFT, PREVIEWED, ALLOC_CONFIRMED (B2), CONFIRMED (toan ky), REOPENED, SUPERSEDED',
  `WIP_METHOD` varchar(40) NOT NULL DEFAULT 'PERCENT_COMPLETED' COMMENT 'PERCENT_COMPLETED | CLOSING_WIP_QUANTITY',
  `ALLOCATE_TYPE` tinyint(4) NOT NULL DEFAULT '0' COMMENT '0=NVL N621/C152, 1=SL N155/C154, 2=doanh thu (ẩn)',
  `IS_622_627_BY_DEPARTMENT` char(1) NOT NULL DEFAULT '0' COMMENT 'is622_627_byDepartment',
  `IS_6212` char(1) NOT NULL DEFAULT '0' COMMENT 'Đưa TK 6212 vào pool phân bổ',
  `ALLOC_NUMBER_DIGIT` tinyint(4) NOT NULL DEFAULT '0' COMMENT 'NUMBER_DIGIT làm tròn phân bổ',
  `INCOMPLETE_MODE` varchar(60) NOT NULL DEFAULT 'STANDARD' COMMENT 'STANDARD | incomplete_cost_SL_TP_SX | incomplete_ValueChange_Calculated_ID2720',
  `INCOMPLETE_PERCENT` decimal(8,4) NOT NULL DEFAULT '0.8000' COMMENT 'Hệ số p (0.8/0.7/0.5) — config, không hardcode UI',
  `ENABLE_PRODUCT_REDUCE` char(1) NOT NULL DEFAULT '0',
  `OPTIONS_JSON` longtext COMMENT 'Snapshot cấu hình khác (LONGTEXT tương thích MySQL 5.6)',
  `REOPEN_REASON` varchar(500) DEFAULT NULL,
  `REOPENED_BY` varchar(50) DEFAULT NULL,
  `REOPENED_AT` datetime DEFAULT NULL,
  `CONFIRMED_BY` varchar(50) DEFAULT NULL,
  `CONFIRMED_AT` datetime DEFAULT NULL,
  `ISDEL` char(1) NOT NULL DEFAULT '0',
  `CREATE_BY` varchar(50) DEFAULT NULL,
  `CREATE_AT` datetime DEFAULT NULL,
  `UPDATE_BY` varchar(50) DEFAULT NULL,
  `UPDATE_AT` datetime DEFAULT NULL,
  PRIMARY KEY (`RUN_ID`),
  KEY `idx_pc_run_period` (`PERIOD_COST_ID`,`RUN_VERSION`),
  KEY `idx_pc_run_company` (`COMPANY_CD`,`PERIOD_YM`,`STATUS`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci
  COMMENT='Phiên tính G-K — hỗ trợ mở lại / ghi đè theo RUN_VERSION';

-- -----------------------------------------------------------------------------
-- 3) Trạng thái bước G-K trong RUN (bước 2–5; bước 1/6 tham chiếu B-I)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `production_cost_step` (
  `STEP_ID` bigint(20) NOT NULL AUTO_INCREMENT,
  `RUN_ID` bigint(20) NOT NULL,
  `COMPANY_CD` varchar(20) NOT NULL,
  `STEP_INDEX` tinyint(4) NOT NULL COMMENT '1..6 — map UI G-K',
  `STEP_CODE` varchar(50) NOT NULL COMMENT 'FA_PREPAID_LOCK, COST_ALLOCATION, EXP_TO_154_ONLY, COST_CALC, VALUATION, PROFIT_LOSS',
  `ENGINE` varchar(10) NOT NULL DEFAULT 'GK' COMMENT 'BI | GK',
  `STATUS` varchar(20) NOT NULL DEFAULT 'NOT_STARTED' COMMENT 'NOT_STARTED, PROCESSING, DONE, REOPENED, ERROR',
  `BI_STEP_CODE` varchar(50) DEFAULT NULL COMMENT 'FA_PREPAID_LOCK | COGS_SUMMARY | PROFIT_LOSS_REPORT khi mirror B-I',
  `RESULT_SUMMARY` varchar(500) DEFAULT NULL COMMENT 'Tóm tắt kết quả lần xác nhận',
  `COMPLETED_BY` varchar(50) DEFAULT NULL,
  `COMPLETED_AT` datetime DEFAULT NULL,
  `ISDEL` char(1) NOT NULL DEFAULT '0',
  `CREATE_BY` varchar(50) DEFAULT NULL,
  `CREATE_AT` datetime DEFAULT NULL,
  `UPDATE_BY` varchar(50) DEFAULT NULL,
  `UPDATE_AT` datetime DEFAULT NULL,
  PRIMARY KEY (`STEP_ID`),
  UNIQUE KEY `uk_pc_step` (`RUN_ID`,`STEP_INDEX`),
  KEY `idx_pc_step_company` (`COMPANY_CD`,`STEP_CODE`,`STATUS`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci
  COMMENT='Trạng thái từng bước G-K trong phiên; B-I lock đọc từ acc_period_lock_step';

-- -----------------------------------------------------------------------------
-- 4) Đối tượng được chọn trong kỳ (Bước 0)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `production_cost_object` (
  `OBJECT_SEL_ID` bigint(20) NOT NULL AUTO_INCREMENT,
  `RUN_ID` bigint(20) NOT NULL,
  `COMPANY_CD` varchar(20) NOT NULL,
  `DEPARTMENT_ID` bigint(20) NOT NULL COMMENT 'FK logic → department_info.DEPARTMENT_ID',
  `PRODUCT_ID` bigint(20) NOT NULL DEFAULT '0' COMMENT '0 = chỉ đối tượng BP; >0 = TP gắn đối tượng — NOT NULL cho UNIQUE ổn định',
  `DEPARTMENT_CD` varchar(20) DEFAULT NULL COMMENT 'Denormalize hiển thị',
  `PRODUCT_CD` varchar(50) DEFAULT NULL COMMENT 'Denormalize hiển thị',
  `IS_SELECTED` char(1) NOT NULL DEFAULT '1',
  `WARNING_TEXT` varchar(500) DEFAULT NULL,
  `ISDEL` char(1) NOT NULL DEFAULT '0',
  `CREATE_BY` varchar(50) DEFAULT NULL,
  `CREATE_AT` datetime DEFAULT NULL,
  `UPDATE_BY` varchar(50) DEFAULT NULL,
  `UPDATE_AT` datetime DEFAULT NULL,
  PRIMARY KEY (`OBJECT_SEL_ID`),
  UNIQUE KEY `uk_pc_object` (`RUN_ID`,`DEPARTMENT_ID`,`PRODUCT_ID`),
  KEY `idx_pc_object_dept` (`COMPANY_CD`,`DEPARTMENT_ID`),
  KEY `idx_pc_object_prod` (`COMPANY_CD`,`PRODUCT_ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci
  COMMENT='Đối tượng / thành phẩm được chọn để tính G-K';

-- -----------------------------------------------------------------------------
-- 5) Pool phân bổ 622/627 (bảng trên — CONG_THUC_PHAN_BO mục 2)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `production_cost_alloc_pool` (
  `POOL_ID` bigint(20) NOT NULL AUTO_INCREMENT,
  `RUN_ID` bigint(20) NOT NULL,
  `RUN_VERSION` int(11) NOT NULL DEFAULT '1' COMMENT 'Khớp version đã confirm',
  `COMPANY_CD` varchar(20) NOT NULL,
  `ACCOUNT_CD` varchar(20) NOT NULL COMMENT '622* / 627* / 6212',
  `CREDIT_SAMPLE` varchar(20) DEFAULT NULL COMMENT 'Bên Có tham chiếu hiển thị',
  `DEPARTMENT_ID` bigint(20) NOT NULL DEFAULT '0' COMMENT '0 = dùng chung (ALL); >0 khi byDepartment — NOT NULL để UNIQUE ổn định',
  `DEPARTMENT_CD` varchar(20) DEFAULT NULL COMMENT 'Denormalize',
  `TOTAL_AMOUNT` decimal(18,2) NOT NULL DEFAULT '0.00' COMMENT 'Số phát sinh nợ',
  `FC_AMOUNT` decimal(18,2) NOT NULL DEFAULT '0.00',
  `CREDIT_AMOUNT` decimal(18,2) NOT NULL DEFAULT '0.00' COMMENT 'Σ CREDIT=A trong kỳ',
  `DEBIT_AMOUNT_154` decimal(18,2) NOT NULL DEFAULT '0.00' COMMENT 'Σ CREDIT=A AND DEBIT LIKE 154%',
  `REDUCTION` decimal(18,2) NOT NULL DEFAULT '0.00' COMMENT 'Điều chỉnh giảm (có thể sửa tay)',
  `ALLOCATION_COSTS` decimal(18,2) NOT NULL DEFAULT '0.00' COMMENT 'P_A = TOTAL - REDUCTION',
  `ISDEL` char(1) NOT NULL DEFAULT '0',
  `CREATE_BY` varchar(50) DEFAULT NULL,
  `CREATE_AT` datetime DEFAULT NULL,
  `UPDATE_BY` varchar(50) DEFAULT NULL,
  `UPDATE_AT` datetime DEFAULT NULL,
  PRIMARY KEY (`POOL_ID`),
  UNIQUE KEY `uk_pc_pool` (`RUN_ID`,`RUN_VERSION`,`ACCOUNT_CD`,`DEPARTMENT_ID`),
  KEY `idx_pc_pool_acc` (`COMPANY_CD`,`ACCOUNT_CD`),
  KEY `idx_pc_pool_dept` (`COMPANY_CD`,`DEPARTMENT_ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci
  COMMENT='Tổng hợp pool chi phí phân bổ 622/627 theo tài khoản';

-- -----------------------------------------------------------------------------
-- 6) Kết quả phân bổ theo thành phẩm × tài khoản (bảng dưới)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `production_cost_alloc_detail` (
  `ALLOC_DETAIL_ID` bigint(20) NOT NULL AUTO_INCREMENT,
  `RUN_ID` bigint(20) NOT NULL,
  `RUN_VERSION` int(11) NOT NULL DEFAULT '1',
  `COMPANY_CD` varchar(20) NOT NULL,
  `PRODUCT_ID` bigint(20) NOT NULL COMMENT 'FK logic → product_info',
  `DEPARTMENT_ID` bigint(20) NOT NULL COMMENT 'FK logic → department_info (đối tượng)',
  `POOL_DEPARTMENT_ID` bigint(20) NOT NULL DEFAULT '0' COMMENT 'DEPARTMENT_ID dòng pool; 0 = dùng chung',
  `PRODUCT_CD` varchar(50) DEFAULT NULL,
  `DEPARTMENT_CD` varchar(20) DEFAULT NULL,
  `AMOUNT_QUANTITY` decimal(18,2) NOT NULL DEFAULT '0.00' COMMENT 'Tiêu thức NVL: Σ N621/C152 theo TP',
  `QUANTITY` decimal(18,6) NOT NULL DEFAULT '0.000000' COMMENT 'Tiêu thức TPNK: Σ SL N155/C154',
  `AMOUNT_6211` decimal(18,2) NOT NULL DEFAULT '0.00',
  `AMOUNT_6212` decimal(18,2) NOT NULL DEFAULT '0.00',
  `ACCOUNT_CD` varchar(20) NOT NULL COMMENT 'Tài khoản được phân bổ (622/627/...)',
  `ALLOCATED_AMOUNT` decimal(18,2) NOT NULL DEFAULT '0.00' COMMENT 'Tiền phân bổ ACC_CD_A',
  `IS_LAST_ROW` char(1) NOT NULL DEFAULT '0' COMMENT '1 = dòng nhận phần dư làm tròn',
  `AMOUNT_622_SUM` decimal(18,2) NOT NULL DEFAULT '0.00' COMMENT 'Σ phân bổ 622* (denorm theo TP)',
  `AMOUNT_627_SUM` decimal(18,2) NOT NULL DEFAULT '0.00' COMMENT 'Σ phân bổ 627* (denorm theo TP)',
  `ISDEL` char(1) NOT NULL DEFAULT '0',
  `CREATE_BY` varchar(50) DEFAULT NULL,
  `CREATE_AT` datetime DEFAULT NULL,
  `UPDATE_BY` varchar(50) DEFAULT NULL,
  `UPDATE_AT` datetime DEFAULT NULL,
  PRIMARY KEY (`ALLOC_DETAIL_ID`),
  UNIQUE KEY `uk_pc_alloc_detail` (`RUN_ID`,`RUN_VERSION`,`PRODUCT_ID`,`DEPARTMENT_ID`,`ACCOUNT_CD`,`POOL_DEPARTMENT_ID`),
  KEY `idx_pc_alloc_prod` (`COMPANY_CD`,`PRODUCT_ID`),
  KEY `idx_pc_alloc_dept` (`COMPANY_CD`,`DEPARTMENT_ID`),
  KEY `idx_pc_alloc_acc` (`COMPANY_CD`,`ACCOUNT_CD`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci
  COMMENT='Chi tiết phân bổ 622/627 — khóa gồm DEPT đối tượng + POOL_DEPARTMENT_ID';

-- -----------------------------------------------------------------------------
-- 6b) Tiêu thức NVL (allocate_type=0) — tổng theo FINISHED_PRODUCT_ID
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `production_cost_nvl_basis` (
  `NVL_BASIS_ID` bigint(20) NOT NULL AUTO_INCREMENT,
  `RUN_ID` bigint(20) NOT NULL,
  `RUN_VERSION` int(11) NOT NULL DEFAULT '1',
  `COMPANY_CD` varchar(20) NOT NULL,
  `PRODUCT_ID` bigint(20) NOT NULL COMMENT 'FINISHED_PRODUCT_ID',
  `AMOUNT_QUANTITY` decimal(18,2) NOT NULL DEFAULT '0.00' COMMENT 'Σ tiền N621/C152 gắn TP qua BOM',
  `LINE_COUNT` int(11) NOT NULL DEFAULT '0' COMMENT 'Số dòng OUTPUT đã gắn',
  `ISDEL` char(1) NOT NULL DEFAULT '0',
  `CREATE_BY` varchar(50) DEFAULT NULL,
  `CREATE_AT` datetime DEFAULT NULL,
  `UPDATE_BY` varchar(50) DEFAULT NULL,
  `UPDATE_AT` datetime DEFAULT NULL,
  PRIMARY KEY (`NVL_BASIS_ID`),
  UNIQUE KEY `uk_pc_nvl_basis` (`RUN_ID`,`RUN_VERSION`,`PRODUCT_ID`),
  KEY `idx_pc_nvl_basis_prod` (`COMPANY_CD`,`PRODUCT_ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci
  COMMENT='Tiêu thức phân bổ NVL theo thành phẩm (không dùng BOM lý thuyết)';

-- -----------------------------------------------------------------------------
-- 6c) Audit từng dòng NVL → TP (hỗ trợ split OUTPUT dùng chung nhiều TP)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `production_cost_nvl_basis_line` (
  `NVL_LINE_ID` bigint(20) NOT NULL AUTO_INCREMENT,
  `RUN_ID` bigint(20) NOT NULL,
  `RUN_VERSION` int(11) NOT NULL DEFAULT '1',
  `COMPANY_CD` varchar(20) NOT NULL,
  `PRODUCT_ID` bigint(20) NOT NULL COMMENT 'FINISHED_PRODUCT_ID',
  `INVENTORY_ID` bigint(20) NOT NULL,
  `FINISHED_LINE_ID` bigint(20) NOT NULL,
  `BOM_MATERIAL_ID` bigint(20) DEFAULT NULL,
  `OUTPUT_ID` bigint(20) NOT NULL,
  `CHITDETAIL_ID` bigint(20) DEFAULT NULL,
  `MATERIAL_PRODUCT_ID` bigint(20) DEFAULT NULL,
  `CONFIRMED_QUANTITY` decimal(18,6) NOT NULL DEFAULT '0.000000',
  `SOURCE_AMOUNT` decimal(18,2) NOT NULL DEFAULT '0.00' COMMENT 'AMOUNT gốc trên CT / output',
  `ALLOCATED_AMOUNT` decimal(18,2) NOT NULL DEFAULT '0.00' COMMENT 'Số gán cho TP (sau split nếu cần)',
  `SPLIT_RATIO` decimal(18,8) NOT NULL DEFAULT '1.00000000' COMMENT '1 nếu 1 OUTPUT → 1 TP',
  `ISDEL` char(1) NOT NULL DEFAULT '0',
  `CREATE_BY` varchar(50) DEFAULT NULL,
  `CREATE_AT` datetime DEFAULT NULL,
  PRIMARY KEY (`NVL_LINE_ID`),
  KEY `idx_pc_nvl_line_run` (`RUN_ID`,`RUN_VERSION`,`PRODUCT_ID`),
  KEY `idx_pc_nvl_line_output` (`COMPANY_CD`,`OUTPUT_ID`),
  KEY `idx_pc_nvl_line_finished` (`COMPANY_CD`,`FINISHED_LINE_ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci
  COMMENT='Chi tiết gắn OUTPUT_ID → FINISHED_PRODUCT_ID cho tiêu thức NVL';

-- -----------------------------------------------------------------------------
-- 6d) Xuất NVL kỳ không gắn BOM / thiếu OUTPUT_ID (cảnh báo — không vào basis)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `production_cost_nvl_orphan` (
  `ORPHAN_ID` bigint(20) NOT NULL AUTO_INCREMENT,
  `RUN_ID` bigint(20) NOT NULL,
  `RUN_VERSION` int(11) NOT NULL DEFAULT '1',
  `COMPANY_CD` varchar(20) NOT NULL,
  `OUTPUT_ID` bigint(20) DEFAULT NULL,
  `CHITDETAIL_ID` bigint(20) DEFAULT NULL,
  `INVENTORY_ID` bigint(20) DEFAULT NULL,
  `MATERIAL_PRODUCT_ID` bigint(20) DEFAULT NULL,
  `AMOUNT` decimal(18,2) NOT NULL DEFAULT '0.00',
  `REASON_CODE` varchar(40) NOT NULL DEFAULT 'NO_BOM' COMMENT 'NO_BOM | OUTPUT_NULL | SPLIT_QTY_ZERO | DETAIL_MISSING',
  `WARNING_TEXT` varchar(500) DEFAULT NULL,
  `ISDEL` char(1) NOT NULL DEFAULT '0',
  `CREATE_AT` datetime DEFAULT NULL,
  PRIMARY KEY (`ORPHAN_ID`),
  KEY `idx_pc_nvl_orphan_run` (`RUN_ID`,`RUN_VERSION`),
  KEY `idx_pc_nvl_orphan_output` (`COMPANY_CD`,`OUTPUT_ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci
  COMMENT='NVL không gắn TP — loại khỏi AMOUNT_QUANTITY, không fallback CD/BOM lý thuyết';

-- -----------------------------------------------------------------------------
-- 6e) Chi phí nguồn dùng cho Bước 4 — chống cộng trùng với EXP_TO_154
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `production_cost_source` (
  `SOURCE_ID` bigint(20) NOT NULL AUTO_INCREMENT,
  `RUN_ID` bigint(20) NOT NULL,
  `RUN_VERSION` int(11) NOT NULL DEFAULT '1',
  `COMPANY_CD` varchar(20) NOT NULL,
  `PRODUCT_ID` bigint(20) NOT NULL,
  `DEPARTMENT_ID` bigint(20) NOT NULL DEFAULT '0' COMMENT '0 nếu chưa gắn; NOT NULL cho UNIQUE ổn định',
  `SOURCE_KIND` varchar(30) NOT NULL COMMENT 'SRC_621|SRC_6212|SRC_622_ALLOC|SRC_627_ALLOC|SRC_154_DIRECT',
  `AMOUNT` decimal(18,2) NOT NULL DEFAULT '0.00',
  `NOTE` varchar(255) DEFAULT NULL COMMENT 'Không bao gồm XFER_EXP_TO_154',
  `ISDEL` char(1) NOT NULL DEFAULT '0',
  `CREATE_BY` varchar(50) DEFAULT NULL,
  `CREATE_AT` datetime DEFAULT NULL,
  `UPDATE_BY` varchar(50) DEFAULT NULL,
  `UPDATE_AT` datetime DEFAULT NULL,
  PRIMARY KEY (`SOURCE_ID`),
  UNIQUE KEY `uk_pc_source` (`RUN_ID`,`RUN_VERSION`,`PRODUCT_ID`,`DEPARTMENT_ID`,`SOURCE_KIND`),
  KEY `idx_pc_source_prod` (`COMPANY_CD`,`PRODUCT_ID`),
  KEY `idx_pc_source_kind` (`COMPANY_CD`,`SOURCE_KIND`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci
  COMMENT='Nguồn chi phí hợp lệ cho tính GT — loại bút toán kết chuyển B3';

-- -----------------------------------------------------------------------------
-- 7) Chi phí dở dang / giá thành (CONG_THUC_TINH_DO_DANG)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `production_cost_wip` (
  `WIP_ID` bigint(20) NOT NULL AUTO_INCREMENT,
  `RUN_ID` bigint(20) NOT NULL,
  `RUN_VERSION` int(11) NOT NULL DEFAULT '1',
  `COMPANY_CD` varchar(20) NOT NULL,
  `PRODUCT_ID` bigint(20) NOT NULL,
  `DEPARTMENT_ID` bigint(20) NOT NULL,
  `PRODUCT_CD` varchar(50) DEFAULT NULL,
  `DEPARTMENT_CD` varchar(20) DEFAULT NULL,
  `INCOMPLETE_QTY_OPENING` decimal(18,6) NOT NULL DEFAULT '0.000000' COMMENT 'SL dở dang đầu kỳ',
  `BEFORE_AMOUNT_RESULT_621` decimal(18,2) NOT NULL DEFAULT '0.00' COMMENT '154 ĐK từ 621',
  `BEFORE_AMOUNT_RESULT_62N` decimal(18,2) NOT NULL DEFAULT '0.00' COMMENT '154 ĐK từ 6212/622/627',
  `BEFORE_AMOUNT_154` decimal(18,2) NOT NULL DEFAULT '0.00' COMMENT 'Tổng ĐK 154',
  `AMOUNT_621` decimal(18,2) NOT NULL DEFAULT '0.00' COMMENT 'CP nguồn kỳ — không gồm bút toán kết chuyển trùng',
  `AMOUNT_6212` decimal(18,2) NOT NULL DEFAULT '0.00',
  `AMOUNT_622` decimal(18,2) NOT NULL DEFAULT '0.00' COMMENT 'Từ production_cost_alloc_detail (Bước 2)',
  `AMOUNT_627` decimal(18,2) NOT NULL DEFAULT '0.00',
  `AMOUNT_154_DIRECT` decimal(18,2) NOT NULL DEFAULT '0.00' COMMENT 'Phát sinh thẳng 154 — cộng 1 lần',
  `TOTAL_AMOUNT_INCURRED` decimal(18,2) NOT NULL DEFAULT '0.00',
  `QUANTITY` decimal(18,6) NOT NULL DEFAULT '0.000000' COMMENT 'SL TP hoàn thành / nhập kho',
  `PERCENT_COMPLETED` decimal(8,2) NOT NULL DEFAULT '0.00',
  `CLOSING_INCOMPLETE_QUANTITY` decimal(18,6) NOT NULL DEFAULT '0.000000',
  `AMOUNT_RESULT_621` decimal(18,2) NOT NULL DEFAULT '0.00' COMMENT 'DD CK phần 621',
  `AMOUNT_RESULT_62N` decimal(18,2) NOT NULL DEFAULT '0.00' COMMENT 'DD CK phần 62N',
  `CLOSING_INCOMPLETE_AMOUNT_154` decimal(18,2) NOT NULL DEFAULT '0.00',
  `PRODUCT_REDUCE` decimal(18,2) NOT NULL DEFAULT '0.00',
  `TOTAL_COST_PRICE` decimal(18,2) NOT NULL DEFAULT '0.00',
  `UNIT_COST` decimal(18,6) NOT NULL DEFAULT '0.000000',
  `Q_HT` decimal(18,6) NOT NULL DEFAULT '0.000000' COMMENT 'Sản lượng quy đổi (chế độ đặc biệt)',
  `ISDEL` char(1) NOT NULL DEFAULT '0',
  `CREATE_BY` varchar(50) DEFAULT NULL,
  `CREATE_AT` datetime DEFAULT NULL,
  `UPDATE_BY` varchar(50) DEFAULT NULL,
  `UPDATE_AT` datetime DEFAULT NULL,
  PRIMARY KEY (`WIP_ID`),
  UNIQUE KEY `uk_pc_wip` (`RUN_ID`,`RUN_VERSION`,`PRODUCT_ID`,`DEPARTMENT_ID`),
  KEY `idx_pc_wip_prod` (`COMPANY_CD`,`PRODUCT_ID`),
  KEY `idx_pc_wip_dept` (`COMPANY_CD`,`DEPARTMENT_ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci
  COMMENT='Kết quả tính dở dang và tổng giá thành theo PRODUCT_ID + DEPARTMENT_ID';

-- -----------------------------------------------------------------------------
-- 8) Liên kết cập nhật giá phiếu nhập TP (INPUT_ID)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `production_cost_receipt` (
  `RECEIPT_COST_ID` bigint(20) NOT NULL AUTO_INCREMENT,
  `RUN_ID` bigint(20) NOT NULL,
  `RUN_VERSION` int(11) NOT NULL DEFAULT '1',
  `COMPANY_CD` varchar(20) NOT NULL,
  `PRODUCT_ID` bigint(20) NOT NULL,
  `DEPARTMENT_ID` bigint(20) NOT NULL DEFAULT '0' COMMENT 'Khớp WIP PRODUCT+DEPT; 0 chỉ khi chứng từ không có DEPT hợp lệ (không tự suy đoán)',
  `INPUT_ID` bigint(20) NOT NULL COMMENT 'FK logic → chit_inventory_input.INPUT_ID',
  `INVENTORY_ID` bigint(20) DEFAULT NULL COMMENT 'FK logic → chit_inventory_info',
  `PRODUCT_CD` varchar(50) DEFAULT NULL,
  `INVENTORY_NO` varchar(50) DEFAULT NULL,
  `QTY` decimal(18,6) NOT NULL DEFAULT '0.000000',
  `OLD_UNIT_PRICE` decimal(18,6) NOT NULL DEFAULT '0.000000',
  `NEW_UNIT_PRICE` decimal(18,6) NOT NULL DEFAULT '0.000000',
  `OLD_AMOUNT` decimal(18,2) NOT NULL DEFAULT '0.00',
  `NEW_AMOUNT` decimal(18,2) NOT NULL DEFAULT '0.00',
  `DELTA_AMOUNT` decimal(18,2) NOT NULL DEFAULT '0.00',
  `IS_APPLIED` char(1) NOT NULL DEFAULT '0' COMMENT '1 = đã mô phỏng/ghi cập nhật giá',
  `ISDEL` char(1) NOT NULL DEFAULT '0',
  `CREATE_BY` varchar(50) DEFAULT NULL,
  `CREATE_AT` datetime DEFAULT NULL,
  `UPDATE_BY` varchar(50) DEFAULT NULL,
  `UPDATE_AT` datetime DEFAULT NULL,
  PRIMARY KEY (`RECEIPT_COST_ID`),
  UNIQUE KEY `uk_pc_receipt` (`RUN_ID`,`RUN_VERSION`,`INPUT_ID`),
  KEY `idx_pc_receipt_input` (`COMPANY_CD`,`INPUT_ID`),
  KEY `idx_pc_receipt_prod` (`COMPANY_CD`,`PRODUCT_ID`),
  KEY `idx_pc_receipt_dept` (`COMPANY_CD`,`PRODUCT_ID`,`DEPARTMENT_ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci
  COMMENT='Ánh xạ giá thành → phiếu nhập TP theo INPUT_ID (ghi đè, không tạo phiếu mới)';

-- -----------------------------------------------------------------------------
-- 9) Tham chiếu chứng từ kế toán sinh ra bởi G-K (ghi đè theo mã ổn định)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `production_cost_voucher_ref` (
  `VOUCHER_REF_ID` bigint(20) NOT NULL AUTO_INCREMENT,
  `RUN_ID` bigint(20) NOT NULL,
  `COMPANY_CD` varchar(20) NOT NULL,
  `STEP_INDEX` tinyint(4) NOT NULL COMMENT 'Bước G-K tạo chứng từ',
  `STEP_CODE` varchar(50) NOT NULL,
  `REF_ROLE` varchar(50) NOT NULL COMMENT 'ALLOC_MEMO, EXP_TO_154_ONLY, COST_POST, VALUATION_COGS, ...',
  `STABLE_KEY` varchar(100) NOT NULL COMMENT 'Khóa ổn định ghi đè: EXP_TO_154_ONLY:{COMPANY}:{PERIOD_YM}',
  `CHIT_ID` bigint(20) DEFAULT NULL COMMENT 'FK logic → chitinfo.CHIT_ID',
  `CHIT_CD` varchar(30) DEFAULT NULL COMMENT 'Denormalize',
  `CHITDETAIL_ID` bigint(20) DEFAULT NULL,
  `RUN_VERSION` int(11) NOT NULL DEFAULT '1' COMMENT 'Version lần ghi gần nhất',
  `ISDEL` char(1) NOT NULL DEFAULT '0',
  `CREATE_BY` varchar(50) DEFAULT NULL,
  `CREATE_AT` datetime DEFAULT NULL,
  `UPDATE_BY` varchar(50) DEFAULT NULL,
  `UPDATE_AT` datetime DEFAULT NULL,
  PRIMARY KEY (`VOUCHER_REF_ID`),
  UNIQUE KEY `uk_pc_voucher` (`COMPANY_CD`,`STABLE_KEY`),
  KEY `idx_pc_voucher_run` (`RUN_ID`,`STEP_INDEX`),
  KEY `idx_pc_voucher_chit` (`COMPANY_CD`,`CHIT_ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci
  COMMENT='Ánh xạ chứng từ G-K — UNIQUE STABLE_KEY đảm bảo ghi đè không tạo trùng';

-- -----------------------------------------------------------------------------
-- 10) Lịch sử thao tác FE-04 (mở lại / preview / confirm)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS `production_cost_history` (
  `HISTORY_ID` bigint(20) NOT NULL AUTO_INCREMENT,
  `RUN_ID` bigint(20) NOT NULL,
  `COMPANY_CD` varchar(20) NOT NULL,
  `PERIOD_YM` char(6) NOT NULL,
  `STEP_INDEX` tinyint(4) NOT NULL,
  `ACTION_CODE` varchar(30) NOT NULL COMMENT 'REOPEN, PREVIEW, CONFIRM, ERROR',
  `STATUS_AFTER` varchar(20) DEFAULT NULL,
  `RUN_VERSION` int(11) DEFAULT NULL,
  `RESULT_BEFORE` varchar(1000) DEFAULT NULL,
  `RESULT_AFTER` varchar(1000) DEFAULT NULL,
  `DIFFERENCE_TEXT` varchar(1000) DEFAULT NULL,
  `REASON` varchar(500) DEFAULT NULL,
  `USER_NAME` varchar(50) DEFAULT NULL,
  `ACTION_AT` datetime DEFAULT NULL,
  `ISDEL` char(1) NOT NULL DEFAULT '0',
  PRIMARY KEY (`HISTORY_ID`),
  KEY `idx_pc_hist_run` (`RUN_ID`,`STEP_INDEX`),
  KEY `idx_pc_hist_company` (`COMPANY_CD`,`PERIOD_YM`,`ACTION_AT`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci
  COMMENT='Nhật ký mở lại / tính thử / ghi đè G-K';

-- -----------------------------------------------------------------------------
-- Ghi chú FK vật lý:
-- Không gắn CONSTRAINT cứng sang department_info / product_info / chitinfo /
-- chit_inventory_input trong bản 2A để tránh phá dump legacy và hỗ trợ cài đặt
-- từng phần. Backend bắt buộc JOIN theo *_ID + COMPANY_CD; có thể bổ sung FK
-- sau khi dữ liệu ID đã sạch.
-- -----------------------------------------------------------------------------
