-- AMnote Web - System / License Information
-- Run in am_manager on MySQL 5.6+. UTF-8 compatible.
-- The keys below are shared by frontend translate(KEY) and backend Common.getLanguage(KEY).
SET NAMES utf8;

INSERT INTO `t_message_info` (`KEY`, `KOR`, `ENG`, `VIET`, `JPN`, `THA`, `CHN`, `COMMENT`, `REG_DATE`, `LAST_MIDOFY_DATE`) VALUES
  ('LICENSE_INFORMATION', '라이선스 정보', 'License Information', 'Thông tin License', 'ライセンス情報', 'ข้อมูลใบอนุญาต', '许可证信息', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_REFRESH', '새로고침', 'Refresh', 'Tải lại', '更新', 'รีเฟรช', '刷新', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_LOADING', '라이선스 정보를 불러오는 중...', 'Loading license information...', 'Đang tải thông tin License...', 'ライセンス情報を読み込み中...', 'กำลังโหลดข้อมูลใบอนุญาต...', '正在加载许可证信息...', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_LOAD_FAILED', '라이선스 정보를 불러올 수 없습니다.', 'Failed to load license information.', 'Không thể tải thông tin License.', 'ライセンス情報を読み込めません。', 'ไม่สามารถโหลดข้อมูลใบอนุญาตได้', '无法加载许可证信息。', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_RETRY', '다시 시도', 'Try again', 'Thử lại', '再試行', 'ลองอีกครั้ง', '重试', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_NOT_CONFIGURED', '라이선스 정보가 없습니다.', 'No license information available', 'Chưa có thông tin License', 'ライセンス情報がありません', 'ยังไม่มีข้อมูลใบอนุญาต', '尚无许可证信息', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_NOT_CONFIGURED_DESC', '이 회사의 라이선스가 설정되지 않았습니다. AMNote 고객 지원팀에 문의하세요.', 'A license has not been configured for this company. Please contact AMNote support.', 'Công ty này chưa được cấu hình thông tin License. Vui lòng liên hệ bộ phận hỗ trợ AMNote.', 'この会社のライセンスは未設定です。AMNoteサポートにお問い合わせください。', 'ยังไม่ได้ตั้งค่าใบอนุญาตสำหรับบริษัทนี้ โปรดติดต่อฝ่ายสนับสนุน AMNote', '该公司的许可证尚未配置。请联系 AMNote 支持团队。', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_STATUS_ACTIVE', '사용 중', 'Active', 'Đang hoạt động', '有効', 'ใช้งานอยู่', '有效', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_STATUS_EXPIRING', '만료 예정', 'Expiring soon', 'Sắp hết hạn', 'まもなく期限切れ', 'ใกล้หมดอายุ', '即将到期', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_STATUS_EXPIRED', '만료됨', 'Expired', 'Đã hết hạn', '期限切れ', 'หมดอายุแล้ว', '已过期', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_STATUS_SUSPENDED', '사용 중지', 'Suspended', 'Tạm ngưng', '一時停止中', 'ระงับการใช้งาน', '已暂停', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_PACKAGE', '소프트웨어 요금제', 'Software plan', 'Gói phần mềm', 'ソフトウェアプラン', 'แพ็กเกจซอฟต์แวร์', '软件套餐', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_EXPIRATION_DATE', '라이선스 만료일', 'Expiration date', 'Ngày hết hạn', '有効期限', 'วันหมดอายุ', '到期日期', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_NO_EXPIRY', '무제한 / 미설정', 'Unlimited / Not set', 'Không giới hạn / chưa thiết lập', '無期限 / 未設定', 'ไม่จำกัด / ยังไม่ได้ตั้งค่า', '无限期 / 未设置', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_REMAINING_DAYS', '{0}일 남음', '{0} days remaining', 'Còn {0} ngày', '残り{0}日', 'เหลืออีก {0} วัน', '剩余 {0} 天', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_USER_COUNT', '사용자', 'Users', 'Người dùng', 'ユーザー', 'ผู้ใช้งาน', '用户', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_GENERAL_TAB', '일반 정보', 'General information', 'Thông tin chung', '基本情報', 'ข้อมูลทั่วไป', '基本信息', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_RENEWAL_TAB', '갱신 이력', 'Renewal history', 'Lịch sử gia hạn', '更新履歴', 'ประวัติการต่ออายุ', '续费历史', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_COMPANY_NAME', '회사명', 'Company name', 'Tên công ty', '会社名', 'ชื่อบริษัท', '公司名称', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_COMPANY_CODE', '회사 코드', 'Company code', 'Mã công ty', '会社コード', 'รหัสบริษัท', '公司代码', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_KEY_MASKED', '라이선스 키 (일부 숨김)', 'License key (masked)', 'Mã License (đã ẩn)', 'ライセンスキー（マスク表示）', 'รหัสใบอนุญาต (ซ่อนบางส่วน)', '许可证密钥（已隐藏）', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_PACKAGE_LABEL', '라이선스 요금제', 'License plan', 'Gói License', 'ライセンスプラン', 'แพ็กเกจใบอนุญาต', '许可证套餐', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_TYPE_LABEL', '라이선스 유형', 'License type', 'Loại giấy phép', 'ライセンス種別', 'ประเภทใบอนุญาต', '许可证类型', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_START_DATE', '시작일', 'Start date', 'Ngày bắt đầu', '開始日', 'วันที่เริ่มต้น', '开始日期', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_MAX_USERS', '최대 사용자 수', 'Maximum users', 'Số người dùng tối đa', '最大ユーザー数', 'จำนวนผู้ใช้สูงสุด', '最大用户数', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_MAX_CONCURRENT_USERS', '최대 동시 접속 수', 'Maximum concurrent sessions', 'Số phiên đồng thời tối đa', '最大同時セッション数', 'จำนวนเซสชันพร้อมกันสูงสุด', '最大并发会话数', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_PROVIDER', '공급사', 'Provider', 'Đơn vị cung cấp', '提供元', 'ผู้ให้บริการ', '提供商', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_RENEWAL_DATE', '갱신일', 'Renewal date', 'Ngày gia hạn', '更新日', 'วันที่ต่ออายุ', '续费日期', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_OLD_END_DATE', '이전 만료일', 'Previous expiration', 'Hạn cũ', '以前の有効期限', 'วันหมดอายุเดิม', '原到期日期', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_NEW_END_DATE', '새 만료일', 'New expiration', 'Hạn mới', '新しい有効期限', 'วันหมดอายุใหม่', '新到期日期', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_NOTE', '비고', 'Note', 'Ghi chú', '備考', 'หมายเหตุ', '备注', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_NO_RENEWAL_HISTORY', '갱신 이력이 없습니다.', 'No renewal history', 'Chưa có lịch sử gia hạn.', '更新履歴はありません', 'ไม่มีประวัติการต่ออายุ', '暂无续费记录', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_SUPPORT_TITLE', '라이선스 지원 및 갱신', 'License support & renewal', 'Hỗ trợ & gia hạn License', 'ライセンスのサポート・更新', 'การสนับสนุนและต่ออายุใบอนุญาต', '许可证支持与续费', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_SUPPORT_DESC', '라이선스 갱신 또는 변경은 AMNote에 문의하세요. 이 화면은 조회 전용입니다.', 'Contact AMNote to renew or adjust your license. This page is read-only.', 'Vui lòng liên hệ AMNote nếu cần gia hạn hoặc điều chỉnh License. Màn hình này chỉ cho phép xem.', 'ライセンスの更新・変更はAMNoteにお問い合わせください。この画面は閲覧専用です。', 'ติดต่อ AMNote หากต้องการต่ออายุหรือเปลี่ยนแปลงใบอนุญาต หน้านี้ดูได้อย่างเดียว', '如需续费或调整许可证，请联系 AMNote。本页面仅供查看。', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_PLAN_STANDARD', '스탠다드', 'Standard', 'Tiêu chuẩn', 'スタンダード', 'มาตรฐาน', '标准版', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_PLAN_PROFESSIONAL', '프로페셔널', 'Professional', 'Chuyên nghiệp', 'プロフェッショナル', 'มืออาชีพ', '专业版', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_TERM_ANNUAL', '연간', 'Annual', 'Hàng năm', '年間', 'รายปี', '年度', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_TERM_MONTHLY', '월간', 'Monthly', 'Hàng tháng', '月間', 'รายเดือน', '月度', 'AMnote Web - Thông tin License', NOW(), NULL),
  ('LICENSE_TERM_PERPETUAL', '영구', 'Perpetual', 'Vĩnh viễn', '永続', 'ถาวร', '永久', 'AMnote Web - Thông tin License', NOW(), NULL)
ON DUPLICATE KEY UPDATE
  `KOR` = VALUES(`KOR`),
  `ENG` = VALUES(`ENG`),
  `VIET` = VALUES(`VIET`),
  `JPN` = VALUES(`JPN`),
  `THA` = VALUES(`THA`),
  `CHN` = VALUES(`CHN`),
  `COMMENT` = VALUES(`COMMENT`),
  `LAST_MIDOFY_DATE` = NOW();

-- Newly inserted or updated messages may remain cached until the language cache is refreshed.
