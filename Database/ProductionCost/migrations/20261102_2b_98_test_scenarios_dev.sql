-- G-K 2B — kịch bản kiểm thử DEV/TEST (template)
-- CẤM production. Điền @cd / @ym / @user trước khi bỏ comment từng khối.
-- Yêu cầu: đã chạy migration 00_install + có dữ liệu phân bổ G-K CONFIRMED cho kỳ test.

-- SET @cd   = 'DEMO';
-- SET @ym   = '202601';
-- SET @user = 'dev_test';

-- ---------------------------------------------------------------------------
-- T0) Trạng thái engine trước test
-- ---------------------------------------------------------------------------
-- SELECT COMPANY_CD, PERIOD_YM, COST_ENGINE, COGS_RULE, CURRENT_RUN_ID, STATUS
-- FROM production_cost_period
-- WHERE COMPANY_CD = @cd AND PERIOD_YM = @ym AND ISDEL = '0';

-- ---------------------------------------------------------------------------
-- T1) G-K khóa COGS với EXP_TO_154_ONLY
-- ---------------------------------------------------------------------------
-- CALL sp_period_lock_cogs_create_voucher(@cd, @ym, 'EXP_TO_154_ONLY', @user);
-- -- sau đó C#/PeriodLock đánh dấu step DONE như luồng B-I hiện có

-- CT chỉ Nợ 154 / Có 621,622,623,627
-- SELECT TRIM(CD.DEBIT) AS D, TRIM(CD.CREDIT) AS C, SUM(CD.AMOUNT) AS AMT
-- FROM chitinfo C
-- JOIN chitdetailinfo CD ON CD.COMPANY_CD=C.COMPANY_CD AND CD.CHIT_ID=C.CHIT_ID AND CD.ISDEL='0'
-- WHERE C.COMPANY_CD=@cd AND C.ISDEL='0' AND C.INPUT_TYPE='LOCK'
--   AND C.CHIT_YMD BETWEEN CONCAT(@ym,'01')
--       AND DATE_FORMAT(LAST_DAY(STR_TO_DATE(CONCAT(@ym,'01'),'%Y%m%d')),'%Y%m%d')
--   AND C.DESCRIPTION LIKE '%EXP_TO_154_ONLY%'
-- GROUP BY TRIM(CD.DEBIT), TRIM(CD.CREDIT);

-- Không có 154→155 / 154→632 từ rule G-K
-- SELECT COUNT(*) AS forbidden_cnt
-- FROM chitinfo C
-- JOIN chitdetailinfo CD ON CD.COMPANY_CD=C.COMPANY_CD AND CD.CHIT_ID=C.CHIT_ID AND CD.ISDEL='0'
-- WHERE C.COMPANY_CD=@cd AND C.ISDEL='0' AND C.INPUT_TYPE='LOCK'
--   AND C.DESCRIPTION LIKE '%EXP_TO_154_ONLY%'
--   AND (
--         (TRIM(CD.DEBIT) LIKE '155%' AND TRIM(CD.CREDIT) LIKE '154%')
--      OR (TRIM(CD.DEBIT) LIKE '632%' AND TRIM(CD.CREDIT) LIKE '154%')
--      OR (TRIM(CD.DEBIT) LIKE '632%' AND TRIM(CD.CREDIT) LIKE '155%')
--   );

-- Đối chiếu phân bổ 622/627 vs Có 622/627 trên CT LOCK
-- (xem block comment trong 20261102_2b_99_verify_readonly.sql)

-- ---------------------------------------------------------------------------
-- T2) B-I bị chặn khi kỳ đã GK
-- ---------------------------------------------------------------------------
-- CALL sp_period_lock_cogs_create_voucher(@cd, @ym, '154_TO_632', @user);
-- Expect: SQLSTATE 45000 'Ky nay da thuoc engine khac...'

-- ---------------------------------------------------------------------------
-- T3) G-K bị chặn khi kỳ đã BI
-- ---------------------------------------------------------------------------
-- -- trên kỳ khác @ym2: khóa B-I trước, rồi thử EXP_TO_154_ONLY
-- CALL sp_period_lock_cogs_create_voucher(@cd, @ym2, '154_TO_632', @user);
-- CALL sp_period_lock_cogs_create_voucher(@cd, @ym2, 'EXP_TO_154_ONLY', @user);
-- Expect: exclusive error

-- ---------------------------------------------------------------------------
-- T4) Hai request đồng thời — chạy 2 session
-- ---------------------------------------------------------------------------
-- Session A: CALL sp_pc_engine_claim(@cd, @ym3, 'GK', 'EXP_TO_154_ONLY', @user);
-- Session B: CALL sp_pc_engine_claim(@cd, @ym3, 'BI', '154_TO_632', @user);
-- Expect: một thành công, một lỗi exclusive

-- ---------------------------------------------------------------------------
-- T5) Chạy lại / unlock nhất quán
-- ---------------------------------------------------------------------------
-- CALL sp_period_lock_cogs_unlock(@cd, @ym, 'retest', @user);
-- SELECT COST_ENGINE, COGS_RULE FROM production_cost_period WHERE COMPANY_CD=@cd AND PERIOD_YM=@ym;
-- Expect: COST_ENGINE='NONE', không còn CT LOCK COGS_SUMMARY
-- CALL sp_period_lock_cogs_create_voucher(@cd, @ym, 'EXP_TO_154_ONLY', @user);
-- -- không nhân đôi số dòng CT (stable delete trước post)

-- ---------------------------------------------------------------------------
-- T6) B-I 3 rule trên kỳ không thuộc G-K (hành vi cũ)
-- ---------------------------------------------------------------------------
-- CALL sp_period_lock_cogs_create_voucher(@cd, @ym_bi, '154_TO_155', @user);
-- CALL sp_period_lock_cogs_unlock(@cd, @ym_bi, 'x', @user);
-- CALL sp_period_lock_cogs_create_voucher(@cd, @ym_bi, '154_TO_155_TO_632', @user);
-- CALL sp_period_lock_cogs_unlock(@cd, @ym_bi, 'x', @user);
-- CALL sp_period_lock_cogs_create_voucher(@cd, @ym_bi, '154_TO_632', @user);
