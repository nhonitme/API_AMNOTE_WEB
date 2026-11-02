-- =============================================================================
-- G-K 2B PREFLIGHT — hard-stop trước mọi DDL/SP
-- =============================================================================
-- Bắt buộc:
--   SET @GK_2B_ALLOW_DB = '<ten_db_dev_da_duyet>';
--   USE <ten_db_dev_da_duyet>;
--   SOURCE 20261102_2b_00_preflight_dev.sql;
-- Sau đó mới SOURCE install / 01..04.
-- CẤM production.
-- =============================================================================

DROP PROCEDURE IF EXISTS `sp_pc_2b_require_allowed_db`;

DELIMITER $$
CREATE PROCEDURE `sp_pc_2b_require_allowed_db`()
BEGIN
    DECLARE v_db VARCHAR(64);
    DECLARE v_allow VARCHAR(64);
    DECLARE v_msg VARCHAR(255);

    SET v_db = DATABASE();
    SET v_allow = NULLIF(TRIM(IFNULL(@GK_2B_ALLOW_DB, '')), '');

    IF v_db IS NULL OR v_db = '' THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'GK2B PREFLIGHT FAIL: chua USE database';
    END IF;

    IF v_allow IS NULL THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'GK2B PREFLIGHT FAIL: thieu @GK_2B_ALLOW_DB (ten DB DEV da duyet)';
    END IF;

    IF v_db <> v_allow THEN
        SET v_msg = LEFT(CONCAT('GK2B PREFLIGHT FAIL: DB=', v_db, ' <> allow=', v_allow), 255);
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = v_msg;
    END IF;

    IF v_db IN ('am_web_prod', 'am_web_production', 'production', 'prod') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'GK2B PREFLIGHT FAIL: ten DB bi chan (production-like)';
    END IF;
END$$
DELIMITER ;

CALL sp_pc_2b_require_allowed_db();

SELECT 'GK2B PREFLIGHT PASS' AS status,
       DATABASE() AS database_ok,
       @GK_2B_ALLOW_DB AS allowed_database,
       NOW() AS passed_at;
