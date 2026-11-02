-- Guard thuần SQL (không DDL) — SOURCE đầu mỗi file migration sau preflight.
-- Re-load allowlist để an toàn khi chạy file lẻ.

SOURCE 20261102_2b_APPROVED_ENV.sql;

SET @gk_2b_db := DATABASE();
SET @gk_2b_host := @@hostname;
SET @gk_2b_allow_user := NULLIF(TRIM(IFNULL(@GK_2B_ALLOW_DB, '')), '');
SET @gk_2b_placeholder := (
       IFNULL(@GK_2B_APPROVED_DB, '') LIKE '%SET_AFTER_DEV_APPROVAL%'
    OR IFNULL(@GK_2B_APPROVED_HOST, '') LIKE '%SET_AFTER_DEV_APPROVAL%'
    OR IFNULL(@GK_2B_APPROVED_DB, '') = ''
    OR IFNULL(@GK_2B_APPROVED_HOST, '') = ''
);
SET @gk_2b_db_ok := (
    @gk_2b_db IS NOT NULL AND @gk_2b_db <> '' AND @gk_2b_db = @GK_2B_APPROVED_DB
    AND @gk_2b_db NOT IN ('am_web_prod', 'am_web_production', 'production', 'prod')
);
SET @gk_2b_host_contains_ready := (
    IFNULL(@GK_2B_APPROVED_HOST_CONTAINS, '') <> ''
    AND IFNULL(@GK_2B_APPROVED_HOST_CONTAINS, '') NOT LIKE '%SET_AFTER_DEV_APPROVAL%'
);
SET @gk_2b_host_ok := (
    @gk_2b_host = @GK_2B_APPROVED_HOST
    OR (@gk_2b_host_contains_ready = 1 AND LOCATE(@GK_2B_APPROVED_HOST_CONTAINS, IFNULL(@gk_2b_host, '')) > 0)
);
SET @gk_2b_user_ok := (@gk_2b_allow_user IS NULL OR @gk_2b_allow_user = @GK_2B_APPROVED_DB);
SET @gk_2b_env_ok := (IFNULL(@GK_2B_APPROVED_ENV_TAG, '') IN ('DEV', 'TEST'));
SET @gk_2b_ok := IF(
    @gk_2b_placeholder = 0 AND @gk_2b_db_ok = 1 AND @gk_2b_host_ok = 1
    AND @gk_2b_user_ok = 1 AND @gk_2b_env_ok = 1, 1, 0
);
-- Hard-stop không DDL: PREPARE câu SQL không hợp lệ khi FAIL.
-- MySQL sẽ trả ERROR 1064 (không phải warning/NULL như phép chia cho 0).
-- BẮT BUỘC chạy bằng mysql --abort-source-on-error, không dùng --force.
SET @gk_2b_stop_sql := IF(
    @gk_2b_ok = 1,
    'SELECT ''GK2B PREFLIGHT PASS'' AS gate_status',
    'GK2B_PREFLIGHT_FAILED__STOP_BEFORE_ANY_DDL'
);
PREPARE gk2b_stop_stmt FROM @gk_2b_stop_sql;
EXECUTE gk2b_stop_stmt;
DEALLOCATE PREPARE gk2b_stop_stmt;
