-- =============================================================================
-- VAT Detail: add VAT_DIRECTION to chitdetailinfo_vat (company DB)
-- Safe to re-run. Does NOT backfill historical rows (NULL stays NULL).
-- Do NOT run on production without review.
-- =============================================================================

SET @schema_name := DATABASE();

SET @has_col := (
  SELECT COUNT(*)
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = @schema_name
    AND TABLE_NAME = 'chitdetailinfo_vat'
    AND COLUMN_NAME = 'VAT_DIRECTION'
);

SET @sql := IF(
  @has_col = 0,
  'ALTER TABLE chitdetailinfo_vat
     ADD COLUMN VAT_DIRECTION VARCHAR(4) NULL
     COMMENT ''BUY: VAT đầu vào; SELL: VAT đầu ra''
     AFTER VAT_CUSTOMS_EXCHANGE_RATE',
  'SELECT ''chitdetailinfo_vat.VAT_DIRECTION already exists'' AS INFO'
);

PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
