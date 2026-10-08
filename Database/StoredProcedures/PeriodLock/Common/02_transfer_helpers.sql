-- Transfer helpers for Period Lock steps 2 & 3 (MySQL 5.6+)

DELIMITER $$

DROP PROCEDURE IF EXISTS `sp_period_lock_resolve_posting_acc`$$
CREATE PROCEDURE `sp_period_lock_resolve_posting_acc`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_ACC_PREFIX VARCHAR(20),
    OUT p_ACC_CD VARCHAR(20)
)
BEGIN
    SET p_ACC_CD = NULL;

    SELECT ACC_CD
      INTO p_ACC_CD
    FROM acclist_info
    WHERE COMPANY_CD = p_COMPANY_CD
      AND IFNULL(ISDEL, '0') = '0'
      AND IFNULL(ISABLEINPUT, '0') = '1'
      AND ACC_CD LIKE CONCAT(TRIM(p_ACC_PREFIX), '%')
    ORDER BY ACC_CD ASC
    LIMIT 1;

    IF IFNULL(p_ACC_CD, '') = '' THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Khong tim thay TK hach toan ISABLEINPUT=1';
    END IF;
END$$

DROP PROCEDURE IF EXISTS `sp_period_lock_lock_voucher_begin`$$
CREATE PROCEDURE `sp_period_lock_lock_voucher_begin`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6),
    IN p_LOCK_STEP_CODE VARCHAR(50),
    IN p_DESCRIPTION VARCHAR(500),
    IN p_USER_ID VARCHAR(50),
    IN p_CODE_TAG VARCHAR(10),
    OUT p_CHIT_ID BIGINT,
    OUT p_CHIT_CD VARCHAR(30),
    OUT p_CHIT_YMD_CHAR CHAR(8)
)
BEGIN
    DECLARE v_chit_no VARCHAR(50);
    DECLARE v_chit_ymd DATE;

    CALL sp_period_lock_fa_prepaid_last_day(p_PERIOD_YM, v_chit_ymd);
    SET p_CHIT_YMD_CHAR = DATE_FORMAT(v_chit_ymd, '%Y%m%d');
    CALL sp_period_lock_fa_prepaid_next_chit_no(p_COMPANY_CD, v_chit_ymd, v_chit_no);
    SET p_CHIT_CD = LEFT(CONCAT('C', DATE_FORMAT(NOW(6), '%Y%m%d%H%i%s%f'), IFNULL(p_CODE_TAG, 'LK')), 30);

    INSERT INTO chitinfo (
        COMPANY_CD, CHIT_CD, CHIT_NO, CHIT_YMD, CHIT_TYPE, INPUT_TYPE,
        LOCK_STEP_CODE, AMOUNT, ISDEL, CREATE_BY, UPDATE_BY
    ) VALUES (
        p_COMPANY_CD, p_CHIT_CD, LEFT(v_chit_no, 50), p_CHIT_YMD_CHAR, 'OT', 'LOCK',
        p_LOCK_STEP_CODE, 0, '0', p_USER_ID, p_USER_ID
    );
    SET p_CHIT_ID = LAST_INSERT_ID();

    INSERT INTO chitinfo_ext (
        COMPANY_CD, CHIT_ID, CHIT_CD, IS_LOCK, ISEXCEL, IS_CONFIRMED, IS_PAYMENT,
        ISDEL, CREATE_BY, UPDATE_BY
    ) VALUES (
        p_COMPANY_CD, p_CHIT_ID, LEFT(p_CHIT_CD, 20), '0', '0', '0', '0',
        '0', p_USER_ID, p_USER_ID
    );

    INSERT INTO chitdescriptioninfo (
        COMPANY_CD, CHIT_ID, CHIT_CD, DESC_CD, LANG_TYPE, DESCRIPTION,
        ISDEL, CREATE_BY, UPDATE_BY
    ) VALUES (
        p_COMPANY_CD, p_CHIT_ID, LEFT(p_CHIT_CD, 20), LEFT(p_CHIT_CD, 20), 'VIET', p_DESCRIPTION,
        '0', p_USER_ID, p_USER_ID
    );
END$$

