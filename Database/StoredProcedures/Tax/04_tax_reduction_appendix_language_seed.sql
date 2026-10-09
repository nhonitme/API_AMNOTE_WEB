-- Deploy to AM_WEB_MANAGER. Run once after adding getLanguage captions
-- to Reporting/TaxReductionAppendixReport.cs.
-- INSERT IGNORE preserves all existing customer-configured translations.
-- PDF is rendered in the language selected on ReportViewer (company.ReportLanguage).
SET NAMES utf8mb4;

INSERT IGNORE INTO t_message_info (`KEY`, KOR, ENG, VIET, JPN, THA, CHN, `COMMENT`)
VALUES
('Tax_reduction_appendix_title', '부가가치세 감면 부속명세서', 'VAT reduction appendix', 'PHỤ LỤC GIẢM THUẾ GIÁ TRỊ GIA TĂNG', '付加価値税減税付表', 'ภาคผนวกการลดภาษีมูลค่าเพิ่ม', '增值税减税附表', 'VAT reduction appendix PDF'),
('Tax_reduction_appendix_period', '신고 기간:', 'Reporting period:', 'Kỳ báo cáo:', '報告期間：', 'รอบระยะเวลารายงาน:', '申报期间：', 'VAT reduction appendix PDF'),
('Tax_reduction_appendix_taxpayer', '납세자명:', 'Taxpayer name:', 'Tên người nộp thuế:', '納税者名：', 'ชื่อผู้เสียภาษี:', '纳税人名称：', 'VAT reduction appendix PDF'),
('Tax_reduction_appendix_taxcode', '납세자번호:', 'Tax identification number:', 'Mã số thuế:', '納税者番号：', 'เลขประจำตัวผู้เสียภาษี:', '纳税人识别号：', 'VAT reduction appendix PDF'),
('Tax_reduction_appendix_currency_unit', '통화 단위: 베트남 동', 'Currency unit: Vietnamese dong', 'Đơn vị tiền tệ: Việt Nam đồng', '通貨単位：ベトナムドン', 'หน่วยเงินตรา: ดองเวียดนาม', '货币单位：越南盾', 'VAT reduction appendix PDF'),
('Tax_reduction_appendix_group1', 'I. 해당 기간 8% 부가가치세율 적용 대상 매입 재화 및 용역', 'I. Goods and services purchased during the period subject to 8% VAT', 'I. Hàng hóa, dịch vụ mua vào trong kỳ được áp dụng mức thuế suất thuế giá trị gia tăng 8%', 'I. 当期に8%の付加価値税率が適用される仕入商品およびサービス', 'I. สินค้าและบริการที่ซื้อในงวดซึ่งใช้อัตราภาษีมูลค่าเพิ่ม 8%', '一、本期适用8%增值税税率的购进货物及服务', 'VAT reduction appendix PDF'),
('Tax_reduction_appendix_group2', 'II. 해당 기간 매출 재화 및 용역', 'II. Goods and services sold during the period', 'II. Hàng hóa, dịch vụ bán ra trong kỳ', 'II. 当期に販売した商品およびサービス', 'II. สินค้าและบริการที่ขายในงวด', '二、本期销售的货物及服务', 'VAT reduction appendix PDF'),
('Tax_reduction_appendix_stt', '번호', 'No.', 'STT', '番号', 'ลำดับ', '序号', 'VAT reduction appendix PDF'),
('Tax_reduction_appendix_product', '재화 및 용역명', 'Description of goods and services', 'Tên hàng hóa, dịch vụ', '商品・サービス名', 'ชื่อสินค้าและบริการ', '货物、服务名称', 'VAT reduction appendix PDF'),
('Tax_reduction_appendix_purchase_value', '부가가치세 제외 매입가액', 'Purchase value before VAT', 'Giá trị mua vào chưa có thuế GTGT', '付加価値税抜き仕入金額', 'มูลค่าซื้อก่อนภาษีมูลค่าเพิ่ม', '不含增值税的购进金额', 'VAT reduction appendix PDF'),
('Tax_reduction_appendix_purchase_vat', '공제 가능한 매입 부가가치세', 'Deductible input VAT', 'Thuế GTGT mua vào được khấu trừ', '控除可能な仕入付加価値税額', 'ภาษีซื้อที่หักได้', '可抵扣进项增值税', 'VAT reduction appendix PDF'),
('Tax_reduction_appendix_sale_value', '부가가치세 제외 금액', 'Value before VAT', 'Giá trị chưa có thuế GTGT', '付加価値税抜き金額', 'มูลค่าก่อนภาษีมูลค่าเพิ่ม', '不含增值税的金额', 'VAT reduction appendix PDF'),
('Tax_reduction_appendix_standard_rate', '법정 부가가치세율', 'Statutory VAT rate', 'Thuế suất theo quy định', '法定付加価値税率', 'อัตราภาษีตามกฎหมาย', '法定增值税税率', 'VAT reduction appendix PDF'),
('Tax_reduction_appendix_reduced_rate', '감면 후 부가가치세율', 'VAT rate after reduction', 'Thuế suất sau giảm', '減税後の付加価値税率', 'อัตราภาษีหลังลด', '减免后增值税税率', 'VAT reduction appendix PDF'),
('Tax_reduction_appendix_vat_reduced', '감면 부가가치세액', 'VAT reduction amount', 'Thuế GTGT được giảm', '減税された付加価値税額', 'จำนวนภาษีมูลค่าเพิ่มที่ลดลง', '增值税减免金额', 'VAT reduction appendix PDF'),
('Tax_reduction_appendix_difference', 'III. 해당 기간 매출 및 매입 부가가치세 차액:', 'III. Difference between output and input VAT during the period:', 'III. Chênh lệch thuế GTGT của hàng hóa, dịch vụ bán ra và mua vào trong kỳ:', 'III. 当期の売上および仕入に係る付加価値税の差額：', 'III. ผลต่างภาษีขายและภาษีซื้อในงวด:', '三、本期销项与进项增值税差额：', 'VAT reduction appendix PDF'),
('Tax_reduction_appendix_currency_suffix', '동', 'VND', 'đồng', 'ドン', 'ดอง', '越南盾', 'VAT reduction appendix PDF'),
('Tax_reduction_appendix_page', '페이지 {0}/{1}', 'Page {0}/{1}', 'Trang {0}/{1}', 'ページ {0}/{1}', 'หน้า {0}/{1}', '第 {0}/{1} 页', 'VAT reduction appendix PDF'),
('Tax_reduction_appendix_total', '합계', 'Total', 'Tổng cộng', '合計', 'รวม', '合计', 'VAT reduction appendix PDF');

-- Verify translations for the PDF captions:
SELECT `KEY`, VIET, ENG, KOR, CHN, JPN, THA
FROM t_message_info
WHERE `KEY` LIKE 'Tax_reduction_appendix_%'
ORDER BY `KEY`;
