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

-- Dumping structure for table am_web_001.fa_asset_depre_posted_alloc
DROP TABLE IF EXISTS `fa_asset_depre_posted_alloc`;
CREATE TABLE IF NOT EXISTS `fa_asset_depre_posted_alloc` (
  `POST_ALLOC_ID` bigint(20) NOT NULL AUTO_INCREMENT COMMENT 'ID nội bộ của dòng phân bổ khấu hao đã chạy',
  `POST_ID` bigint(20) NOT NULL COMMENT 'ID kết quả khấu hao tháng của tài sản, liên kết với fa_asset_depre_posted',
  `COMPANY_CD` varchar(20) COLLATE utf8_unicode_ci NOT NULL COMMENT 'Mã công ty',
  `ASSET_ID` bigint(20) NOT NULL COMMENT 'ID tài sản cố định',
  `DEPRE_YM` char(6) COLLATE utf8_unicode_ci NOT NULL COMMENT 'Tháng khấu hao, định dạng YYYYMM',
  `ALLOC_ID` bigint(20) DEFAULT NULL COMMENT 'ID dòng cấu hình phân bổ gốc, liên kết với fa_asset_depre_alloc',
  `ALLOC_SEQ` int(11) NOT NULL DEFAULT '1' COMMENT 'Thứ tự dòng phân bổ',
  `DEBIT_ACCT_CD` varchar(20) COLLATE utf8_unicode_ci NOT NULL COMMENT 'Tài khoản ghi Nợ chi phí khấu hao',
  `CREDIT_ACCT_CD` varchar(20) COLLATE utf8_unicode_ci NOT NULL DEFAULT '214' COMMENT 'Tài khoản ghi Có hao mòn lũy kế',
  `DEPARTMENT_ID` bigint(20) DEFAULT NULL COMMENT 'Mã đối tượng tập hợp chi phí',
  `ALLOC_TYPE` varchar(20) COLLATE utf8_unicode_ci NOT NULL COMMENT 'Kiểu phân bổ đã áp dụng: PERCENT hoặc AMOUNT',
  `ALLOC_RATE` decimal(9,4) DEFAULT NULL COMMENT 'Tỷ lệ phân bổ đã áp dụng nếu phân bổ theo phần trăm',
  `DEPRE_AMT` decimal(18,2) NOT NULL DEFAULT '0.00' COMMENT 'Số tiền khấu hao thực tế phân bổ vào dòng này',
  `BALANCE_YN` char(1) COLLATE utf8_unicode_ci NOT NULL DEFAULT 'N' COMMENT 'Dòng này có phải dòng nhận phần còn lại hoặc chênh lệch làm tròn hay không',
  `CHITDETAIL_ID` bigint(20) DEFAULT NULL COMMENT 'ID dòng chi tiết chứng từ trong bảng chitdetail',
  `CREATE_DT` datetime NOT NULL COMMENT 'Ngày tạo dòng phân bổ khấu hao',
  `CREATE_BY` varchar(50) COLLATE utf8_unicode_ci DEFAULT NULL COMMENT 'Người tạo dòng phân bổ khấu hao',
  PRIMARY KEY (`POST_ALLOC_ID`),
  KEY `idx_fa_asset_depre_posted_alloc_01` (`COMPANY_CD`,`ASSET_ID`,`DEPRE_YM`),
  KEY `idx_fa_asset_depre_posted_alloc_02` (`COMPANY_CD`,`POST_ID`),
  KEY `idx_fa_asset_depre_posted_alloc_03` (`COMPANY_CD`,`DEBIT_ACCT_CD`),
  KEY `idx_fa_asset_depre_posted_alloc_04` (`COMPANY_CD`),
  KEY `idx_fa_asset_depre_posted_alloc_05` (`COMPANY_CD`,`CHITDETAIL_ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='Bảng lưu chi tiết phân bổ khấu hao đã chạy, mỗi dòng liên kết với một dòng chứng từ trong chitdetail';

-- Data exporting was unselected.
/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IF(@OLD_FOREIGN_KEY_CHECKS IS NULL, 1, @OLD_FOREIGN_KEY_CHECKS) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
