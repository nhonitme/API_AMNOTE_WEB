-- =============================================================================
-- G-K 2B — assertion test DEV/TEST (không tự seed nghiệp vụ nặng)
-- Cột đúng schema: chitinfo.INPUT_TYPE, LOCK_STEP_CODE, AMOUNT;
--   mô tả ở chitdescriptioninfo.DESCRIPTION (KHÔNG có C.DESCRIPTION).
--
-- Cách chạy:
--   SET @GK_2B_ALLOW_DB = '<dev>';
--   USE <dev>;
--   CALL sp_pc_2b_require_allowed_db();
--   SET @cd='...'; SET @ym_gk='...'; SET @ym_bi='...'; SET @user='dev_test';
--   SOURCE 20261102_2b_98_test_scenarios_dev.sql;
--
-- Kết quả: bảng tmp_gk2b_assert (ASSERT_NAME, EXPECTED, ACTUAL, PASS_FAIL)
-- =============================================================================

CALL sp_pc_2b_require_allowed_db();

DROP TEMPORARY TABLE IF EXISTS tmp_gk2b_assert;
CREATE TEMPORARY TABLE tmp_gk2b_assert (
    ASSERT_NAME VARCHAR(100) NOT NULL,
    EXPECTED VARCHAR(100) NOT NULL,
    ACTUAL VARCHAR(100) NOT NULL,
    PASS_FAIL CHAR(4) NOT NULL
) ENGINE=MEMORY;

DROP PROCEDURE IF EXISTS sp_gk2b_assert_eq;
DELIMITER $$
CREATE PROCEDURE sp_gk2b_assert_eq(
    IN p_name VARCHAR(100),
    IN p_expected VARCHAR(100),
    IN p_actual VARCHAR(100)
)
BEGIN
    INSERT INTO tmp_gk2b_assert(ASSERT_NAME, EXPECTED, ACTUAL, PASS_FAIL)
    VALUES (
        p_name,
        IFNULL(p_expected, ''),
        IFNULL(p_actual, ''),
        IF IFNULL(p_expected, '') = IFNULL(p_actual, '') THEN 'PASS' ELSE 'FAIL' END
    );
END$$
DELIMITER ;

-- Helper snapshot COGS LOCK vouchers
-- Dùng CHIT_YMD = last day of period; INPUT_TYPE='LOCK'; LOCK_STEP_CODE='COGS_SUMMARY'

SET @from_gk = CONCAT(@ym_gk, '01');
SET @to_gk = DATE_FORMAT(LAST_DAY(STR_TO_DATE(CONCAT(@ym_gk, '01'), '%Y%m%d')), '%Y%m%d');

-- ---------------------------------------------------------------------------
-- SNAPSHOT trước khi thử B-I xâm nhập kỳ G-K
-- ---------------------------------------------------------------------------
SELECT
    COUNT(DISTINCT h.CHIT_ID) INTO @gk_chit_cnt_before
FROM chitinfo h
WHERE h.COMPANY_CD = @cd
  AND h.ISDEL = '0'
  AND h.INPUT_TYPE = 'LOCK'
  AND h.LOCK_STEP_CODE = 'COGS_SUMMARY'
  AND h.CHIT_YMD = @to_gk;

SELECT
    IFNULL(SUM(h.AMOUNT), 0) INTO @gk_amt_before
FROM chitinfo h
WHERE h.COMPANY_CD = @cd
  AND h.ISDEL = '0'
  AND h.INPUT_TYPE = 'LOCK'
  AND h.LOCK_STEP_CODE = 'COGS_SUMMARY'
  AND h.CHIT_YMD = @to_gk;

SELECT
    IFNULL(SUM(d.AMOUNT), 0) INTO @gk_detail_amt_before
FROM chitinfo h
INNER JOIN chitdetailinfo d
    ON d.COMPANY_CD = h.COMPANY_CD AND d.CHIT_ID = h.CHIT_ID AND d.ISDEL = '0'
