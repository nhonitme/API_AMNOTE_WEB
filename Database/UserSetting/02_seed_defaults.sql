-- Seed mặc định hệ thống (COMPANY_CD='', USER_ID='') trong DB công ty.
-- Chạy trên từng database công ty khi triển khai.

INSERT INTO user_setting_info (COMPANY_CD, USER_ID, KEY_NAME, VALUE, NOTE, UPDATE_AT)
VALUES
    (
        '',
        '',
        'DEFAULT_CHECK_VAT',
        '1',
        'Mặc định tick VAT khi lập phiếu',
        NOW()
    ),
    (
        '',
        '',
        'PERIOD_LOCK_STEP_CODES',
        'FA_PREPAID_LOCK,COGS_SUMMARY,PROFIT_LOSS_REPORT',
        'Khóa sổ - Bước đã chọn (1-3): FA_PREPAID_LOCK=Bước1 TSCĐ/CP trả trước; COGS_SUMMARY=Bước2 Giá vốn; PROFIT_LOSS_REPORT=Bước3 Lãi lỗ. VALUE: mã cách nhau dấu phẩy',
        NOW()
    ),
    (
        '',
        '',
        'PERIOD_LOCK_COGS_RULE',
        '154_TO_632',
        'Khóa sổ - Bước 2 (COGS_SUMMARY): cách chuyển giá vốn. VALUE: 154_TO_155 | 154_TO_632 | 154_TO_155_TO_632',
        NOW()
    ),
    (
        '',
        '',
        'PERIOD_LOCK_PL_BALANCE_METHOD',
        'BALANCE_ACCOUNT_TWO_SIDE',
        'Khóa sổ - Bước 3 (PROFIT_LOSS_REPORT): cách tính báo cáo lãi lỗ. VALUE: BALANCE_ACCOUNT | BALANCE_ACCOUNT_TWO_SIDE',
        NOW()
    ),
    (
        '',
        '',
        'MATERIAL_ISSUE_ACCOUNT_PAIRS',
        '621/152,627/152,623/152,641/152,642/152,241/152',
        'Xuất nguyên vật liệu - Danh sách cặp tài khoản Nợ/Có được phép sử dụng. VALUE: các cặp cách nhau bằng dấu phẩy; dạng NO/CO | NO/ | /CO (so khớp prefix).',
        NOW()
    ),
    (
        '',
        '',
        'FINISH_GOODS_IN_ACCOUNT_PAIRS',
        '155/154',
        'Nhập thành phẩm - Danh sách cặp tài khoản Nợ/Có được phép sử dụng. VALUE: các cặp cách nhau bằng dấu phẩy; dạng NO/CO | NO/ | /CO (so khớp prefix).',
        NOW()
    ),
    (
        '',
        '',
        'OT_INVENTORY_RECEIPT_ACCOUNT_PAIRS',
        '152/,155/,156/',
        'Phiếu OT - Cặp TK xác định popup nhập kho (IR). VALUE: các cặp cách nhau bằng dấu phẩy; dạng NO/CO | NO/ | /CO (so khớp prefix).',
        NOW()
    ),
    (
        '',
        '',
        'OT_INVENTORY_ISSUE_ACCOUNT_PAIRS',
        '621/152,627/152,623/152,641/152,642/152,241/152,632/156',
        'Phiếu OT - Cặp TK xác định popup xuất kho (IO). VALUE: các cặp cách nhau bằng dấu phẩy; dạng NO/CO | NO/ | /CO (so khớp prefix).',
        NOW()
    )
ON DUPLICATE KEY UPDATE
    NOTE = VALUES(NOTE),
    UPDATE_DT = NOW();
