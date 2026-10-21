-- =============================================================================
-- DATABASE: am_manager
-- Tinh nang: Xem thong tin License tren AMnote Web (chi doc)
-- Tuong thich MySQL 5.6+; khong tu dong cap License mau.
--
-- company_license:
--   Luu thong tin giay phep su dung hien tai cua tung cong ty.
--   Moi COMPANY_CD co toi da 1 ban ghi License (UNIQUE KEY).
--
-- company_license_renewal:
--   Luu nhat ky gia han License theo tung cong ty, moi lan gia han mot dong.
--   Du lieu lich su duoc hien thi tren tab "Lich su gia han".
--
-- LUU Y:
--   1. Chi nhan vien quan tri AMNote duoc cap nhat du lieu License.
--   2. Ban hien tai CHI HIEN THI; chua chan su dung khi het han/qua gioi han.
--   3. CREATE TABLE IF NOT EXISTS khong bo sung COMMENT vao bang DA ton tai.
--      Neu da tao bang, can dung ALTER TABLE ... MODIFY COLUMN / COMMENT.
-- =============================================================================

CREATE TABLE IF NOT EXISTS `company_license` (
  `LICENSE_ID` BIGINT NOT NULL AUTO_INCREMENT
    COMMENT 'ID duy nhat cua ban ghi License, khoa chinh tu tang',

  `COMPANY_CD` VARCHAR(20) NOT NULL
    COMMENT 'Ma cong ty duoc cap License, duy nhat trong bang',

  `LICENSE_KEY` VARCHAR(100) DEFAULT NULL
    COMMENT 'Ma khoa License do AMNote cap; API chi tra ve gia tri da che',

  `PLAN_CODE` VARCHAR(50) NOT NULL DEFAULT 'STANDARD'
    COMMENT 'Ma goi phan mem, vi du STANDARD hoac PROFESSIONAL',

  `LICENSE_TYPE` VARCHAR(30) NOT NULL DEFAULT 'ANNUAL'
    COMMENT 'Loai thoi han License, vi du ANNUAL, MONTHLY, PERPETUAL',

  `START_DATE` DATE DEFAULT NULL
    COMMENT 'Ngay bat dau hieu luc License; NULL neu chua thiet lap',

  `END_DATE` DATE DEFAULT NULL
    COMMENT 'Ngay het han License; NULL neu khong thiet lap gioi han thoi gian',

  `MAX_USERS` INT DEFAULT NULL
    COMMENT 'So tai khoan nguoi dung toi da duoc cap phep; NULL neu chua quy dinh',

  `MAX_CONCURRENT_USERS` INT DEFAULT NULL
    COMMENT 'So phien dang nhap dong thoi toi da duoc cap phep; NULL neu chua quy dinh',

  `LICENSE_STATUS` VARCHAR(20) NOT NULL DEFAULT 'ACTIVE'
    COMMENT 'Trang thai luu tru: ACTIVE hoac SUSPENDED; API tu tinh EXPIRING/EXPIRED theo END_DATE',

  `NOTE` VARCHAR(500) DEFAULT NULL
    COMMENT 'Ghi chu noi bo ve License cua cong ty',

  `CREATED_AT` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
    COMMENT 'Thoi diem tao ban ghi License',

  `UPDATED_AT` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
    COMMENT 'Thoi diem cap nhat ban ghi License gan nhat',

  PRIMARY KEY (`LICENSE_ID`),
  UNIQUE KEY `UK_COMPANY_LICENSE_COMPANY` (`COMPANY_CD`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci
  COMMENT='Thong tin License hien tai theo cong ty cua AMnote Web';


CREATE TABLE IF NOT EXISTS `company_license_renewal` (
  `ID` BIGINT NOT NULL AUTO_INCREMENT
    COMMENT 'ID duy nhat cua lan gia han License, khoa chinh tu tang',

  `COMPANY_CD` VARCHAR(20) NOT NULL
    COMMENT 'Ma cong ty co License duoc gia han, lien ket logic voi company_license.COMPANY_CD',

  `RENEWED_AT` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
    COMMENT 'Ngay gio thuc hien gia han License',

  `OLD_END_DATE` DATE DEFAULT NULL
    COMMENT 'Ngay het han cua License truoc khi gia han',

  `NEW_END_DATE` DATE DEFAULT NULL
    COMMENT 'Ngay het han moi cua License sau khi gia han',

  `NOTE` VARCHAR(500) DEFAULT NULL
    COMMENT 'Noi dung hoac ghi chu ve lan gia han License',

  `CREATED_BY` VARCHAR(100) DEFAULT NULL
    COMMENT 'Ma nguoi quan tri AMNote thuc hien ghi nhan lan gia han',

  PRIMARY KEY (`ID`),
  KEY `IX_LICENSE_RENEWAL_COMPANY_DATE` (`COMPANY_CD`, `RENEWED_AT`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci
  COMMENT='Lich su cac lan gia han License theo cong ty';


-- Vi du cap License bang thao tac quan tri (can thay gia tri phu hop):
-- INSERT INTO company_license (COMPANY_CD, PLAN_CODE, LICENSE_TYPE, START_DATE, END_DATE, MAX_USERS)
-- VALUES ('<company_cd>', 'PROFESSIONAL', 'ANNUAL', '2026-01-01', '2026-12-31', 20);
--
-- Khi gia han, cap nhat company_license.END_DATE va them 1 dong vao
-- company_license_renewal trong CUNG giao dich (transaction).
-- Tinh nang hien tai chi doc License, khong bao gom thao tac gia han.
