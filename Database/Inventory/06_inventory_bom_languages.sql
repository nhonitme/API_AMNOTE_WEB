-- Execute in am_manager. Upsert translations for Inventory BOM allocation (Xuất NVL theo định mức).
INSERT INTO t_message_info (`KEY`, KOR, ENG, VIET, JPN, THA, CHN, COMMENT, REG_DATE, LAST_MIDOFY_DATE)
VALUES
-- UI labels / actions
('BOM_ALLOCATION','원자재 소요량 출고','Issue materials by BOM','Xuất NVL theo định mức','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_ALLOCATION_TITLE','완제품별 원자재 소요량','Allocate materials by finished product','Phân bổ nguyên vật liệu theo thành phẩm','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_FINISHED_GRID','완제품 목록','Grid 1 – Finished products','Grid 1 – Thành phẩm','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_MATERIAL_GRID','원자재 소요량','Grid 2 – Material requirements','Grid 2 – Định mức nguyên vật liệu','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_EDIT_DEFINITION','소요량 기준 수정','Edit material requirements','Cập nhật định mức','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_NEEDS_RECALCULATE','다시 계산 필요','Recalculation required','Cần Tính lại','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_QUANTITY','완제품 단위당 원자재 소요량','Material quantity per unit','Định mức / TP','','','','Inventory BOM allocation',NOW(),NULL),
('CALCULATED_QUANTITY','계산 수량','Calculated quantity','Số lượng tính','','','','Inventory BOM allocation',NOW(),NULL),
('CONFIRMED_QUANTITY','확정 수량','Confirmed quantity','Số lượng xác nhận','','','','Inventory BOM allocation',NOW(),NULL),
('FINISHED_PRODUCT','완제품','Finished product','Thành phẩm','','','','Inventory BOM allocation',NOW(),NULL),
('MATERIAL_PRODUCT_CD','원자재 코드','Material code','Mã NVL','','','','Inventory BOM allocation',NOW(),NULL),
('MATERIAL_PRODUCT_NM','원자재명','Material name','Tên NVL','','','','Inventory BOM allocation',NOW(),NULL),
('SELECT_PRODUCT','완제품 선택','Select finished product','Chọn thành phẩm','','','','Inventory BOM allocation',NOW(),NULL),
('DELETE_SELECTED','선택 삭제','Delete selected','Xóa đã chọn','','','','Inventory BOM allocation',NOW(),NULL),
('CALCULATE','다시 계산','Recalculate','Tính lại','','','','Inventory BOM allocation',NOW(),NULL),
('IMPORT_EXCEL','엑셀 가져오기','Import Excel','Import Excel','','','','Inventory BOM allocation',NOW(),NULL),
-- Messages / validation
('BOM_RECALCULATE','완제품 목록이 변경되었습니다. 다시 계산하세요.','Finished products changed; recalculate before confirming','Danh sách đã thay đổi, cần Tính lại trước khi xác nhận','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_EMPTY','계산된 원자재가 없습니다.','No calculated material requirements','Chưa có kết quả định mức','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_APPLIED','원자재가 출고 전표에 추가되었습니다. 저장하면 DB에 기록됩니다.','Materials added to IO draft. Save voucher to commit.','Đã chuyển nguyên vật liệu vào lưới IO. Lưu phiếu để ghi database.','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_OPEN_FAILED','원자재 배분 정보를 열지 못했습니다.','Unable to open material allocation','Không mở được bảng phân bổ NVL','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_NO_FINISHED','완제품이 없습니다.','No finished products selected','Chưa có thành phẩm','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_INVALID_QUANTITY','완제품 수량은 0보다 커야 합니다.','Finished product quantity must be greater than 0','Số lượng thành phẩm phải lớn hơn 0','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_LOAD_CATALOG_FAILED','완제품/창고 목록을 불러오지 못했습니다.','Unable to load finished product / store catalog','Không đọc được danh mục thành phẩm/kho','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_CALCULATE_FAILED','원자재 소요량을 계산하지 못했습니다.','Unable to calculate material requirements','Không tính được định mức','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_MISSING_DEFINITION','{0}개 완제품에 소요량이 없습니다. Grid 1을 확인하세요.','{0} finished product(s) have no BOM. Please check Grid 1.','{0} thành phẩm chưa khai báo định mức. Vui lòng kiểm tra Grid 1.','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_MISSING_INPUT_STORE','일부 원자재에 입고 이력이 없어 창고를 지정할 수 없습니다. Grid 2에서 창고를 선택하세요.','Some materials have no inbound history for store assignment. Please select a store on Grid 2.','Một số NVL chưa có lịch sử nhập kho để gán mã kho. Vui lòng chọn kho trên Grid 2.','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_ORPHAN_MATERIAL','원자재 행에 대응되는 완제품이 없습니다.','Material row has no matching finished product','Dòng nguyên vật liệu không có thành phẩm tương ứng','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_NO_CONFIRMED_QTY','출고 수량이 유효한 원자재가 없습니다.','No materials with a valid issue quantity','Không có nguyên vật liệu có số lượng xuất hợp lệ','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_STORE_REQUIRED','출고할 모든 원자재 행에 출고 창고를 선택하세요.','Select an issue store for every material row to issue','Vui lòng chọn kho xuất cho mọi dòng NVL cần xuất','','','','Inventory BOM allocation',NOW(),NULL),
-- Excel import
('BOM_IMPORT_TITLE','엑셀에서 완제품 가져오기','Import finished products from Excel','Import thành phẩm từ Excel','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_IMPORT_DESC','PRODUCT_CD, QUANTITY 열을 사용합니다. 양식은 import 화면에서 다운로드하세요.','Use PRODUCT_CD and QUANTITY columns. Download the template in the import form.','Cột PRODUCT_CD và QUANTITY. Tải file mẫu trong form import.','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_IMPORT_NO_VALID_ROWS','엑셀에 유효한 완제품이 없습니다.','No valid finished products in Excel','Không có thành phẩm hợp lệ trong Excel','','','','Inventory BOM allocation',NOW(),NULL),
('BOM_IMPORT_PARTIAL','유효한 행은 반영되었습니다. 오류 행을 확인하세요.','Valid rows loaded; check error rows','Đã nạp các dòng hợp lệ; có dòng lỗi cần kiểm tra','','','','Inventory BOM allocation',NOW(),NULL),
('IMPORT_EMPTY_FILE','엑셀 파일에 데이터 시트가 없습니다.','Excel file has no data sheet','File Excel không có sheet dữ liệu','','','','Inventory BOM allocation',NOW(),NULL),
('IMPORT_SUCCESS','완제품 목록을 가져왔습니다.','Finished product list loaded','Đã tải danh sách thành phẩm','','','','Inventory BOM allocation',NOW(),NULL),
('PRODUCT_NOT_FOUND','완제품을 찾을 수 없습니다','Finished product not found','Không tìm thấy thành phẩm','','','','Inventory BOM allocation',NOW(),NULL)
ON DUPLICATE KEY UPDATE
  KOR = VALUES(KOR),
  ENG = VALUES(ENG),
  VIET = VALUES(VIET),
  COMMENT = VALUES(COMMENT),
  LAST_MIDOFY_DATE = NOW();

SELECT `KEY`, KOR, ENG, VIET FROM t_message_info
WHERE `KEY` LIKE 'BOM\\_%'
   OR `KEY` IN (
     'CALCULATED_QUANTITY', 'CONFIRMED_QUANTITY', 'FINISHED_PRODUCT',
     'MATERIAL_PRODUCT_CD', 'MATERIAL_PRODUCT_NM', 'SELECT_PRODUCT',
     'DELETE_SELECTED', 'CALCULATE', 'IMPORT_EXCEL',
     'IMPORT_EMPTY_FILE', 'IMPORT_SUCCESS', 'PRODUCT_NOT_FOUND'
   )
ORDER BY `KEY`;
