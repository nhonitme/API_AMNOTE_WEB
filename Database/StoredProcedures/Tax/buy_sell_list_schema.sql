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

-- Dumping structure for table am_web_001.buy_list_einvoice
DROP TABLE IF EXISTS `buy_list_einvoice`;
CREATE TABLE IF NOT EXISTS `buy_list_einvoice` (
  `mhdon` varchar(50) COLLATE utf8_unicode_ci NOT NULL DEFAULT '',
  `tthai` char(2) COLLATE utf8_unicode_ci DEFAULT '',
  `khmshdon` varchar(1) COLLATE utf8_unicode_ci DEFAULT '',
  `khhdon` varchar(6) COLLATE utf8_unicode_ci DEFAULT '',
  `tdlap` char(8) COLLATE utf8_unicode_ci DEFAULT '',
  `nky` char(8) COLLATE utf8_unicode_ci DEFAULT '' COMMENT 'Ngày ký',
  `shdon` varchar(20) COLLATE utf8_unicode_ci DEFAULT '',
  `dvtte` varchar(3) COLLATE utf8_unicode_ci DEFAULT '',
  `mtdtchieu` varchar(100) COLLATE utf8_unicode_ci DEFAULT '',
  `nbten` varchar(200) COLLATE utf8_unicode_ci DEFAULT '',
  `nbdchi` varchar(200) COLLATE utf8_unicode_ci DEFAULT '',
  `nbmst` varchar(14) COLLATE utf8_unicode_ci DEFAULT '',
  `nmten` varchar(200) COLLATE utf8_unicode_ci DEFAULT '',
  `nmdchi` varchar(200) COLLATE utf8_unicode_ci DEFAULT '',
  `nmmst` varchar(14) COLLATE utf8_unicode_ci DEFAULT '',
  `tgia` double NOT NULL DEFAULT '1',
  `tgtcthue` double NOT NULL DEFAULT '0',
  `tgtthue` double NOT NULL DEFAULT '0',
  `tgtttbso` double NOT NULL DEFAULT '0',
  `tgtphi` double NOT NULL DEFAULT '0',
  `ttcktmai` double NOT NULL DEFAULT '0',
  `GChu` varchar(500) COLLATE utf8_unicode_ci NOT NULL DEFAULT '',
  `AUTO_CHIT_CD` varchar(20) COLLATE utf8_unicode_ci DEFAULT '',
  `IS_ATTACH_FILE` char(1) COLLATE utf8_unicode_ci NOT NULL DEFAULT '0' COMMENT 'Đính kèm thêm mẫu html khi chọn nút đính kèm E-B',
  `LATEST_YMD` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `ISDEL` char(1) COLLATE utf8_unicode_ci DEFAULT '',
  `USERID` varchar(20) COLLATE utf8_unicode_ci DEFAULT '',
  PRIMARY KEY (`mhdon`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

-- Data exporting was unselected.
/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IF(@OLD_FOREIGN_KEY_CHECKS IS NULL, 1, @OLD_FOREIGN_KEY_CHECKS) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
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

-- Dumping structure for table am_web_001.buy_list_json
CREATE TABLE IF NOT EXISTS `buy_list_json` (
  `mhdon` varchar(50) COLLATE utf8_unicode_ci NOT NULL DEFAULT '',
  `json` mediumtext COLLATE utf8_unicode_ci,
  `ISDEL` char(1) COLLATE utf8_unicode_ci DEFAULT '0',
  PRIMARY KEY (`mhdon`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

-- Data exporting was unselected.
/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IF(@OLD_FOREIGN_KEY_CHECKS IS NULL, 1, @OLD_FOREIGN_KEY_CHECKS) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
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

-- Dumping structure for table am_web_001.sell_list_einvoice
CREATE TABLE IF NOT EXISTS `sell_list_einvoice` (
  `mhdon` varchar(50) COLLATE utf8_unicode_ci NOT NULL DEFAULT '',
  `tthai` char(2) COLLATE utf8_unicode_ci DEFAULT '',
  `khmshdon` varchar(1) COLLATE utf8_unicode_ci DEFAULT '',
  `khhdon` varchar(6) COLLATE utf8_unicode_ci DEFAULT '',
  `tdlap` char(8) COLLATE utf8_unicode_ci DEFAULT '',
  `shdon` varchar(20) COLLATE utf8_unicode_ci DEFAULT '',
  `nky` char(8) COLLATE utf8_unicode_ci DEFAULT '' COMMENT 'Ngày ký',
  `dvtte` varchar(3) COLLATE utf8_unicode_ci DEFAULT '',
  `mtdtchieu` varchar(100) COLLATE utf8_unicode_ci DEFAULT '',
  `nbten` varchar(200) COLLATE utf8_unicode_ci DEFAULT '',
  `nbdchi` varchar(200) COLLATE utf8_unicode_ci DEFAULT '',
  `nbmst` varchar(14) COLLATE utf8_unicode_ci DEFAULT '',
  `nmten` varchar(200) COLLATE utf8_unicode_ci DEFAULT '',
  `nmdchi` varchar(200) COLLATE utf8_unicode_ci DEFAULT '',
  `nmmst` varchar(14) COLLATE utf8_unicode_ci DEFAULT '',
  `tgia` double NOT NULL DEFAULT '1',
  `tgtcthue` double NOT NULL DEFAULT '0',
  `tgtthue` double NOT NULL DEFAULT '0',
  `tgtttbso` double NOT NULL DEFAULT '0',
  `tgtphi` double NOT NULL DEFAULT '0',
  `ttcktmai` double NOT NULL DEFAULT '0',
  `GChu` varchar(500) COLLATE utf8_unicode_ci NOT NULL DEFAULT '',
  `AUTO_CHIT_CD` varchar(20) COLLATE utf8_unicode_ci DEFAULT '',
  `IS_ATTACH_FILE` char(1) COLLATE utf8_unicode_ci NOT NULL DEFAULT '0' COMMENT 'Đính kèm thêm mẫu html khi chọn nút đính kèm E-B',
  `LATEST_YMD` timestamp NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `ISDEL` char(1) COLLATE utf8_unicode_ci DEFAULT '',
  `USERID` varchar(20) COLLATE utf8_unicode_ci DEFAULT '',
  PRIMARY KEY (`mhdon`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

-- Data exporting was unselected.
/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IF(@OLD_FOREIGN_KEY_CHECKS IS NULL, 1, @OLD_FOREIGN_KEY_CHECKS) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
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

-- Dumping structure for table am_web_001.sell_list_json
CREATE TABLE IF NOT EXISTS `sell_list_json` (
  `mhdon` varchar(50) COLLATE utf8_unicode_ci NOT NULL DEFAULT '',
  `json` mediumtext COLLATE utf8_unicode_ci,
  `ISDEL` char(1) COLLATE utf8_unicode_ci DEFAULT '0',
  PRIMARY KEY (`mhdon`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 COLLATE=utf8_unicode_ci;

-- Data exporting was unselected.
/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IF(@OLD_FOREIGN_KEY_CHECKS IS NULL, 1, @OLD_FOREIGN_KEY_CHECKS) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
