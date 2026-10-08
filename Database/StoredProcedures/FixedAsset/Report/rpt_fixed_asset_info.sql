-- =============================================================================
-- rpt_fixed_asset_info — danh mục TSCĐ (master grid PDF / DEFAULT_GRID)
-- Deploy trên company DB.
-- Tham chiếu: rpt_inventory_opening_info / sp_fa_asset_get
-- =============================================================================

DROP PROCEDURE IF EXISTS `rpt_fixed_asset_info`;
DELIMITER //

CREATE PROCEDURE `rpt_fixed_asset_info`(
    IN `p_COMPANY_CD` VARCHAR(20),
    IN `p_ASSET_ID` BIGINT,
    IN `p_ASSET_CD` VARCHAR(50),
    IN `p_LANGUAGE` VARCHAR(20)
)
BEGIN
    SET @rownum := 0;

    SELECT
        (@rownum := @rownum + 1) AS ROW_NO,
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
        -- Empty placeholder: API fills localized FA_STATUS into STATUS_TEXT.
        -- Prefer VARCHAR so MySQL does not yield MaxLength=0 for '' AS col.
        CAST('' AS CHAR(200)) AS STATUS_TEXT,
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
            OR p_ASSET_ID = 0
            OR A.ASSET_ID = p_ASSET_ID
          )
      AND (
            p_ASSET_CD IS NULL
            OR p_ASSET_CD = ''
            OR A.ASSET_CD = p_ASSET_CD
          )
    ORDER BY A.ASSET_CD;
END//

DELIMITER ;
