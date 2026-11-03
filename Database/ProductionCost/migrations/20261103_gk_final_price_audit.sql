-- =============================================================================
-- G-K final — snapshot giá để REOPEN hoàn nguyên được.
-- Chỉ DEV/TEST. Guard giống migration 2B: dừng trước mọi DDL nếu không phải
-- database/host/env đã duyệt. Không chạy trên Production.
-- MySQL 5.6 / 8.0. Không START TRANSACTION.
-- DROP chỉ hai procedure skeleton G-K (sp_pc_valuation_*). Không đụng procedure Kho/B-I.
-- =============================================================================

SOURCE 20261102_2b_00_guard_inline.sql;

DROP PROCEDURE IF EXISTS `sp_pc_valuation_apply_input_price`;
DROP PROCEDURE IF EXISTS `sp_pc_valuation_confirm`;

CREATE TABLE IF NOT EXISTS `production_cost_price_audit` (
  `AUDIT_ID` bigint(20) NOT NULL AUTO_INCREMENT,
  `COMPANY_CD` varchar(20) NOT NULL,
  `PERIOD_YM` char(6) NOT NULL,
  `RUN_ID` bigint(20) NOT NULL,
  `RUN_VERSION` int(11) NOT NULL,
  `TARGET_KIND` varchar(10) NOT NULL COMMENT 'INPUT | OUTPUT',
  `ROW_ID` bigint(20) NOT NULL COMMENT 'INPUT_ID hoặc OUTPUT_ID',
  `PRODUCT_ID` bigint(20) NOT NULL DEFAULT '0',
  `STORE_ID` bigint(20) DEFAULT NULL,
  `CHIT_TYPE` varchar(20) DEFAULT NULL,
  `OLD_UNIT_PRICE` decimal(18,6) NOT NULL DEFAULT '0.000000',
  `OLD_AMOUNT` decimal(18,2) NOT NULL DEFAULT '0.00',
  `NEW_UNIT_PRICE` decimal(18,6) NOT NULL DEFAULT '0.000000',
  `NEW_AMOUNT` decimal(18,2) NOT NULL DEFAULT '0.00',
  `IS_REVERSED` char(1) NOT NULL DEFAULT '0',
  `ISDEL` char(1) NOT NULL DEFAULT '0',
  `CREATE_BY` varchar(50) DEFAULT NULL,
  `CREATE_AT` datetime DEFAULT NULL,
  `UPDATE_BY` varchar(50) DEFAULT NULL,
  `UPDATE_AT` datetime DEFAULT NULL,
  PRIMARY KEY (`AUDIT_ID`),
  KEY `idx_pc_price_audit_run` (`COMPANY_CD`,`RUN_ID`,`RUN_VERSION`,`IS_REVERSED`),
  KEY `idx_pc_price_audit_row` (`COMPANY_CD`,`TARGET_KIND`,`ROW_ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci
  COMMENT='Snapshot giá trước khi B5 cập nhật — REOPEN chỉ hoàn khi giá hiện tại còn khớp';

SELECT 'gk_final price audit ready' AS NOTE;
