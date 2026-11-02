-- G-K 2B — Exclusive COST_ENGINE (DEV/TEST)
-- MySQL 5.6 / 8.0 · CẤM production
-- Transaction: KHÔNG START/COMMIT trong claim (tránh commit nhầm TX của caller).
-- Mutex: GET_LOCK theo COMPANY_CD+PERIOD_YM + UPDATE có điều kiện / UNIQUE race.

SOURCE 20261102_2b_00_guard_inline.sql;

DELIMITER $$

DROP PROCEDURE IF EXISTS `sp_pc_engine_check`$$
CREATE PROCEDURE `sp_pc_engine_check`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6),
    IN p_COST_ENGINE VARCHAR(10),
    IN p_COGS_RULE VARCHAR(50)
)
proc: BEGIN
    DECLARE v_engine VARCHAR(10) DEFAULT 'NONE';
    DECLARE v_tbl INT DEFAULT 0;

    IF p_COST_ENGINE NOT IN ('GK', 'BI') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'COST_ENGINE phai la GK hoac BI';
    END IF;

    SELECT COUNT(*)
      INTO v_tbl
    FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'production_cost_period';

    IF IFNULL(v_tbl, 0) = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Thieu bang production_cost_period — chay migration 2B tren DEV';
    END IF;

    SELECT IFNULL(COST_ENGINE, 'NONE')
      INTO v_engine
    FROM production_cost_period
    WHERE COMPANY_CD = p_COMPANY_CD
      AND PERIOD_YM = p_PERIOD_YM
      AND ISDEL = '0'
    LIMIT 1;

    SET v_engine = IFNULL(v_engine, 'NONE');

    IF v_engine <> 'NONE' AND v_engine <> p_COST_ENGINE THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Ky nay da thuoc engine khac (GK/BI exclusive)';
    END IF;
END proc$$

-- Chỉ đọc: preview/validate — không chiếm quyền
DROP PROCEDURE IF EXISTS `sp_pc_engine_assert_for_cogs`$$
CREATE PROCEDURE `sp_pc_engine_assert_for_cogs`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6),
    IN p_RULE_CODE VARCHAR(50),
    IN p_USER_ID VARCHAR(50)
)
BEGIN
    -- p_USER_ID giữ chữ ký cũ (C#); không ghi DB ở đây
    IF p_RULE_CODE = 'EXP_TO_154_ONLY' THEN
        CALL sp_pc_engine_check(p_COMPANY_CD, p_PERIOD_YM, 'GK', 'EXP_TO_154_ONLY');
    ELSEIF p_RULE_CODE IN ('154_TO_155', '154_TO_155_TO_632', '154_TO_632') THEN
        CALL sp_pc_engine_check(p_COMPANY_CD, p_PERIOD_YM, 'BI', p_RULE_CODE);
    ELSE
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Rule ket chuyen gia von khong hop le';
    END IF;
END$$

DROP PROCEDURE IF EXISTS `sp_pc_engine_claim`$$
CREATE PROCEDURE `sp_pc_engine_claim`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6),
    IN p_COST_ENGINE VARCHAR(10),
    IN p_COGS_RULE VARCHAR(50),
    IN p_USER_ID VARCHAR(50)
)
proc: BEGIN
    DECLARE v_engine VARCHAR(10) DEFAULT 'NONE';
    DECLARE v_period_id BIGINT DEFAULT NULL;
    DECLARE v_from CHAR(8);
    DECLARE v_to CHAR(8);
    DECLARE v_tbl INT DEFAULT 0;
    DECLARE v_lock_name VARCHAR(64);
    DECLARE v_got_lock INT DEFAULT 0;
    DECLARE v_rows INT DEFAULT 0;

    IF p_COST_ENGINE NOT IN ('GK', 'BI') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'COST_ENGINE phai la GK hoac BI';
    END IF;

    SELECT COUNT(*)
      INTO v_tbl
    FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'production_cost_period';

    IF IFNULL(v_tbl, 0) = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Thieu bang production_cost_period — chay migration 2B tren DEV';
    END IF;

    SET v_from = CONCAT(p_PERIOD_YM, '01');
    SET v_to = DATE_FORMAT(LAST_DAY(STR_TO_DATE(CONCAT(p_PERIOD_YM, '01'), '%Y%m%d')), '%Y%m%d');
    SET v_lock_name = LEFT(CONCAT('pc_eng_', p_COMPANY_CD, '_', p_PERIOD_YM), 64);

    SET v_got_lock = GET_LOCK(v_lock_name, 15);
    IF IFNULL(v_got_lock, 0) <> 1 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Khong lay duoc exclusive lock COST_ENGINE (timeout)';
    END IF;

    BEGIN
        DECLARE EXIT HANDLER FOR SQLEXCEPTION
        BEGIN
            DO RELEASE_LOCK(v_lock_name);
            RESIGNAL;
        END;

        SELECT PERIOD_COST_ID, IFNULL(COST_ENGINE, 'NONE')
          INTO v_period_id, v_engine
        FROM production_cost_period
        WHERE COMPANY_CD = p_COMPANY_CD
          AND PERIOD_YM = p_PERIOD_YM
          AND ISDEL = '0'
        LIMIT 1;

        IF v_period_id IS NULL THEN
            BEGIN
                DECLARE CONTINUE HANDLER FOR 1062
                BEGIN
                    SELECT PERIOD_COST_ID, IFNULL(COST_ENGINE, 'NONE')
                      INTO v_period_id, v_engine
                    FROM production_cost_period
                    WHERE COMPANY_CD = p_COMPANY_CD
                      AND PERIOD_YM = p_PERIOD_YM
                      AND ISDEL = '0'
                    LIMIT 1;
                END;

                INSERT INTO production_cost_period (
                    COMPANY_CD, PERIOD_YM, FROM_YMD, TO_YMD, STATUS,
                    COST_ENGINE, COGS_RULE, ISDEL, CREATE_BY, CREATE_AT, UPDATE_BY, UPDATE_AT
                ) VALUES (
                    p_COMPANY_CD, p_PERIOD_YM, v_from, v_to, 'IN_PROGRESS',
                    p_COST_ENGINE, p_COGS_RULE, '0', p_USER_ID, NOW(), p_USER_ID, NOW()
                );
            END;

            IF v_period_id IS NULL THEN
                DO RELEASE_LOCK(v_lock_name);
                LEAVE proc;
            END IF;
        END IF;

        SET v_engine = IFNULL(v_engine, 'NONE');

        IF v_engine <> 'NONE' AND v_engine <> p_COST_ENGINE THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Ky nay da thuoc engine khac (GK/BI exclusive)';
        END IF;

        -- Điều kiện WHERE: không chiếm nhầm nếu engine đổi giữa check/claim
        UPDATE production_cost_period
        SET COST_ENGINE = p_COST_ENGINE,
            COGS_RULE = p_COGS_RULE,
            STATUS = IF(STATUS = 'OPEN', 'IN_PROGRESS', STATUS),
            UPDATE_BY = p_USER_ID,
            UPDATE_AT = NOW()
        WHERE PERIOD_COST_ID = v_period_id
          AND ISDEL = '0'
          AND (IFNULL(COST_ENGINE, 'NONE') = 'NONE' OR COST_ENGINE = p_COST_ENGINE);

        SET v_rows = ROW_COUNT();
        IF IFNULL(v_rows, 0) = 0 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Ky nay da thuoc engine khac (GK/BI exclusive)';
        END IF;

        DO RELEASE_LOCK(v_lock_name);
    END;
