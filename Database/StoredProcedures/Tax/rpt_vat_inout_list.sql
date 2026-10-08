-- --------------------------------------------------------
-- Host:                         118.69.170.50
-- Server version:               5.6.11-log - Source distribution
-- Server OS:                    Linux
-- HeidiSQL Version:             8.3.0.4694
-- --------------------------------------------------------

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET NAMES utf8 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;

-- Dumping structure for procedure am_web_001.rpt_vat_inout_list
DROP PROCEDURE IF EXISTS `rpt_vat_inout_list`;
DELIMITER //
CREATE DEFINER=`amnc9it`@`115.78.232.22` PROCEDURE `rpt_vat_inout_list`(
    IN p_COMPANY_CD VARCHAR(20) CHARACTER SET utf8 COLLATE utf8_unicode_ci,
    IN p_FROM_YMD CHAR(8) CHARACTER SET utf8 COLLATE utf8_unicode_ci,
    IN p_TO_YMD CHAR(8) CHARACTER SET utf8 COLLATE utf8_unicode_ci,
    IN p_TYPE VARCHAR(10) CHARACTER SET utf8 COLLATE utf8_unicode_ci,
    IN p_STATUS VARCHAR(10) CHARACTER SET utf8 COLLATE utf8_unicode_ci,
    IN p_SEARCH_TEXT VARCHAR(200) CHARACTER SET utf8 COLLATE utf8_unicode_ci,
    IN p_LANGUAGE VARCHAR(10) CHARACTER SET utf8 COLLATE utf8_unicode_ci
)
BEGIN
    DECLARE v_company_cd VARCHAR(20) CHARACTER SET utf8 COLLATE utf8_unicode_ci;
    DECLARE v_type VARCHAR(10) CHARACTER SET utf8 COLLATE utf8_unicode_ci;
    DECLARE v_from_ymd CHAR(8) CHARACTER SET utf8 COLLATE utf8_unicode_ci;
    DECLARE v_to_ymd CHAR(8) CHARACTER SET utf8 COLLATE utf8_unicode_ci;
    DECLARE v_status VARCHAR(10) CHARACTER SET utf8 COLLATE utf8_unicode_ci;
    DECLARE v_search VARCHAR(200) CHARACTER SET utf8 COLLATE utf8_unicode_ci;
    DECLARE v_language VARCHAR(10) CHARACTER SET utf8 COLLATE utf8_unicode_ci;

    SET v_company_cd = TRIM(IFNULL(p_COMPANY_CD, ''));
    SET v_type = UPPER(TRIM(IFNULL(p_TYPE, 'BUY')));
    SET v_from_ymd = TRIM(IFNULL(p_FROM_YMD, ''));
    SET v_to_ymd = TRIM(IFNULL(p_TO_YMD, ''));
    SET v_status = TRIM(IFNULL(p_STATUS, ''));
    SET v_search = TRIM(IFNULL(p_SEARCH_TEXT, ''));
    SET v_language = UPPER(TRIM(IFNULL(p_LANGUAGE, 'VIET')));

    IF v_type IN ('1', 'IN', 'BUY') THEN
        SET v_type = 'BUY';
    ELSEIF v_type IN ('0', '2', 'OUT', 'SELL') THEN
        SET v_type = 'SELL';
    END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_vat_inout_keys;

    CREATE TEMPORARY TABLE tmp_vat_inout_keys
    (
        mhdon VARCHAR(50)
            CHARACTER SET utf8
            COLLATE utf8_unicode_ci NOT NULL,

        PRIMARY KEY (mhdon)
    );

    /* ============================================================
       BUY - HÓA ĐƠN ĐẦU VÀO
       ============================================================ */
    IF v_type = 'BUY' THEN

        INSERT INTO tmp_vat_inout_keys (mhdon)
        SELECT A.mhdon
        FROM buy_list_einvoice AS A

        LEFT JOIN buy_list_json AS JSON
            ON JSON.mhdon = A.mhdon
            AND IFNULL(JSON.ISDEL, '') != '1'

        LEFT JOIN chitinfo AS B
            ON A.AUTO_CHIT_CD = B.CHIT_CD
            AND IFNULL(B.ISDEL, '') != '1'

        WHERE IFNULL(A.ISDEL, '') != '1'

            AND (
                v_from_ymd = ''
                OR v_to_ymd = ''
                OR A.tdlap BETWEEN v_from_ymd AND v_to_ymd
            )

            AND (
                v_status = ''
                OR
                (
                    CASE
                        WHEN IFNULL(JSON.mhdon, '') = '' THEN '998'
                        ELSE IFNULL(A.tthai, '')
                    END
                ) = v_status
            )

            AND (
                v_search = ''

                OR IFNULL(A.shdon, '') LIKE CONCAT('%', v_search, '%')
                OR IFNULL(A.mhdon, '') LIKE CONCAT('%', v_search, '%')
                OR IFNULL(A.khhdon, '') LIKE CONCAT('%', v_search, '%')
                OR IFNULL(A.khmshdon, '') LIKE CONCAT('%', v_search, '%')
                OR IFNULL(A.GChu, '') LIKE CONCAT('%', v_search, '%')

                OR IFNULL(A.nbten, '') LIKE CONCAT('%', v_search, '%')
                OR IFNULL(A.nbmst, '') LIKE CONCAT('%', v_search, '%')

                OR IFNULL(A.nmten, '') LIKE CONCAT('%', v_search, '%')
                OR IFNULL(A.nmmst, '') LIKE CONCAT('%', v_search, '%')

                OR IFNULL(B.CHIT_NO, '') LIKE CONCAT('%', v_search, '%')
            )

        GROUP BY A.mhdon;


        /* Result set 1: invoice rows */
		 	SELECT
		          A.mhdon,
		
		            CASE
		                WHEN IFNULL(JSON.mhdon, '') = '' THEN '998'
		                ELSE A.tthai
		            END AS tthai,
		
					 	(
					    SELECT
					        CASE UPPER(p_LANGUAGE)
					            WHEN 'ENG' THEN m.ENG
					            WHEN 'KOR' THEN m.KOR
					            ELSE m.VIET
					        END
					    FROM am_web_manager.t_message_info m
					    WHERE m.`KEY` =
					        CASE tthai
							  		WHEN IFNULL(JSON.mhdon, '') = '' THEN 'eInvoice_AdjustType_NM_998'
					            WHEN '1'   THEN 'eInvoice_AdjustType_NM_1'
					            WHEN '2'   THEN 'eInvoice_AdjustType_NM_3'
					            WHEN '3'   THEN 'eInvoice_AdjustType_NM_5'
					            WHEN '4'   THEN 'eInvoice_AdjustType_NM_2'
					            WHEN '5'   THEN 'eInvoice_AdjustType_NM_4'
					            WHEN '6'   THEN 'eInvoice_AdjustType_NM_6'
					            ELSE ''
					        END
					    LIMIT 1
					) AS tthai_ten,

            A.khmshdon,
            (
				    SELECT
				        CASE UPPER(p_LANGUAGE)
				            WHEN 'ENG' THEN m.ENG
				            WHEN 'KOR' THEN m.KOR
				            ELSE m.VIET
				        END
				    FROM am_web_manager.t_message_info m
				    WHERE m.`KEY` = CONCAT('EInvoiceKind_', khmshdon)
				    LIMIT 1
				) AS khmshdon_ten,
            A.khhdon,

            A.tdlap,
            DATE_FORMAT(A.tdlap, '%d/%m/%Y') AS tdlap_ten,

            A.shdon,

            IFNULL(B.CHIT_NO, '') AS CHIT_NO,

            A.dvtte,

            CASE
                WHEN A.TGia <= 1 THEN 0
                ELSE A.TGia
            END AS TGia,

            A.mtdtchieu,

            /* BUY => nhà bán là đối tượng chính */
            A.nbten AS ten,
            A.nbdchi AS dchi,
            A.nbmst AS mst,

            A.nbten,
            A.nbdchi,
            A.nbmst,

            A.nmten,
            A.nmdchi,
            A.nmmst,

            A.tgtcthue,
            A.tgtthue,
            A.tgtttbso,
            A.ttcktmai,

            A.GChu,

            A.AUTO_CHIT_CD,

            IFNULL(B.CHIT_YMD, '') AS CHIT_YMD,
            IFNULL(B.CHIT_TYPE, 0) AS CHIT_TYPE

        FROM buy_list_einvoice AS A

        INNER JOIN tmp_vat_inout_keys AS K
            ON K.mhdon = A.mhdon

        LEFT JOIN chitinfo AS B
            ON A.AUTO_CHIT_CD = B.CHIT_CD
            AND IFNULL(B.ISDEL, '') != '1'

        LEFT JOIN buy_list_json AS JSON
            ON JSON.mhdon = A.mhdon
            AND IFNULL(JSON.ISDEL, '') != '1'

        GROUP BY A.mhdon

        ORDER BY
            A.khmshdon,
            A.tdlap,
            CONVERT(A.shdon, UNSIGNED INTEGER),
            A.khhdon,
            A.mhdon;


        /* Result set 2: JSON */
        SELECT
            JSON.mhdon,
            IFNULL(JSON.json, '') AS json

        FROM buy_list_json AS JSON

        INNER JOIN tmp_vat_inout_keys AS K
            ON K.mhdon = JSON.mhdon

        WHERE IFNULL(JSON.ISDEL, '') != '1';


    /* ============================================================
       SELL - HÓA ĐƠN ĐẦU RA
       ============================================================ */
    ELSE

        INSERT INTO tmp_vat_inout_keys (mhdon)
        SELECT A.mhdon
        FROM sell_list_einvoice AS A

        LEFT JOIN sell_list_json AS JSON
            ON JSON.mhdon = A.mhdon
            AND IFNULL(JSON.ISDEL, '') != '1'

        LEFT JOIN chitinfo AS B
            ON A.AUTO_CHIT_CD = B.CHIT_CD
            AND IFNULL(B.ISDEL, '') != '1'

        WHERE IFNULL(A.ISDEL, '') != '1'

            AND (
                v_from_ymd = ''
                OR v_to_ymd = ''
                OR A.tdlap BETWEEN v_from_ymd AND v_to_ymd
            )

            AND (
                v_status = ''
                OR
                (
                    CASE
                        WHEN IFNULL(JSON.mhdon, '') = '' THEN '998'
                        ELSE IFNULL(A.tthai, '')
                    END
                ) = v_status
            )

            AND (
                v_search = ''

                OR IFNULL(A.shdon, '') LIKE CONCAT('%', v_search, '%')
                OR IFNULL(A.mhdon, '') LIKE CONCAT('%', v_search, '%')
                OR IFNULL(A.khhdon, '') LIKE CONCAT('%', v_search, '%')
                OR IFNULL(A.khmshdon, '') LIKE CONCAT('%', v_search, '%')
                OR IFNULL(A.GChu, '') LIKE CONCAT('%', v_search, '%')

                OR IFNULL(A.nbten, '') LIKE CONCAT('%', v_search, '%')
                OR IFNULL(A.nbmst, '') LIKE CONCAT('%', v_search, '%')

                OR IFNULL(A.nmten, '') LIKE CONCAT('%', v_search, '%')
                OR IFNULL(A.nmmst, '') LIKE CONCAT('%', v_search, '%')

                OR IFNULL(B.CHIT_NO, '') LIKE CONCAT('%', v_search, '%')
            )

        GROUP BY A.mhdon;


        /* Result set 1: invoice rows */
        SELECT
            A.mhdon,

            CASE
                WHEN IFNULL(JSON.mhdon, '') = '' THEN '998'
                ELSE A.tthai
            END AS tthai,
		
					 (
					    SELECT
					        CASE UPPER(p_LANGUAGE)
					            WHEN 'ENG' THEN m.ENG
					            WHEN 'KOR' THEN m.KOR
					            ELSE m.VIET
					        END
					    FROM am_web_manager.t_message_info m
					    WHERE m.`KEY` =
					        CASE tthai
							  		WHEN IFNULL(JSON.mhdon, '') = '' THEN 'eInvoice_AdjustType_NM_998'
					            WHEN '1'   THEN 'eInvoice_AdjustType_NM_1'
					            WHEN '2'   THEN 'eInvoice_AdjustType_NM_3'
					            WHEN '3'   THEN 'eInvoice_AdjustType_NM_5'
					            WHEN '4'   THEN 'eInvoice_AdjustType_NM_2'
					            WHEN '5'   THEN 'eInvoice_AdjustType_NM_4'
					            WHEN '6'   THEN 'eInvoice_AdjustType_NM_6'
					            ELSE ''
					        END
					    LIMIT 1
					) AS tthai_ten,

            A.khmshdon,
            (
				    SELECT
				        CASE UPPER(p_LANGUAGE)
				            WHEN 'ENG' THEN m.ENG
				            WHEN 'KOR' THEN m.KOR
				            ELSE m.VIET
				        END
				    FROM am_web_manager.t_message_info m
				    WHERE m.`KEY` = CONCAT('EInvoiceKind_', khmshdon)
				    LIMIT 1
				) AS khmshdon_ten,
            A.khhdon,

            A.tdlap,
            DATE_FORMAT(A.tdlap, '%d/%m/%Y') AS tdlap_ten,

            A.shdon,

            IFNULL(B.CHIT_NO, '') AS CHIT_NO,

            A.dvtte,

            CASE
                WHEN A.TGia <= 1 THEN 0
                ELSE A.TGia
            END AS TGia,

            A.mtdtchieu,

            /* SELL => người mua là đối tượng chính */
            A.nmten AS ten,
            A.nmdchi AS dchi,
            A.nmmst AS mst,

            A.nbten,
            A.nbdchi,
            A.nbmst,

            A.nmten,
            A.nmdchi,
            A.nmmst,

            A.tgtcthue,
            A.tgtthue,
            A.tgtttbso,
            A.ttcktmai,

            A.GChu,

            A.AUTO_CHIT_CD,

            IFNULL(B.CHIT_YMD, '') AS CHIT_YMD,
            IFNULL(B.CHIT_TYPE, 0) AS CHIT_TYPE

        FROM sell_list_einvoice AS A

        INNER JOIN tmp_vat_inout_keys AS K
            ON K.mhdon = A.mhdon

        LEFT JOIN chitinfo AS B
            ON A.AUTO_CHIT_CD = B.CHIT_CD
            AND IFNULL(B.ISDEL, '') != '1'

        LEFT JOIN sell_list_json AS JSON
            ON JSON.mhdon = A.mhdon
            AND IFNULL(JSON.ISDEL, '') != '1'

        GROUP BY A.mhdon

        ORDER BY
            A.khmshdon,
            A.tdlap,
            CONVERT(A.shdon, UNSIGNED INTEGER),
            A.khhdon,
            A.mhdon;


        /* Result set 2: JSON */
        SELECT
            JSON.mhdon,
            IFNULL(JSON.json, '') AS json

        FROM sell_list_json AS JSON

        INNER JOIN tmp_vat_inout_keys AS K
            ON K.mhdon = JSON.mhdon

        WHERE IFNULL(JSON.ISDEL, '') != '1';

    END IF;

    DROP TEMPORARY TABLE IF EXISTS tmp_vat_inout_keys;

END//
DELIMITER ;