WHERE h.COMPANY_CD = @cd
  AND h.ISDEL = '0'
  AND h.INPUT_TYPE = 'LOCK'
  AND h.LOCK_STEP_CODE = 'COGS_SUMMARY'
  AND h.CHIT_YMD = @to_gk;

SELECT IFNULL(COST_ENGINE, 'NONE'), IFNULL(COGS_RULE, '')
  INTO @eng_before, @rule_before
FROM production_cost_period
WHERE COMPANY_CD = @cd AND PERIOD_YM = @ym_gk AND ISDEL = '0'
LIMIT 1;

SELECT STATUS INTO @cogs_step_before
FROM acc_period_lock_step
WHERE COMPANY_CD = @cd AND PERIOD_YM = @ym_gk AND STEP_CODE = 'COGS_SUMMARY'
LIMIT 1;

-- ---------------------------------------------------------------------------
-- T9 BẮT BUỘC: G-K đã có CT → B-I rule khác bị từ chối → CT G-K nguyên vẹn
-- ---------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_gk2b_try_bi_on_gk;
DELIMITER $$
CREATE PROCEDURE sp_gk2b_try_bi_on_gk(
    IN p_cd VARCHAR(20),
    IN p_ym CHAR(6),
    IN p_user VARCHAR(50),
    OUT p_rejected INT
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        SET p_rejected = 1;
    END;
    SET p_rejected = 0;
    CALL sp_period_lock_cogs_create_voucher(p_cd, p_ym, '154_TO_632', p_user);
END$$
DELIMITER ;

CALL sp_gk2b_try_bi_on_gk(@cd, @ym_gk, @user, @bi_rejected);

CALL sp_gk2b_assert_eq('T9_BI_REJECTED', '1', CAST(IFNULL(@bi_rejected, 0) AS CHAR));

SELECT COUNT(DISTINCT h.CHIT_ID) INTO @gk_chit_cnt_after
FROM chitinfo h
WHERE h.COMPANY_CD = @cd AND h.ISDEL = '0' AND h.INPUT_TYPE = 'LOCK'
  AND h.LOCK_STEP_CODE = 'COGS_SUMMARY' AND h.CHIT_YMD = @to_gk;

SELECT IFNULL(SUM(h.AMOUNT), 0) INTO @gk_amt_after
FROM chitinfo h
WHERE h.COMPANY_CD = @cd AND h.ISDEL = '0' AND h.INPUT_TYPE = 'LOCK'
  AND h.LOCK_STEP_CODE = 'COGS_SUMMARY' AND h.CHIT_YMD = @to_gk;

SELECT IFNULL(SUM(d.AMOUNT), 0) INTO @gk_detail_amt_after
FROM chitinfo h
INNER JOIN chitdetailinfo d ON d.COMPANY_CD = h.COMPANY_CD AND d.CHIT_ID = h.CHIT_ID AND d.ISDEL = '0'
WHERE h.COMPANY_CD = @cd AND h.ISDEL = '0' AND h.INPUT_TYPE = 'LOCK'
  AND h.LOCK_STEP_CODE = 'COGS_SUMMARY' AND h.CHIT_YMD = @to_gk;

SELECT IFNULL(COST_ENGINE, 'NONE'), IFNULL(COGS_RULE, '')
  INTO @eng_after, @rule_after
FROM production_cost_period
WHERE COMPANY_CD = @cd AND PERIOD_YM = @ym_gk AND ISDEL = '0'
LIMIT 1;

SELECT STATUS INTO @cogs_step_after
FROM acc_period_lock_step
WHERE COMPANY_CD = @cd AND PERIOD_YM = @ym_gk AND STEP_CODE = 'COGS_SUMMARY'
LIMIT 1;

CALL sp_gk2b_assert_eq('T9_CHIT_CNT_UNCHANGED', CAST(@gk_chit_cnt_before AS CHAR), CAST(@gk_chit_cnt_after AS CHAR));
CALL sp_gk2b_assert_eq('T9_HEADER_AMT_UNCHANGED', CAST(@gk_amt_before AS CHAR), CAST(@gk_amt_after AS CHAR));
CALL sp_gk2b_assert_eq('T9_DETAIL_AMT_UNCHANGED', CAST(@gk_detail_amt_before AS CHAR), CAST(@gk_detail_amt_after AS CHAR));
CALL sp_gk2b_assert_eq('T9_ENGINE_UNCHANGED', IFNULL(@eng_before, 'NONE'), IFNULL(@eng_after, 'NONE'));
CALL sp_gk2b_assert_eq('T9_RULE_UNCHANGED', IFNULL(@rule_before, ''), IFNULL(@rule_after, ''));
CALL sp_gk2b_assert_eq('T9_STEP_STATUS_UNCHANGED', IFNULL(@cogs_step_before, ''), IFNULL(@cogs_step_after, ''));

-- ---------------------------------------------------------------------------
-- T1b: CT G-K chỉ Nợ 154* / Có 621|622|623|627 (không 155/632)
-- ---------------------------------------------------------------------------
SELECT IFNULL(SUM(CASE
    WHEN TRIM(d.DEBIT) LIKE '154%'
     AND (
            TRIM(d.CREDIT) LIKE '621%'
         OR TRIM(d.CREDIT) LIKE '622%'
         OR TRIM(d.CREDIT) LIKE '623%'
         OR TRIM(d.CREDIT) LIKE '627%'
     ) THEN 0 ELSE 1 END), 0)
  INTO @bad_lines
FROM chitinfo h
INNER JOIN chitdetailinfo d ON d.COMPANY_CD = h.COMPANY_CD AND d.CHIT_ID = h.CHIT_ID AND d.ISDEL = '0'
INNER JOIN chitdescriptioninfo des
    ON des.COMPANY_CD = h.COMPANY_CD AND des.CHIT_ID = h.CHIT_ID AND des.ISDEL = '0'
WHERE h.COMPANY_CD = @cd AND h.ISDEL = '0' AND h.INPUT_TYPE = 'LOCK'
  AND h.LOCK_STEP_CODE = 'COGS_SUMMARY' AND h.CHIT_YMD = @to_gk
  AND des.DESCRIPTION LIKE '%EXP_TO_154_ONLY%';

CALL sp_gk2b_assert_eq('T1_ONLY_154_VS_62X', '0', CAST(@bad_lines AS CHAR));

SELECT IFNULL(SUM(CASE
    WHEN (TRIM(d.DEBIT) LIKE '155%' AND TRIM(d.CREDIT) LIKE '154%')
      OR (TRIM(d.DEBIT) LIKE '632%' AND TRIM(d.CREDIT) LIKE '154%')
      OR (TRIM(d.DEBIT) LIKE '632%' AND TRIM(d.CREDIT) LIKE '155%')
    THEN 1 ELSE 0 END), 0)
  INTO @forbidden_xfer
FROM chitinfo h
INNER JOIN chitdetailinfo d ON d.COMPANY_CD = h.COMPANY_CD AND d.CHIT_ID = h.CHIT_ID AND d.ISDEL = '0'
INNER JOIN chitdescriptioninfo des
    ON des.COMPANY_CD = h.COMPANY_CD AND des.CHIT_ID = h.CHIT_ID AND des.ISDEL = '0'
WHERE h.COMPANY_CD = @cd AND h.ISDEL = '0' AND h.INPUT_TYPE = 'LOCK'
  AND h.LOCK_STEP_CODE = 'COGS_SUMMARY' AND h.CHIT_YMD = @to_gk
  AND des.DESCRIPTION LIKE '%EXP_TO_154_ONLY%';

CALL sp_gk2b_assert_eq('T1_NO_154_TO_155_632', '0', CAST(@forbidden_xfer AS CHAR));

-- ---------------------------------------------------------------------------
-- T4: unlock cross-engine bị từ chối
-- ---------------------------------------------------------------------------
DROP PROCEDURE IF EXISTS sp_gk2b_try_unlock;
DELIMITER $$
CREATE PROCEDURE sp_gk2b_try_unlock(
    IN p_cd VARCHAR(20),
    IN p_ym CHAR(6),
    IN p_user VARCHAR(50),
    IN p_engine VARCHAR(10),
    OUT p_rejected INT
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        SET p_rejected = 1;
    END;
    SET p_rejected = 0;
    CALL sp_period_lock_cogs_unlock(p_cd, p_ym, 'test cross unlock', p_user, p_engine);
END$$
DELIMITER ;

-- Nếu kỳ G-K: BI unlock phải reject
CALL sp_gk2b_try_unlock(@cd, @ym_gk, @user, 'BI', @unlock_bi_rej);
CALL sp_gk2b_assert_eq('T4_BI_UNLOCK_ON_GK_REJECT', '1', CAST(IFNULL(@unlock_bi_rej, 0) AS CHAR));

-- CT vẫn nguyên sau unlock reject
SELECT COUNT(DISTINCT h.CHIT_ID) INTO @gk_chit_cnt_after_unlock
FROM chitinfo h
WHERE h.COMPANY_CD = @cd AND h.ISDEL = '0' AND h.INPUT_TYPE = 'LOCK'
  AND h.LOCK_STEP_CODE = 'COGS_SUMMARY' AND h.CHIT_YMD = @to_gk;
CALL sp_gk2b_assert_eq('T4_CHIT_AFTER_BAD_UNLOCK', CAST(@gk_chit_cnt_before AS CHAR), CAST(@gk_chit_cnt_after_unlock AS CHAR));

-- ---------------------------------------------------------------------------
-- T5: validate không claim (engine không đổi khi chỉ validate rule trùng)
-- ---------------------------------------------------------------------------
SET @eng_v1 = NULL;
SELECT IFNULL(COST_ENGINE, 'NONE') INTO @eng_v1
FROM production_cost_period WHERE COMPANY_CD = @cd AND PERIOD_YM = @ym_gk AND ISDEL = '0' LIMIT 1;

-- validate EXP khi đã GK: OK và không đổi
DROP PROCEDURE IF EXISTS sp_gk2b_try_validate;
DELIMITER $$
CREATE PROCEDURE sp_gk2b_try_validate(
    IN p_cd VARCHAR(20), IN p_ym CHAR(6), IN p_rule VARCHAR(50), IN p_user VARCHAR(50),
    OUT p_ok INT
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN SET p_ok = 0; END;
    SET p_ok = 1;
    CALL sp_period_lock_cogs_validate(p_cd, p_ym, p_rule, p_user);
END$$
DELIMITER ;

CALL sp_gk2b_try_validate(@cd, @ym_gk, 'EXP_TO_154_ONLY', @user, @val_ok);
SELECT IFNULL(COST_ENGINE, 'NONE') INTO @eng_v2
FROM production_cost_period WHERE COMPANY_CD = @cd AND PERIOD_YM = @ym_gk AND ISDEL = '0' LIMIT 1;
CALL sp_gk2b_assert_eq('T5_VALIDATE_NO_ENGINE_CHANGE', IFNULL(@eng_v1, 'NONE'), IFNULL(@eng_v2, 'NONE'));

-- ---------------------------------------------------------------------------
-- Báo cáo
-- ---------------------------------------------------------------------------
SELECT * FROM tmp_gk2b_assert ORDER BY ASSERT_NAME;
SELECT
    SUM(CASE WHEN PASS_FAIL = 'PASS' THEN 1 ELSE 0 END) AS pass_cnt,
    SUM(CASE WHEN PASS_FAIL = 'FAIL' THEN 1 ELSE 0 END) AS fail_cnt,
    COUNT(*) AS total_cnt
FROM tmp_gk2b_assert;

DROP PROCEDURE IF EXISTS sp_gk2b_try_bi_on_gk;
DROP PROCEDURE IF EXISTS sp_gk2b_try_unlock;
DROP PROCEDURE IF EXISTS sp_gk2b_try_validate;
DROP PROCEDURE IF EXISTS sp_gk2b_assert_eq;
