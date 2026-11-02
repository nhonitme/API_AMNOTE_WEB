-- =============================================================================
-- G-K 2B — môi trường DEV/TEST đã được phê duyệt (committed allowlist)
-- =============================================================================
-- Điền giá trị THỰT sau khi được duyệt. Giá trị placeholder → preflight LUÔN FAIL.
-- Không dựa vào biến do người chạy tự bịa; runner chỉ được USE đúng DB trong list này.
-- CẤM production.
-- =============================================================================

SET @GK_2B_APPROVED_DB := '<<SET_AFTER_DEV_APPROVAL>>';
SET @GK_2B_APPROVED_HOST := '<<SET_AFTER_DEV_APPROVAL>>';
SET @GK_2B_APPROVED_ENV_TAG := 'DEV';

-- Tuỳ chọn: chuỗi phụ (substring) bắt buộc có trong @@hostname nếu host exact chưa chốt
SET @GK_2B_APPROVED_HOST_CONTAINS := '<<SET_AFTER_DEV_APPROVAL>>';
