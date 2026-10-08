
CREATE TABLE IF NOT EXISTS `info_login_tax_office_einvoice` (
  `COMPANY_CD` varchar(20) COLLATE utf8_unicode_ci NOT NULL,
  `USERNAME` varchar(50) COLLATE utf8_unicode_ci NOT NULL,
  `PASSWORD` varchar(50) COLLATE utf8_unicode_ci NOT NULL,
  `TOKEN` varchar(2000) COLLATE utf8_unicode_ci DEFAULT NULL COMMENT 'Bearer token GDT — dùng lại đến khi hết hạn/401',
  `NUMBER_LOGIN` int(2) DEFAULT '0' COMMENT 'menu E-K Login sai 3 lần  là bị khóa; SET =0 : để thử lại ở menu E-K',
  `DATE_CREATE` timestamp NULL DEFAULT CURRENT_TIMESTAMP,
  `DATE_UPDATE` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `IS_ACTIVE` char(1) COLLATE utf8_unicode_ci DEFAULT '1',
  `IS_DEFAULT` char(1) COLLATE utf8_unicode_ci NOT NULL DEFAULT '0' COMMENT 'Tài khoản mặc định hoặc tài khoản chọn lấy dữ liệu lần cuối',
  PRIMARY KEY (`COMPANY_CD`,`USERNAME`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

-- Mở rộng TOKEN nếu bảng cũ còn varchar(500):
-- ALTER TABLE info_login_tax_office_einvoice MODIFY COLUMN TOKEN varchar(2000) COLLATE utf8_unicode_ci DEFAULT NULL;