END proc$$

DROP PROCEDURE IF EXISTS `sp_pc_engine_claim_for_cogs`$$
CREATE PROCEDURE `sp_pc_engine_claim_for_cogs`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6),
    IN p_RULE_CODE VARCHAR(50),
    IN p_USER_ID VARCHAR(50)
)
BEGIN
    IF p_RULE_CODE = 'EXP_TO_154_ONLY' THEN
        CALL sp_pc_engine_claim(p_COMPANY_CD, p_PERIOD_YM, 'GK', 'EXP_TO_154_ONLY', p_USER_ID);
    ELSEIF p_RULE_CODE IN ('154_TO_155', '154_TO_155_TO_632', '154_TO_632') THEN
        CALL sp_pc_engine_claim(p_COMPANY_CD, p_PERIOD_YM, 'BI', p_RULE_CODE, p_USER_ID);
    ELSE
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Rule ket chuyen gia von khong hop le';
    END IF;
END$$

DROP PROCEDURE IF EXISTS `sp_pc_engine_release_cogs`$$
CREATE PROCEDURE `sp_pc_engine_release_cogs`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6),
    IN p_USER_ID VARCHAR(50),
    IN p_EXPECTED_ENGINE VARCHAR(10)
)
proc: BEGIN
    DECLARE v_tbl INT DEFAULT 0;
    DECLARE v_engine VARCHAR(10) DEFAULT 'NONE';
    DECLARE v_rows INT DEFAULT 0;

    SELECT COUNT(*)
      INTO v_tbl
    FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'production_cost_period';

    IF IFNULL(v_tbl, 0) = 0 THEN
        LEAVE proc;
    END IF;

    IF IFNULL(p_EXPECTED_ENGINE, '') NOT IN ('GK', 'BI') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Unlock COST_ENGINE can p_EXPECTED_ENGINE = GK|BI';
    END IF;

    SELECT IFNULL(COST_ENGINE, 'NONE')
      INTO v_engine
    FROM production_cost_period
    WHERE COMPANY_CD = p_COMPANY_CD
      AND PERIOD_YM = p_PERIOD_YM
      AND ISDEL = '0'
    LIMIT 1;

    SET v_engine = IFNULL(v_engine, 'NONE');

    IF v_engine = 'NONE' THEN
        LEAVE proc;
    END IF;

    IF v_engine <> p_EXPECTED_ENGINE THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Unlock bi tu choi: COST_ENGINE khong khop expected engine';
    END IF;

    UPDATE production_cost_period
    SET COST_ENGINE = 'NONE',
        COGS_RULE = NULL,
        UPDATE_BY = p_USER_ID,
        UPDATE_AT = NOW()
    WHERE COMPANY_CD = p_COMPANY_CD
      AND PERIOD_YM = p_PERIOD_YM
      AND ISDEL = '0'
      AND COST_ENGINE = p_EXPECTED_ENGINE;

    SET v_rows = ROW_COUNT();
    IF IFNULL(v_rows, 0) = 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Unlock bi tu choi: khong release duoc COST_ENGINE';
    END IF;
END proc$$

DELIMITER ;
