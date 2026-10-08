-- --------------------------------------------------------
-- Host:                         118.69.170.50
-- Server version:               5.6.11-log - Source distribution
-- Server OS:                    Linux
-- HeidiSQL Version:             8.3.0.4694
-- --------------------------------------------------------

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET NAMES utf8 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;

-- Dumping structure for table am_web_001.fa_asset_depre_posted
DROP TABLE IF EXISTS `fa_asset_depre_posted`;
CREATE TABLE IF NOT EXISTS `fa_asset_depre_posted` (
  `POST_ID` bigint(20) NOT NULL AUTO_INCREMENT COMMENT 'ID nội bộ của kết quả khấu hao đã chạy',
  `COMPANY_CD` varchar(20) COLLATE utf8_unicode_ci NOT NULL COMMENT 'Mã công ty',
  `ASSET_ID` bigint(20) NOT NULL COMMENT 'ID tài sản cố định',
  `DEPRE_YM` char(6) COLLATE utf8_unicode_ci NOT NULL COMMENT 'Tháng khấu hao đã chạy, định dạng YYYYMM',
  `DEPRE_TYPE` varchar(20) COLLATE utf8_unicode_ci NOT NULL COMMENT 'Loại tháng khấu hao: FIRST tháng đầu, NORMAL tháng giữa, LAST tháng cuối',
  `DEPRE_AMT` decimal(18,2) NOT NULL DEFAULT '0.00' COMMENT 'Tổng số khấu hao của tài sản trong tháng này',
  `ACCUM_DEPRE_AMT` decimal(18,2) NOT NULL DEFAULT '0.00' COMMENT 'Hao mòn lũy kế của tài sản đến cuối tháng này',
  `END_BOOK_AMT` decimal(18,2) NOT NULL DEFAULT '0.00' COMMENT 'Giá trị còn lại cuối tháng sau khi khấu hao',
  `CHITINFO_ID` bigint(20) DEFAULT NULL COMMENT 'ID chứng từ khấu hao trong bảng chitinfo',
  `CHIT_NO` varchar(100) COLLATE utf8_unicode_ci DEFAULT NULL COMMENT 'Số chứng từ khấu hao, lưu nhanh để tra cứu',
  `LOCK_YN` char(1) COLLATE utf8_unicode_ci NOT NULL DEFAULT 'Y' COMMENT 'Đánh dấu kết quả này đã thuộc kỳ khóa sổ: Y đã khóa, N chưa khóa',
  `CANCEL_YN` char(1) COLLATE utf8_unicode_ci NOT NULL DEFAULT 'N' COMMENT 'Cờ hủy kết quả khấu hao: Y đã hủy, N chưa hủy',
  `CREATE_DT` datetime NOT NULL COMMENT 'Ngày chạy khấu hao',
  `CREATE_BY` varchar(50) COLLATE utf8_unicode_ci DEFAULT NULL COMMENT 'Người chạy khấu hao',
  PRIMARY KEY (`POST_ID`),
  UNIQUE KEY `uk_fa_asset_depre_posted_01` (`COMPANY_CD`,`ASSET_ID`,`DEPRE_YM`),
  KEY `idx_fa_asset_depre_posted_01` (`COMPANY_CD`,`DEPRE_YM`),
  KEY `idx_fa_asset_depre_posted_02` (`COMPANY_CD`,`ASSET_ID`),
  KEY `idx_fa_asset_depre_posted_03` (`COMPANY_CD`,`CHITINFO_ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='Bảng lưu kết quả khấu hao đã chạy theo từng tài sản và từng tháng, liên kết với chứng từ khấu hao trong chitinfo';

-- Data exporting was unselected.
/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IF(@OLD_FOREIGN_KEY_CHECKS IS NULL, 1, @OLD_FOREIGN_KEY_CHECKS) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
