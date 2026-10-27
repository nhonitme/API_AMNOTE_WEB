-- Execute in am_manager. Insert missing translations without overriding standard keys.
INSERT INTO t_message_info (`KEY`, KOR, ENG, VIET, JPN, THA, CHN, COMMENT, REG_DATE, LAST_MIDOFY_DATE)
VALUES
('BOM_ALLOCATION','완제품별 원자재 소요량','Allocate materials by finished product','Phân bổ nguyên vật liệu theo thành phẩm','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_FINISHED_GRID','완제품 목록','Grid 1 – Finished products','Grid 1 – Thành phẩm','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_MATERIAL_GRID','원자재 소요량','Grid 2 – Material requirements','Grid 2 – Định mức nguyên vật liệu','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_EDIT_DEFINITION','소요량 기준 수정','Edit material requirements','Cập nhật định mức','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_RECALCULATE','완제품 목록이 변경되었습니다. 다시 계산하세요.','Finished products changed; recalculate before confirming','Danh sách đã thay đổi, cần Tính lại trước khi xác nhận','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_EMPTY','계산된 원자재가 없습니다.','No calculated material requirements','Chưa có kết quả định mức','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_APPLIED','원자재가 출고 전표에 추가되었습니다. 저장하면 DB에 기록됩니다.','Materials added to IO draft. Save voucher to commit.','Đã chuyển NVL vào lưới IO. Lưu phiếu để ghi database.','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_OPEN_FAILED','원자재 배분 정보를 열지 못했습니다.','Unable to open material allocation','Không mở được bảng phân bổ NVL','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_NO_FINISHED','완제품이 없습니다.','No finished products selected','Chưa có thành phẩm','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_INVALID_QUANTITY','완제품 수량은 0보다 커야 합니다.','Finished product quantity must be positive','Số lượng thành phẩm phải lớn hơn 0','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_QUANTITY','완제품 단위당 원자재 소요량','Material quantity per unit','Định mức / TP','','','','Inventory BOM allocation',NOW(),NULL),
('CALCULATED_QUANTITY','계산 수량','Calculated quantity','Số lượng tính','','','','Inventory BOM allocation',NOW(),NULL),
('CONFIRMED_QUANTITY','확정 수량','Confirmed quantity','Số lượng xác nhận','','','','Inventory BOM allocation',NOW(),NULL),
('FINISHED_PRODUCT','완제품','Finished product','Thành phẩm','','','','Inventory BOM allocation',NOW(),NULL),
('MATERIAL_PRODUCT_CD','원자재 코드','Material code','Mã NVL','','','','Inventory BOM allocation',NOW(),NULL),
('MATERIAL_PRODUCT_NM','원자재명','Material name','Tên NVL','','','','Inventory BOM allocation',NOW(),NULL)
ON DUPLICATE KEY UPDATE `KEY` = VALUES(`KEY`);

SELECT `KEY`, KOR, ENG, VIET FROM t_message_info WHERE `KEY` LIKE 'BOM\\_%';