DROP PROCEDURE IF EXISTS `sp_period_lock_lock_voucher_line`$$
CREATE PROCEDURE `sp_period_lock_lock_voucher_line`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_CHIT_ID BIGINT,
    IN p_CHIT_CD VARCHAR(30),
    IN p_CHIT_YMD_CHAR CHAR(8),
    IN p_DEBIT VARCHAR(20),
    IN p_CREDIT VARCHAR(20),
    IN p_AMOUNT DECIMAL(18,2),
    IN p_SORT INT,
    IN p_DEPARTMENT_ID BIGINT,
    IN p_DESCRIPTION VARCHAR(500),
    IN p_USER_ID VARCHAR(50)
)
BEGIN
    DECLARE v_detail_cd VARCHAR(30);
    DECLARE v_detail_id BIGINT;
    DECLARE v_department_cd VARCHAR(20) DEFAULT NULL;

    SET v_detail_cd = LEFT(CONCAT('D', DATE_FORMAT(NOW(6), '%Y%m%d%H%i%s%f'), LPAD(p_SORT, 6, '0')), 30);

    INSERT INTO chitdetailinfo (
        COMPANY_CD, CHIT_ID, CHIT_CD, CHITDETAIL_CD, CHIT_YMD, DEBIT, CREDIT, AMOUNT,
        FC_AMOUNT, FC_RATE, SORT, ISDEL, CREATE_BY, UPDATE_BY
    ) VALUES (
        p_COMPANY_CD, p_CHIT_ID, p_CHIT_CD, v_detail_cd, p_CHIT_YMD_CHAR,
        p_DEBIT, p_CREDIT, p_AMOUNT, 0, 0, p_SORT, '0', p_USER_ID, p_USER_ID
    );
    SET v_detail_id = LAST_INSERT_ID();

    INSERT INTO chitdetaildescriptioninfo (
        COMPANY_CD, CHITDETAIL_ID, CHITDETAIL_CD, DESC_CD, LANG_TYPE, DESCRIPTION,
        ISDEL, CREATE_BY, UPDATE_BY
    ) VALUES (
        p_COMPANY_CD, v_detail_id, LEFT(v_detail_cd, 20), LEFT(v_detail_cd, 20), 'VIET', p_DESCRIPTION,
        '0', p_USER_ID, p_USER_ID
    );

    IF p_DEPARTMENT_ID IS NOT NULL AND p_DEPARTMENT_ID > 0 THEN
        SELECT dep.DEPARTMENT_CD
          INTO v_department_cd
        FROM department_info dep
        WHERE dep.DEPARTMENT_ID = p_DEPARTMENT_ID
          AND dep.COMPANY_CD = p_COMPANY_CD
        LIMIT 1;
    END IF;

    INSERT INTO chitdetailinfo_ext (
        COMPANY_CD, CHITDETAIL_ID, CHITDETAIL_CD, DEPARTMENT_ID, DEPARTMENT_CD,
        ISDEL, CREATE_BY, UPDATE_BY
    ) VALUES (
        p_COMPANY_CD, v_detail_id, LEFT(v_detail_cd, 20), p_DEPARTMENT_ID, v_department_cd,
        '0', p_USER_ID, p_USER_ID
    );
END$$

DROP PROCEDURE IF EXISTS `sp_period_lock_lock_voucher_end`$$
CREATE PROCEDURE `sp_period_lock_lock_voucher_end`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_CHIT_ID BIGINT,
    IN p_TOTAL_AMOUNT DECIMAL(18,2),
    IN p_USER_ID VARCHAR(50)
)
BEGIN
    UPDATE chitinfo
    SET AMOUNT = IFNULL(p_TOTAL_AMOUNT, 0),
        UPDATE_BY = p_USER_ID
    WHERE CHIT_ID = p_CHIT_ID
      AND COMPANY_CD = p_COMPANY_CD;
END$$

DROP PROCEDURE IF EXISTS `sp_period_lock_post_xfer_group`$$
CREATE PROCEDURE `sp_period_lock_post_xfer_group`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6),
    IN p_LOCK_STEP_CODE VARCHAR(50),
    IN p_TRANSFER_CODE VARCHAR(30),
    IN p_DESCRIPTION VARCHAR(500),
    IN p_USER_ID VARCHAR(50),
    IN p_CODE_TAG VARCHAR(10)
)
proc: BEGIN
    DECLARE v_done INT DEFAULT 0;
    DECLARE v_debit VARCHAR(20);
    DECLARE v_credit VARCHAR(20);
    DECLARE v_amount DECIMAL(18,2);
    DECLARE v_dept_id BIGINT;
    DECLARE v_chit_id BIGINT;
    DECLARE v_chit_cd VARCHAR(30);
    DECLARE v_chit_ymd CHAR(8);
    DECLARE v_sort INT DEFAULT 0;
    DECLARE v_total DECIMAL(18,2) DEFAULT 0;
    DECLARE v_line_count INT DEFAULT 0;

    DECLARE cur_lines CURSOR FOR
        SELECT DEBIT_ACC, CREDIT_ACC, AMOUNT, DEPARTMENT_ID
        FROM tmp_xfer_lines
        WHERE TRANSFER_CODE = p_TRANSFER_CODE
          AND AMOUNT > 0
        ORDER BY DEPARTMENT_ID, DEBIT_ACC, CREDIT_ACC;

    DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_done = 1;

    SELECT IFNULL(COUNT(*), 0)
      INTO v_line_count
    FROM tmp_xfer_lines
    WHERE TRANSFER_CODE = p_TRANSFER_CODE
      AND AMOUNT > 0;

    IF v_line_count = 0 THEN
        LEAVE proc;
    END IF;

    CALL sp_period_lock_lock_voucher_begin(
        p_COMPANY_CD, p_PERIOD_YM, p_LOCK_STEP_CODE, p_DESCRIPTION, p_USER_ID, p_CODE_TAG,
        v_chit_id, v_chit_cd, v_chit_ymd
    );

    OPEN cur_lines;
    line_loop: LOOP
        SET v_done = 0;
        FETCH cur_lines INTO v_debit, v_credit, v_amount, v_dept_id;
        IF v_done = 1 THEN
            LEAVE line_loop;
        END IF;

        SET v_sort = v_sort + 1;
        SET v_total = v_total + IFNULL(v_amount, 0);

        CALL sp_period_lock_lock_voucher_line(
            p_COMPANY_CD, v_chit_id, v_chit_cd, v_chit_ymd,
            v_debit, v_credit, v_amount, v_sort, v_dept_id,
            p_DESCRIPTION, p_USER_ID
        );
    END LOOP;
    CLOSE cur_lines;

    CALL sp_period_lock_lock_voucher_end(p_COMPANY_CD, v_chit_id, v_total, p_USER_ID);
END proc$$

DELIMITER ;
