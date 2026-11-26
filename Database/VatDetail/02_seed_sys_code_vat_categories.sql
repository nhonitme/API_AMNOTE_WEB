-- =============================================================================
-- VAT Detail: seed sys_code categories for Ghi chú / Phân loại bảng kê
-- Target: manager database (sys_code used by get_sys_code)
-- CODE_TYPE:
--   VAT_BUY_CATEGORY
--   VAT_SELL_CATEGORY
-- Safe to re-run (INSERT only when CODE_TYPE+CODE_CD missing).
-- Does NOT overwrite existing rows. Does NOT delete anything.
-- Do NOT run on production without review.
-- =============================================================================

-- Guard: NOTE column length (fail soft if unexpected)
SET @note_len := (
  SELECT CHARACTER_MAXIMUM_LENGTH
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME = 'sys_code'
    AND COLUMN_NAME = 'NOTE'
  LIMIT 1
);

SELECT IFNULL(@note_len, -1) AS SYS_CODE_NOTE_LENGTH;

-- ---------- VAT_BUY_CATEGORY ----------
INSERT INTO sys_code (
  CODE_TYPE, CODE_CD, CODE_NAME, SORT_ORDER, NOTE, IS_ACTIVE, ISDEL, CREATE_AT, UPDATE_AT
)
SELECT
  v.CODE_TYPE, v.CODE_CD, v.CODE_NAME, v.SORT_ORDER, v.NOTE, 1, '0', NOW(), NOW()
FROM (
  SELECT 'VAT_BUY_CATEGORY' AS CODE_TYPE, '1a' AS CODE_CD, 'VAT_BUY_CATEGORY_1A' AS CODE_NAME, 1 AS SORT_ORDER,
         '1a. Hàng hóa, dịch vụ dùng riêng cho SXKD chịu thuế GTGT (nhóm 1a)' AS NOTE
  UNION ALL
  SELECT 'VAT_BUY_CATEGORY', '1b', 'VAT_BUY_CATEGORY_1B', 2,
         '1b. Hàng hóa, dịch vụ dùng riêng cho SXKD chịu thuế GTGT (nhóm 1b)'
  UNION ALL
  SELECT 'VAT_BUY_CATEGORY', '2', 'VAT_BUY_CATEGORY_2', 3,
         '2. Hàng hóa, dịch vụ dùng chung cho SXKD chịu thuế và không chịu thuế'
  UNION ALL
  SELECT 'VAT_BUY_CATEGORY', '3', 'VAT_BUY_CATEGORY_3', 4,
         '3. Hàng hóa, dịch vụ dùng cho dự án đầu tư'
  UNION ALL
  SELECT 'VAT_BUY_CATEGORY', 'NO_REPORT', 'VAT_BUY_CATEGORY_NO_REPORT', 5,
         'Không lên bảng kê'
) AS v
WHERE NOT EXISTS (
  SELECT 1
  FROM sys_code s
  WHERE s.CODE_TYPE = v.CODE_TYPE
    AND s.CODE_CD = v.CODE_CD
    AND IFNULL(s.ISDEL, '0') = '0'
);

-- ---------- VAT_SELL_CATEGORY ----------
INSERT INTO sys_code (
  CODE_TYPE, CODE_CD, CODE_NAME, SORT_ORDER, NOTE, IS_ACTIVE, ISDEL, CREATE_AT, UPDATE_AT
)
SELECT
  v.CODE_TYPE, v.CODE_CD, v.CODE_NAME, v.SORT_ORDER, v.NOTE, 1, '0', NOW(), NOW()
FROM (
  SELECT 'VAT_SELL_CATEGORY' AS CODE_TYPE, '1' AS CODE_CD, 'VAT_SELL_CATEGORY_1' AS CODE_NAME, 1 AS SORT_ORDER,
         '1. Hàng hóa, dịch vụ không chịu thuế GTGT' AS NOTE
  UNION ALL
  SELECT 'VAT_SELL_CATEGORY', '2', 'VAT_SELL_CATEGORY_2', 2,
         '2. Hàng hóa, dịch vụ chịu thuế suất GTGT 0%'
  UNION ALL
  SELECT 'VAT_SELL_CATEGORY', '3', 'VAT_SELL_CATEGORY_3', 3,
         '3. Hàng hóa, dịch vụ chịu thuế suất GTGT 5%'
  UNION ALL
  SELECT 'VAT_SELL_CATEGORY', '4', 'VAT_SELL_CATEGORY_4', 4,
         '4. Hàng hóa, dịch vụ chịu thuế suất GTGT 10%'
  UNION ALL
  SELECT 'VAT_SELL_CATEGORY', '5', 'VAT_SELL_CATEGORY_5', 5,
         '5. Hàng hóa, dịch vụ không tính thuế GTGT'
  UNION ALL
  SELECT 'VAT_SELL_CATEGORY', 'VAT8', 'VAT_SELL_CATEGORY_8', 6,
         'Hàng hóa, dịch vụ chịu thuế suất GTGT 8%'
  UNION ALL
  SELECT 'VAT_SELL_CATEGORY', 'NO_REPORT', 'VAT_SELL_CATEGORY_NO_REPORT', 7,
         'Không lên bảng kê'
) AS v
WHERE NOT EXISTS (
  SELECT 1
  FROM sys_code s
  WHERE s.CODE_TYPE = v.CODE_TYPE
    AND s.CODE_CD = v.CODE_CD
    AND IFNULL(s.ISDEL, '0') = '0'
);

SELECT CODE_TYPE, CODE_CD, CODE_NAME, SORT_ORDER, NOTE, IS_ACTIVE, ISDEL
FROM sys_code
WHERE CODE_TYPE IN ('VAT_BUY_CATEGORY', 'VAT_SELL_CATEGORY')
  AND IFNULL(ISDEL, '0') = '0'
ORDER BY CODE_TYPE, SORT_ORDER, CODE_CD;
