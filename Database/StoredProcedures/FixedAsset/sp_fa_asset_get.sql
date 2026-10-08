DROP PROCEDURE IF EXISTS `sp_fa_asset_get`;
DELIMITER //

CREATE PROCEDURE `sp_fa_asset_get`(
    IN `p_COMPANY_CD` VARCHAR(20),
    IN `p_ASSET_ID` BIGINT,
    IN `p_STATUS` VARCHAR(20),
    IN `p_ACC_CD` VARCHAR(20)
)
BEGIN
    SELECT
        A.ASSET_ID,
        A.COMPANY_CD,
        A.ASSET_CD,
        A.ASSET_NM,
        A.ACC_CD,
        IFNULL(B.ACCTITLE_NM_VIET, '') AS ACC_NM_VIET,
        IFNULL(B.ACCTITLE_NM_ENG, '') AS ACC_NM_ENG,
        IFNULL(B.ACCTITLE_NM_KOR, '') AS ACC_NM_KOR,
        IFNULL(B.ACCTITLE_NM_CHINA, '') AS ACC_NM_CHINA,
        A.USE_DEPT_CD,
        A.RECEIVE_YMD,
        A.USE_START_YMD,
        A.DEPRE_START_YM,
        A.DEPRE_END_YM,
        A.USEFUL_LIFE_MONTH,
        A.NORMAL_MONTH_COUNT,
        A.ORIGINAL_AMT,
        A.ACCUM_DEPRE_AMT,
        A.REMAIN_DEPRE_AMT,
        A.FIRST_DEPRE_AMT,
        A.NORMAL_DEPRE_AMT,
        A.LAST_DEPRE_AMT,
        A.ACQ_CHITINFO_ID,
        A.ACQ_CHITDETAIL_ID,
        A.ACQ_CHIT_NO,
        A.STATUS,
        A.NOTE
    FROM fa_asset A
    LEFT JOIN acclist_info B
        ON A.COMPANY_CD = B.COMPANY_CD
       AND A.ACC_CD = B.ACC_CD
       AND IFNULL(B.ISDEL, '0') <> '1'
       AND IFNULL(B.ISABLEINPUT, '0') = '1'
    WHERE A.COMPANY_CD = p_COMPANY_CD
      AND IFNULL(A.ISDEL, '0') = '0'

      AND (
            p_ASSET_ID IS NULL
            OR A.ASSET_ID = p_ASSET_ID
          )

      AND (
            p_ASSET_ID IS NOT NULL
            OR p_STATUS IS NULL
            OR p_STATUS = ''
            OR A.STATUS = p_STATUS
          )

      AND (
            p_ASSET_ID IS NOT NULL
            OR p_ACC_CD IS NULL
            OR p_ACC_CD = ''
            OR A.ACC_CD = p_ACC_CD
          )

    ORDER BY A.ASSET_CD;
END//

DELIMITER ;
