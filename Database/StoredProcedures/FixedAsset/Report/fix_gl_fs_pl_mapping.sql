-- Fix: GL_FS_PL was pointing at GL_PROFIT_LOSS_B02DNTT (rpt_profit_loss_b02dntt),
-- so /gl/fs/profit-loss (GL_PROFIT_LOSS_B02DN) called the wrong store.
-- Restore GL_FS_PL -> B02DN and add a dedicated mapping for B02DNTT.

UPDATE company_report_mapping m
INNER JOIN sys_report_catalog wrong
  ON wrong.REPORT_ID = m.REPORT_ID
 AND wrong.REPORT_CODE = 'GL_PROFIT_LOSS_B02DNTT'
INNER JOIN sys_report_catalog correct
  ON correct.REPORT_CODE = 'GL_PROFIT_LOSS_B02DN'
SET m.REPORT_ID = correct.REPORT_ID,
    m.UPDATED_AT = NOW()
WHERE m.REPORT_KEY = 'GL_FS_PL'
  AND m.ISDEL = '0';

INSERT INTO company_report_mapping (
  COMPANY_CD, REPORT_KEY, REPORT_ID, SIGN_IDS,
  PAGE_ORIENTATION, PAPER_KIND, FONT_FAMILY,
  FONT_SIZE, TITLE_FONT_SIZE, INFO_FONT_SIZE, HEADER_FONT_SIZE, DETAIL_FONT_SIZE, FOOTER_FONT_SIZE,
  MARGIN_LEFT, MARGIN_RIGHT, MARGIN_TOP, MARGIN_BOTTOM,
  IS_DEFAULT, IS_ACTIVE, ISDEL, CREATED_AT, UPDATED_AT
)
SELECT
  src.COMPANY_CD,
  'GL_PROFIT_LOSS_B02DNTT',
  catalog.REPORT_ID,
  src.SIGN_IDS,
  src.PAGE_ORIENTATION,
  src.PAPER_KIND,
  src.FONT_FAMILY,
  src.FONT_SIZE,
  src.TITLE_FONT_SIZE,
  src.INFO_FONT_SIZE,
  src.HEADER_FONT_SIZE,
  src.DETAIL_FONT_SIZE,
  src.FOOTER_FONT_SIZE,
  src.MARGIN_LEFT,
  src.MARGIN_RIGHT,
  src.MARGIN_TOP,
  src.MARGIN_BOTTOM,
  '1',
  '1',
  '0',
  NOW(),
  NOW()
FROM company_report_mapping src
INNER JOIN sys_report_catalog catalog
  ON catalog.REPORT_CODE = 'GL_PROFIT_LOSS_B02DNTT'
WHERE src.REPORT_KEY = 'GL_FS_PL'
  AND src.ISDEL = '0'
  AND NOT EXISTS (
    SELECT 1
    FROM company_report_mapping existing
    WHERE existing.COMPANY_CD = src.COMPANY_CD
      AND existing.REPORT_KEY = 'GL_PROFIT_LOSS_B02DNTT'
      AND existing.ISDEL = '0'
  );
