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

-- Dumping structure for table am_web_001.fa_asset_depre_alloc
DROP TABLE IF EXISTS `fa_asset_depre_alloc`;
CREATE TABLE IF NOT EXISTS `fa_asset_depre_alloc` (
  `ALLOC_ID` bigint(20) NOT NULL AUTO_INCREMENT COMMENT 'ID nội bộ của dòng cấu hình phân bổ khấu hao',
  `COMPANY_CD` varchar(20) COLLATE utf8_unicode_ci NOT NULL COMMENT 'Mã công ty',
  `ASSET_ID` bigint(20) NOT NULL COMMENT 'ID tài sản cố định',
  `ALLOC_SEQ` int(11) NOT NULL DEFAULT '1' COMMENT 'Thứ tự dòng phân bổ trong cùng một tài sản',
  `ALLOC_TYPE` varchar(20) COLLATE utf8_unicode_ci NOT NULL COMMENT 'Kiểu phân bổ: PERCENT phân bổ theo tỷ lệ, AMOUNT phân bổ theo số tiền',
  `ALLOC_RATE` decimal(9,4) DEFAULT NULL COMMENT 'Tỷ lệ phân bổ nếu ALLOC_TYPE = PERCENT, ví dụ 60 nghĩa là 60%',
  `FIRST_ALLOC_AMT` decimal(18,2) DEFAULT NULL COMMENT 'Số tiền phân bổ cho tháng đầu nếu ALLOC_TYPE = AMOUNT',
  `NORMAL_ALLOC_AMT` decimal(18,2) DEFAULT NULL COMMENT 'Số tiền phân bổ cho mỗi tháng giữa nếu ALLOC_TYPE = AMOUNT',
  `LAST_ALLOC_AMT` decimal(18,2) DEFAULT NULL COMMENT 'Số tiền phân bổ cho tháng cuối nếu ALLOC_TYPE = AMOUNT',
  `BALANCE_YN` char(1) COLLATE utf8_unicode_ci NOT NULL DEFAULT 'N' COMMENT 'Dòng nhận phần còn lại hoặc chênh lệch làm tròn: Y có, N không',
  `DEBIT_ACCT_CD` varchar(20) COLLATE utf8_unicode_ci NOT NULL COMMENT 'Tài khoản ghi Nợ chi phí khấu hao, ví dụ 627, 641, 642',
  `CREDIT_ACCT_CD` varchar(20) COLLATE utf8_unicode_ci NOT NULL DEFAULT '214' COMMENT 'Tài khoản ghi Có hao mòn lũy kế, thường là 214',
  `DEPARTMENT_ID` bigint(20) DEFAULT NULL COMMENT 'Mã bộ phận nhận chi phí khấu hao',
  `NOTE` varchar(500) COLLATE utf8_unicode_ci DEFAULT NULL COMMENT 'Ghi chú dòng phân bổ',
  `ISDEL` char(1) COLLATE utf8_unicode_ci NOT NULL DEFAULT '0' COMMENT 'Cờ xóa mềm: 0 chưa xóa, 1 đã xóa',
  `CREATE_DT` datetime NOT NULL COMMENT 'Ngày tạo dữ liệu',
  `CREATE_BY` varchar(50) COLLATE utf8_unicode_ci DEFAULT NULL COMMENT 'Người tạo dữ liệu',
  `UPDATE_DT` datetime DEFAULT NULL COMMENT 'Ngày cập nhật dữ liệu',
  `UPDATE_BY` varchar(50) COLLATE utf8_unicode_ci DEFAULT NULL COMMENT 'Người cập nhật dữ liệu',
  PRIMARY KEY (`ALLOC_ID`),
  KEY `idx_fa_asset_depre_alloc_01` (`COMPANY_CD`,`ASSET_ID`),
  KEY `idx_fa_asset_depre_alloc_02` (`COMPANY_CD`,`ASSET_ID`,`ALLOC_SEQ`),
  KEY `idx_fa_asset_depre_alloc_03` (`COMPANY_CD`,`DEBIT_ACCT_CD`),
  KEY `idx_fa_asset_depre_alloc_04` (`COMPANY_CD`,`DEPARTMENT_ID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci COMMENT='Bảng lưu cấu hình phân bổ chi phí khấu hao của từng tài sản theo phần trăm hoặc theo số tiền đầu/giữa/cuối';

-- Data exporting was unselected.
/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IF(@OLD_FOREIGN_KEY_CHECKS IS NULL, 1, @OLD_FOREIGN_KEY_CHECKS) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
