DELIMITER $$

DROP PROCEDURE IF EXISTS `sp_period_lock_fa_prepaid_unlock`$$
CREATE PROCEDURE `sp_period_lock_fa_prepaid_unlock`(
    IN p_COMPANY_CD VARCHAR(20),
    IN p_PERIOD_YM CHAR(6),
    IN p_REASON VARCHAR(500),
    IN p_USER_ID VARCHAR(50)
)
BEGIN
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    DELETE dd
    FROM chitdetaildescriptioninfo dd
    INNER JOIN chitdetailinfo d
        ON d.CHITDETAIL_ID = dd.CHITDETAIL_ID
       AND d.COMPANY_CD = dd.COMPANY_CD
    INNER JOIN fa_asset_depre_posted p
        ON p.CHITINFO_ID = d.CHIT_ID
       AND p.COMPANY_CD = d.COMPANY_CD
    WHERE p.COMPANY_CD = p_COMPANY_CD
      AND p.DEPRE_YM = p_PERIOD_YM
      AND IFNULL(p.CANCEL_YN, 'N') = 'N'
      AND p.CHITINFO_ID IS NOT NULL;

    DELETE ext
    FROM chitdetailinfo_ext ext
    INNER JOIN chitdetailinfo d
        ON d.CHITDETAIL_ID = ext.CHITDETAIL_ID
       AND d.COMPANY_CD = ext.COMPANY_CD
    INNER JOIN fa_asset_depre_posted p
        ON p.CHITINFO_ID = d.CHIT_ID
       AND p.COMPANY_CD = d.COMPANY_CD
    WHERE p.COMPANY_CD = p_COMPANY_CD
      AND p.DEPRE_YM = p_PERIOD_YM
      AND IFNULL(p.CANCEL_YN, 'N') = 'N'
      AND p.CHITINFO_ID IS NOT NULL;

    DELETE d
    FROM chitdetailinfo d
    INNER JOIN fa_asset_depre_posted p
        ON p.CHITINFO_ID = d.CHIT_ID
       AND p.COMPANY_CD = d.COMPANY_CD
    WHERE p.COMPANY_CD = p_COMPANY_CD
      AND p.DEPRE_YM = p_PERIOD_YM
      AND IFNULL(p.CANCEL_YN, 'N') = 'N'
      AND p.CHITINFO_ID IS NOT NULL;

    DELETE desc_h
    FROM chitdescriptioninfo desc_h
    INNER JOIN fa_asset_depre_posted p
        ON p.CHITINFO_ID = desc_h.CHIT_ID
       AND p.COMPANY_CD = desc_h.COMPANY_CD
    WHERE p.COMPANY_CD = p_COMPANY_CD
      AND p.DEPRE_YM = p_PERIOD_YM
      AND IFNULL(p.CANCEL_YN, 'N') = 'N'
      AND p.CHITINFO_ID IS NOT NULL;

    DELETE ext_h
    FROM chitinfo_ext ext_h
    INNER JOIN fa_asset_depre_posted p
        ON p.CHITINFO_ID = ext_h.CHIT_ID
       AND p.COMPANY_CD = ext_h.COMPANY_CD
    WHERE p.COMPANY_CD = p_COMPANY_CD
      AND p.DEPRE_YM = p_PERIOD_YM
      AND IFNULL(p.CANCEL_YN, 'N') = 'N'
      AND p.CHITINFO_ID IS NOT NULL;

    DELETE h
    FROM chitinfo h
    INNER JOIN fa_asset_depre_posted p
        ON p.CHITINFO_ID = h.CHIT_ID
       AND p.COMPANY_CD = h.COMPANY_CD
    WHERE p.COMPANY_CD = p_COMPANY_CD
      AND p.DEPRE_YM = p_PERIOD_YM
      AND IFNULL(p.CANCEL_YN, 'N') = 'N'
      AND p.CHITINFO_ID IS NOT NULL;

    UPDATE fa_asset_depre_posted_alloc pa
    INNER JOIN fa_asset_depre_posted p
        ON p.POST_ID = pa.POST_ID
       AND p.COMPANY_CD = pa.COMPANY_CD
    SET pa.CHITDETAIL_ID = NULL
    WHERE p.COMPANY_CD = p_COMPANY_CD
      AND p.DEPRE_YM = p_PERIOD_YM
      AND p.CANCEL_YN = 'N';

    UPDATE fa_asset_depre_posted
    SET CANCEL_YN = 'Y',
        LOCK_YN = 'N',
        CHITINFO_ID = NULL,
        CHIT_NO = NULL
    WHERE COMPANY_CD = p_COMPANY_CD
      AND DEPRE_YM = p_PERIOD_YM
      AND CANCEL_YN = 'N';

    CALL sp_period_lock_step_upsert(
        p_COMPANY_CD,
        p_PERIOD_YM,
        'FA_PREPAID_LOCK',
        'Khóa TSCĐ / CP trả trước',
        1,
        'OPEN',
        'Đã mở',
        p_USER_ID
    );

    COMMIT;
END$$

DELIMITER ;
