-- /cost/cost-object: multilingual UI captions
-- Run against am_manager (MySQL 5.6 / 8.4). Requires UNIQUE or PRIMARY KEY on `KEY`.
-- Existing translations are intentionally preserved; re-running is safe.
SET NAMES utf8mb4;

INSERT INTO `t_message_info` (`KEY`, `KOR`, `ENG`, `VIET`, `JPN`, `THA`, `CHN`, `COMMENT`, `REG_DATE`, `LAST_MIDOFY_DATE`) VALUES
  ('ALL', '모두', 'All', 'Tất cả', 'すべて', 'ทั้งหมด', '全部', 'Cost object / Page', NOW(), NULL),
  ('COST_OBJECT_MISSING', '미등록', 'Not registered', 'Chưa khai báo', '未登録', 'ยังไม่ได้ลงทะเบียน', '未登记', 'Cost object / Page', NOW(), NULL),
  ('COST_OBJECT_REGISTERED', '등록됨', 'Registered', 'Đã khai báo', '登録済み', 'ลงทะเบียนแล้ว', '已登记', 'Cost object / Page', NOW(), NULL),
  ('COST_OBJECT_NO_MISSING', '선택한 미등록 완제품이 없습니다.', 'No unregistered finished products selected.', 'Không có thành phẩm chưa khai báo được chọn.', '未登録の完成品が選択されていません。', 'ไม่ได้เลือกสินค้าสำเร็จรูปที่ยังไม่ได้ลงทะเบียน', '未选择未登记的成品。', 'Cost object / Page', NOW(), NULL),
  ('COST_OBJECT_MAX_200', '한 번에 최대 200개 완제품을 동기화할 수 있습니다.', 'Synchronize up to 200 finished products at a time.', 'Mỗi lần đồng bộ tối đa 200 thành phẩm.', '一度に同期できる完成品は最大200件です。', 'ซิงค์สินค้าสำเร็จรูปได้สูงสุด 200 รายการต่อครั้ง', '每次最多同步200个成品。', 'Cost object / Page', NOW(), NULL),
  ('COST_OBJECT_SYNC_CONFIRM', '완제품 {0}개에 대한 원가 집계 대상을 생성하시겠습니까?', 'Create cost accumulation objects for {0} finished products?', 'Tạo đối tượng tập hợp chi phí cho {0} thành phẩm?', '完成品{0}件の原価集計対象を作成しますか？', 'สร้างวัตถุรวบรวมต้นทุนสำหรับสินค้าสำเร็จรูป {0} รายการหรือไม่', '为{0}个成品创建成本归集对象？', 'Cost object / Page', NOW(), NULL),
  ('CONFIRM', '확인', 'Confirm', 'Xác nhận', '確認', 'ยืนยัน', '确认', 'Cost object / Page', NOW(), NULL),
  ('COST_OBJECT_SYNC_RESULT', '생성: {0}, 기존: {1}, 오류: {2}', 'Created: {0}, existing: {1}, errors: {2}', 'Đã tạo: {0}, đã tồn tại: {1}, lỗi: {2}', '作成: {0}、既存: {1}、エラー: {2}', 'สร้างแล้ว: {0}, มีอยู่แล้ว: {1}, ข้อผิดพลาด: {2}', '已创建：{0}，已存在：{1}，错误：{2}', 'Cost object / Page', NOW(), NULL),
  ('COST_OBJECT_SYNC_ERROR', '원가 집계 대상을 동기화할 수 없습니다.', 'Unable to synchronize cost accumulation objects.', 'Không đồng bộ được đối tượng tập hợp chi phí.', '原価集計対象を同期できません。', 'ไม่สามารถซิงค์วัตถุรวบรวมต้นทุนได้', '无法同步成本归集对象。', 'Cost object / Page', NOW(), NULL),
  ('LOAD_FAILED', '데이터를 불러오지 못했습니다.', 'Failed to load data.', 'Không tải được dữ liệu.', 'データの読み込みに失敗しました。', 'โหลดข้อมูลไม่สำเร็จ', '数据加载失败。', 'Cost object / Page', NOW(), NULL),
  ('STATUS', '상태', 'Status', 'Trạng thái', '状態', 'สถานะ', '状态', 'Cost object / Page', NOW(), NULL),
  ('EXECUTE', '실행', 'Execute', 'Thực hiện', '実行', 'ดำเนินการ', '执行', 'Cost object / Page', NOW(), NULL),
  ('COST_OBJECT_MASTER_CATALOG', '원가 집계 대상', 'Cost Accumulation Objects', 'Đối tượng tập hợp chi phí', '原価集計対象', 'รายการวัตถุรวบรวมต้นทุน', '成本归集对象', 'Cost object / Page', NOW(), NULL),
  ('INVENTORY_ITEM_SETUP', '재고 품목 등록', 'Inventory Item Setup', 'Khai báo hàng tồn kho', '在庫品目設定', 'ตั้งค่ารายการสินค้าคงคลัง', '库存商品设置', 'Cost object / Page', NOW(), NULL),
  ('COST_OBJECT_SYNC_SELECTED', '선택 항목 생성', 'Create selected objects', 'Tạo đối tượng đã chọn', '選択した対象を作成', 'สร้างรายการที่เลือก', '创建所选对象', 'Cost object / Page', NOW(), NULL),
  ('COST_OBJECT_SYNC_ALL', '미등록 전체 동기화', 'Sync all missing', 'Đồng bộ tất cả còn thiếu', '未登録の全件を同期', 'ซิงค์รายการที่ยังขาดทั้งหมด', '同步全部未登记项', 'Cost object / Page', NOW(), NULL),
  ('COST_OBJECT_TOTAL_PRODUCTS', '전체 완제품', 'Total finished products', 'Tổng thành phẩm', '完成品総数', 'สินค้าสำเร็จรูปทั้งหมด', '成品总数', 'Cost object / Page', NOW(), NULL),
  ('COST_OBJECT_PROGRESS', '완료율', 'Completion rate', 'Tỷ lệ hoàn thành', '完了率', 'อัตราความสำเร็จ', '完成率', 'Cost object / Page', NOW(), NULL),
  ('COST_OBJECT_SETUP', '원가 집계 대상 등록', 'Cost Accumulation Object Setup', 'Khai báo đối tượng tập hợp chi phí', '原価集計対象の設定', 'ตั้งค่าวัตถุรวบรวมต้นทุน', '成本归集对象设置', 'Cost object / Page', NOW(), NULL),
  ('COST_OBJECT_RULE', '완제품 1개당 원가 집계 대상 1개를 사용하며 코드는 동일합니다.', 'One cost accumulation object per finished product; the codes must match.', 'Một thành phẩm tương ứng một đối tượng; mã đối tượng = mã thành phẩm.', '完成品ごとに原価集計対象を1件設定し、コードを一致させます。', 'สินค้าสำเร็จรูปหนึ่งรายการต่อวัตถุรวบรวมต้นทุนหนึ่งรายการ โดยใช้รหัสเดียวกัน', '每个成品对应一个成本归集对象，编码相同。', 'Cost object / Page', NOW(), NULL),
  ('COST_OBJECT_PRODUCT_CODE', '완제품 코드', 'Finished product code', 'Mã thành phẩm', '完成品コード', 'รหัสสินค้าสำเร็จรูป', '成品编码', 'Cost object / Page', NOW(), NULL),
  ('COST_OBJECT_PRODUCT_NAME', '완제품명', 'Finished product name', 'Tên thành phẩm', '完成品名', 'ชื่อสินค้าสำเร็จรูป', '成品名称', 'Cost object / Page', NOW(), NULL),
  ('COST_OBJECT_ACCOUNT', '계정과목', 'Account', 'Tài khoản', '勘定科目', 'บัญชี', '科目', 'Cost object / Page', NOW(), NULL),
  ('COST_OBJECT_CD', '원가 집계 대상 코드', 'Cost object code', 'Mã đối tượng THCP', '原価集計対象コード', 'รหัสวัตถุรวบรวมต้นทุน', '成本归集对象编码', 'Cost object / Page', NOW(), NULL),
  ('COST_OBJECT_NAME', '원가 집계 대상명', 'Cost object name', 'Tên đối tượng THCP', '原価集計対象名', 'ชื่อวัตถุรวบรวมต้นทุน', '成本归集对象名称', 'Cost object / Page', NOW(), NULL),
  ('ACTION', '작업', 'Action', 'Thao tác', '操作', 'การดำเนินการ', '操作', 'Cost object / Page', NOW(), NULL),
  ('CREATE', '생성', 'Create', 'Tạo', '作成', 'สร้าง', '创建', 'Cost object / Page', NOW(), NULL),
  ('TOTAL', '합계', 'Total', 'Tổng', '合計', 'รวม', '合计', 'Cost object / Page', NOW(), NULL),
  ('ITEM', '항목', 'item', 'mục', '項目', 'รายการ', '项', 'Cost object / Page', NOW(), NULL),
  ('LOADING', '데이터 로딩 중...', 'Loading data...', 'Đang tải dữ liệu...', 'データを読み込み中...', 'กำลังโหลดข้อมูล...', '正在加载数据...', 'Cost object / Page', NOW(), NULL)
