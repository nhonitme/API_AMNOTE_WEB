-- Migrate legacy fa_asset.STATUS values to lifecycle statuses
-- Run once per company database (e.g. am_web_001)

UPDATE fa_asset SET STATUS = 'IN_USE' WHERE STATUS = 'USING';
UPDATE fa_asset SET STATUS = 'SUSPENDED' WHERE STATUS = 'STOP';
UPDATE fa_asset SET STATUS = 'SOLD' WHERE STATUS = 'FINISHED';
