DROP PROCEDURE IF EXISTS `getuser_setting_info`;
DROP PROCEDURE IF EXISTS `setuser_setting_info`;
DROP PROCEDURE IF EXISTS `deleteuser_setting_info`;
DROP PROCEDURE IF EXISTS `sp_user_setting_info_get`;
DROP PROCEDURE IF EXISTS `sp_user_setting_info_set`;
DROP PROCEDURE IF EXISTS `sp_user_setting_info_delete`;

DELIMITER //

CREATE PROCEDURE `sp_user_setting_info_get`(
    IN `p_COMPANY_CD` VARCHAR(20),
    IN `p_USER_ID` VARCHAR(20),
    IN `p_KEY_NAME` VARCHAR(100)
)
BEGIN
    SELECT
        COMPANY_CD,
        USER_ID,
        KEY_NAME,
        VALUE,
        NOTE,
        UPDATE_DT
    FROM user_setting_info
    WHERE (
            (COMPANY_CD = IFNULL(p_COMPANY_CD, '') AND USER_ID = IFNULL(p_USER_ID, ''))
         OR (COMPANY_CD = IFNULL(p_COMPANY_CD, '') AND USER_ID = '')
         OR (COMPANY_CD = '' AND USER_ID = '')
          )
      AND (
            p_KEY_NAME IS NULL
         OR p_KEY_NAME = ''
         OR KEY_NAME = p_KEY_NAME
          )
    ORDER BY
        KEY_NAME,
        CASE
            WHEN COMPANY_CD = IFNULL(p_COMPANY_CD, '') AND USER_ID = IFNULL(p_USER_ID, '') THEN 1
            WHEN COMPANY_CD = IFNULL(p_COMPANY_CD, '') AND USER_ID = '' THEN 2
            ELSE 3
        END;
END//

CREATE PROCEDURE `sp_user_setting_info_set`(
    IN `p_COMPANY_CD` VARCHAR(20),
    IN `p_USER_ID` VARCHAR(20),
    IN `p_KEY_NAME` VARCHAR(100),
    IN `p_VALUE` VARCHAR(250),
    IN `p_NOTE` VARCHAR(250)
)
BEGIN
    INSERT INTO user_setting_info (
        COMPANY_CD,
        USER_ID,
        KEY_NAME,
        VALUE,
        NOTE,
        UPDATE_DT
    )
    VALUES (
        IFNULL(p_COMPANY_CD, ''),
        IFNULL(p_USER_ID, ''),
        IFNULL(p_KEY_NAME, ''),
        IFNULL(p_VALUE, ''),
        IFNULL(p_NOTE, ''),
        NOW()
    )
    ON DUPLICATE KEY UPDATE
        VALUE = VALUES(VALUE),
        NOTE = VALUES(NOTE),
        UPDATE_DT = NOW();
END//

CREATE PROCEDURE `sp_user_setting_info_delete`(
    IN `p_COMPANY_CD` VARCHAR(20),
    IN `p_USER_ID` VARCHAR(20),
    IN `p_KEY_NAME` VARCHAR(100)
)
BEGIN
    DELETE FROM user_setting_info
    WHERE COMPANY_CD = IFNULL(p_COMPANY_CD, '')
      AND USER_ID = IFNULL(p_USER_ID, '')
      AND KEY_NAME = IFNULL(p_KEY_NAME, '');
END//

DELIMITER ;
