DROP TABLE IF EXISTS `user_setting_info`;

CREATE TABLE IF NOT EXISTS `user_setting_info` (
  `COMPANY_CD` varchar(20) COLLATE utf8_unicode_ci NOT NULL DEFAULT '' COMMENT 'Mã công ty. Rỗng = áp dụng mặc định toàn hệ thống',
  `USER_ID` varchar(20) COLLATE utf8_unicode_ci NOT NULL DEFAULT '' COMMENT 'Mã người dùng. Rỗng = áp dụng mặc định theo công ty',
  `KEY_NAME` varchar(100) COLLATE utf8_unicode_ci NOT NULL DEFAULT '' COMMENT 'Mã option/cài đặt. Ví dụ: DEFAULT_CHECK_VAT',
  `VALUE` varchar(250) COLLATE utf8_unicode_ci NOT NULL DEFAULT '' COMMENT 'Giá trị option',
  `NOTE` varchar(250) COLLATE utf8_unicode_ci NOT NULL DEFAULT '' COMMENT 'Ghi chú ý nghĩa option',
  `UPDATE_DT` datetime DEFAULT NULL COMMENT 'Ngày cập nhật cuối cùng',
  PRIMARY KEY (`COMPANY_CD`, `USER_ID`, `KEY_NAME`),
  KEY `IX_USER_SETTING_INFO_USER` (`USER_ID`),
  KEY `IX_USER_SETTING_INFO_KEY` (`KEY_NAME`)
) ENGINE=InnoDB
DEFAULT CHARSET=utf8
COLLATE=utf8_unicode_ci
COMMENT='Thông tin lưu option/cài đặt đơn giản của người dùng';
