-- G-K 2B — kiểm tra chỉ đọc (DEV/TEST). Không DML/DDL.
-- Chạy sau migration. Mô tả CT nằm ở chitdescriptioninfo.DESCRIPTION
-- (chitinfo không có cột DESCRIPTION).

CALL sp_pc_2b_require_allowed_db();

SELECT DATABASE() AS db_name, NOW() AS checked_at;

-- 1) Bảng production_cost_*
SELECT TABLE_NAME, ENGINE, TABLE_COLLATION
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME LIKE 'production_cost_%'
ORDER BY TABLE_NAME;

-- 2) UNIQUE / nullable rủi ro
SELECT TABLE_NAME, INDEX_NAME, COLUMN_NAME, NULLABLE, COLUMN_KEY
FROM information_schema.STATISTICS s
JOIN information_schema.COLUMNS c
  ON c.TABLE_SCHEMA = s.TABLE_SCHEMA
 AND c.TABLE_NAME = s.TABLE_NAME
 AND c.COLUMN_NAME = s.COLUMN_NAME
WHERE s.TABLE_SCHEMA = DATABASE()
  AND s.TABLE_NAME LIKE 'production_cost_%'
  AND s.NON_UNIQUE = 0
  AND s.INDEX_NAME <> 'PRIMARY'
ORDER BY TABLE_NAME, INDEX_NAME, SEQ_IN_INDEX;

-- Cột trong UNIQUE phải NOT NULL (NULLABLE='NO')
SELECT s.TABLE_NAME, s.INDEX_NAME, s.COLUMN_NAME, c.IS_NULLABLE AS risk_if_YES
FROM information_schema.STATISTICS s
JOIN information_schema.COLUMNS c
  ON c.TABLE_SCHEMA = s.TABLE_SCHEMA
 AND c.TABLE_NAME = s.TABLE_NAME
 AND c.COLUMN_NAME = s.COLUMN_NAME
WHERE s.TABLE_SCHEMA = DATABASE()
  AND s.TABLE_NAME LIKE 'production_cost_%'
  AND s.NON_UNIQUE = 0
  AND s.INDEX_NAME <> 'PRIMARY'
  AND c.IS_NULLABLE = 'YES';

-- 3) Stored procedures 2B
SELECT ROUTINE_NAME, ROUTINE_TYPE, CREATED, LAST_ALTERED
FROM information_schema.ROUTINES
WHERE ROUTINE_SCHEMA = DATABASE()
  AND (
        ROUTINE_NAME LIKE 'sp_pc_engine_%'
     OR ROUTINE_NAME LIKE 'sp_pc_nvl_%'
     OR ROUTINE_NAME LIKE 'sp_period_lock_cogs_%'
  )
ORDER BY ROUTINE_NAME;

-- 4) COGS SP chấp nhận EXP_TO_154_ONLY (đọc định nghĩa)
SELECT ROUTINE_NAME,
       CASE WHEN ROUTINE_DEFINITION LIKE '%EXP_TO_154_ONLY%' THEN 'YES' ELSE 'NO' END AS has_exp_to_154_only,
       CASE WHEN ROUTINE_DEFINITION LIKE '%sp_pc_engine_assert_for_cogs%' THEN 'YES' ELSE 'NO' END AS has_engine_assert
FROM information_schema.ROUTINES
WHERE ROUTINE_SCHEMA = DATABASE()
  AND ROUTINE_NAME IN (
    'sp_period_lock_cogs_validate',
    'sp_period_lock_cogs_build_xfer',
    'sp_period_lock_cogs_create_voucher',
    'sp_period_lock_cogs_unlock'
  );

-- 5) Mẫu đối chiếu đọc (điền COMPANY_CD / PERIOD_YM khi test nghiệp vụ)
-- Tổng phân bổ 622/627 vs CT LOCK EXP (chỉ SELECT — comment sẵn)
/*
SET @cd = 'YOUR_COMPANY';
SET @ym = '202601';

SELECT 'alloc_622_627' AS kind, SUM(ALLOCATED_AMOUNT) AS amt
FROM production_cost_alloc_detail ad
JOIN production_cost_period p ON p.CURRENT_RUN_ID = ad.RUN_ID AND p.COMPANY_CD = ad.COMPANY_CD
JOIN production_cost_run r ON r.RUN_ID = ad.RUN_ID AND r.RUN_VERSION = ad.RUN_VERSION
WHERE p.COMPANY_CD = @cd AND p.PERIOD_YM = @ym AND p.COST_ENGINE = 'GK'
  AND ad.ISDEL = '0' AND (ad.ACCOUNT_CD LIKE '622%' OR ad.ACCOUNT_CD LIKE '627%');

SELECT 'lock_credit_622_627' AS kind, SUM(CD.AMOUNT) AS amt
FROM chitinfo C
JOIN chitdetailinfo CD ON CD.COMPANY_CD = C.COMPANY_CD AND CD.CHIT_ID = C.CHIT_ID AND CD.ISDEL = '0'
WHERE C.COMPANY_CD = @cd AND C.ISDEL = '0' AND C.INPUT_TYPE = 'LOCK'
  AND C.CHIT_YMD BETWEEN CONCAT(@ym,'01') AND DATE_FORMAT(LAST_DAY(STR_TO_DATE(CONCAT(@ym,'01'),'%Y%m%d')),'%Y%m%d')
  AND (TRIM(CD.CREDIT) LIKE '622%' OR TRIM(CD.CREDIT) LIKE '627%');

SELECT 'forbidden_154_to_155_632' AS kind, COUNT(*) AS cnt
FROM chitinfo C
JOIN chitdetailinfo CD ON CD.COMPANY_CD = C.COMPANY_CD AND CD.CHIT_ID = C.CHIT_ID AND CD.ISDEL = '0'
WHERE C.COMPANY_CD = @cd AND C.ISDEL = '0' AND C.INPUT_TYPE = 'LOCK'
  AND C.CHIT_YMD BETWEEN CONCAT(@ym,'01') AND DATE_FORMAT(LAST_DAY(STR_TO_DATE(CONCAT(@ym,'01'),'%Y%m%d')),'%Y%m%d')
  AND TRIM(CD.DEBIT) LIKE '15%'
  AND (TRIM(CD.CREDIT) LIKE '154%' OR TRIM(CD.DEBIT) LIKE '632%' OR TRIM(CD.CREDIT) LIKE '632%')
  AND C.DESCRIPTION LIKE '%EXP_TO_154_ONLY%';

SELECT COMPANY_CD, PERIOD_YM, COST_ENGINE, COGS_RULE, STATUS, CURRENT_RUN_ID
FROM production_cost_period
WHERE COMPANY_CD = @cd AND PERIOD_YM = @ym AND ISDEL = '0';
*/
