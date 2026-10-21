-- =============================================================================
-- DATABASE: am_manager
-- Tính năng: Xem thông tin License trên AMnote Web (chỉ đọc)
-- Tương thích MySQL 5.6+; không tự động cấp License mẫu.
--
-- company_license:
--   Lưu thông tin giấy phép sử dụng hiện tại của từng công ty.
--   Mỗi COMPANY_CD có tối đa 1 bản ghi License (UNIQUE KEY).
--
-- company_license_renewal:
--   Lưu nhật ký gia hạn License theo từng công ty, mỗi lần gia hạn một dòng.
--   Dữ liệu lịch sử được hiển thị trên tab "Lịch sử gia hạn".
--
-- LƯU Ý:
--   1. Chỉ nhân viên quản trị AMNote được cập nhật dữ liệu License.
--   2. Bản hiện tại CHỈ HIỂN THỊ; chưa chặn sử dụng khi hết hạn/quá giới hạn.
--   3. CREATE TABLE IF NOT EXISTS không bổ sung COMMENT vào bảng ĐÃ tồn tại.
--      Nếu đã tạo bảng, cần dùng ALTER TABLE ... MODIFY COLUMN / COMMENT.
-- =============================================================================

CREATE TABLE IF NOT EXISTS `company_license` (
  `LICENSE_ID` BIGINT NOT NULL AUTO_INCREMENT
    COMMENT 'ID duy nhất của bản ghi License, khóa chính tự tăng',

  `COMPANY_CD` VARCHAR(20) NOT NULL
    COMMENT 'Mã công ty được cấp License, duy nhất trong bảng',

  `LICENSE_KEY` VARCHAR(100) DEFAULT NULL
    COMMENT 'Mã khóa License do AMNote cấp; API chỉ trả về giá trị đã che',

  `PLAN_CODE` VARCHAR(50) NOT NULL DEFAULT 'STANDARD'
    COMMENT 'Mã gói phần mềm, ví dụ STANDARD hoặc PROFESSIONAL',

  `LICENSE_TYPE` VARCHAR(30) NOT NULL DEFAULT 'ANNUAL'
    COMMENT 'Loại thời hạn License, ví dụ ANNUAL, MONTHLY, PERPETUAL',

  `START_DATE` DATE DEFAULT NULL
    COMMENT 'Ngày bắt đầu hiệu lực License; NULL nếu chưa thiết lập',

  `END_DATE` DATE DEFAULT NULL
    COMMENT 'Ngày hết hạn License; NULL nếu không thiết lập giới hạn thời gian',

  `MAX_USERS` INT DEFAULT NULL
    COMMENT 'Số tài khoản người dùng tối đa được cấp phép; NULL nếu chưa quy định',

  `MAX_CONCURRENT_USERS` INT DEFAULT NULL
    COMMENT 'Số phiên đăng nhập đồng thời tối đa được cấp phép; NULL nếu chưa quy định',

  `LICENSE_STATUS` VARCHAR(20) NOT NULL DEFAULT 'ACTIVE'
    COMMENT 'Trạng thái lưu: ACTIVE hoặc SUSPENDED; API tự tính EXPIRING/EXPIRED theo END_DATE',

  `NOTE` VARCHAR(500) DEFAULT NULL
    COMMENT 'Ghi chú nội bộ về License của công ty',

  `CREATED_AT` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
    COMMENT 'Thời điểm tạo bản ghi License',

  `UPDATED_AT` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
    COMMENT 'Thời điểm cập nhật bản ghi License gần nhất',

  PRIMARY KEY (`LICENSE_ID`),
  UNIQUE KEY `UK_COMPANY_LICENSE_COMPANY` (`COMPANY_CD`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci
  COMMENT='Thông tin License hiện tại theo công ty của AMnote Web';


CREATE TABLE IF NOT EXISTS `company_license_renewal` (
  `ID` BIGINT NOT NULL AUTO_INCREMENT
    COMMENT 'ID duy nhất của lần gia hạn License, khóa chính tự tăng',

  `COMPANY_CD` VARCHAR(20) NOT NULL
    COMMENT 'Mã công ty có License được gia hạn, liên kết logic với company_license.COMPANY_CD',

  `RENEWED_AT` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
    COMMENT 'Ngày giờ thực hiện gia hạn License',

  `OLD_END_DATE` DATE DEFAULT NULL
    COMMENT 'Ngày hết hạn của License trước khi gia hạn',

  `NEW_END_DATE` DATE DEFAULT NULL
    COMMENT 'Ngày hết hạn mới của License sau khi gia hạn',

  `NOTE` VARCHAR(500) DEFAULT NULL
    COMMENT 'Nội dung hoặc ghi chú về lần gia hạn License',

  `CREATED_BY` VARCHAR(100) DEFAULT NULL
    COMMENT 'Mã người quản trị AMNote thực hiện ghi nhận lần gia hạn',

  PRIMARY KEY (`ID`),
  KEY `IX_LICENSE_RENEWAL_COMPANY_DATE` (`COMPANY_CD`, `RENEWED_AT`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci
  COMMENT='Lịch sử các lần gia hạn License theo công ty';


-- Ví dụ cấp License bằng thao tác quản trị (cần thay giá trị phù hợp):
-- INSERT INTO company_license (COMPANY_CD, PLAN_CODE, LICENSE_TYPE, START_DATE, END_DATE, MAX_USERS)
-- VALUES ('<company_cd>', 'PROFESSIONAL', 'ANNUAL', '2026-01-01', '2026-12-31', 20);
--
-- Khi gia hạn, cập nhật company_license.END_DATE và thêm 1 dòng vào
-- company_license_renewal trong CÙNG giao dịch (transaction).
-- Tính năng hiện tại chỉ đọc License, không bao gồm thao tác gia hạn.