ON DUPLICATE KEY UPDATE `KEY` = `KEY`;

-- Verify all captions used by the page.
SELECT `KEY`, `KOR`, `ENG`, `VIET`, `JPN`, `THA`, `CHN`
FROM `t_message_info`
WHERE `KEY` IN ('ALL', 'COST_OBJECT_MISSING', 'COST_OBJECT_REGISTERED', 'COST_OBJECT_NO_MISSING', 'COST_OBJECT_MAX_200', 'COST_OBJECT_SYNC_CONFIRM', 'CONFIRM', 'COST_OBJECT_SYNC_RESULT', 'COST_OBJECT_SYNC_ERROR', 'LOAD_FAILED', 'STATUS', 'EXECUTE', 'COST_OBJECT_MASTER_CATALOG', 'INVENTORY_ITEM_SETUP', 'COST_OBJECT_SYNC_SELECTED', 'COST_OBJECT_SYNC_ALL', 'COST_OBJECT_TOTAL_PRODUCTS', 'COST_OBJECT_PROGRESS', 'COST_OBJECT_SETUP', 'COST_OBJECT_RULE', 'COST_OBJECT_PRODUCT_CODE', 'COST_OBJECT_PRODUCT_NAME', 'COST_OBJECT_ACCOUNT', 'COST_OBJECT_CD', 'COST_OBJECT_NAME', 'ACTION', 'CREATE', 'TOTAL', 'ITEM', 'LOADING')
ORDER BY `KEY`;
