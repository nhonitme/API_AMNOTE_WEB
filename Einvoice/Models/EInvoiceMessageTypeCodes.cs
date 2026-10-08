namespace API_AMNOTE_WEB.Models
{
    public static class EInvoiceMessageTypeCodes
    {
        public const string SendDeclaration = "100";
        public const string SendDeclarationDelegation = "101";
        public const string DeclarationReceiveNotice = "102";
        public const string DeclarationAcceptNotice = "103";
        public const string DeclarationDelegationAcceptNotice = "104";
        public const string ExpiredTaxCodeInvoiceNotice = "105";
        public const string SendPerOccurrenceInvoiceRequest = "106";
        public const string HighRiskTaxpayerNotice = "107";
        public const string StopUsingInvoiceNotice = "108";
        public const string SendElectronicDocumentDeclaration = "109";
        public const string ElectronicDocumentDeclarationReceiveNotice = "110";
        public const string ElectronicDocumentDeclarationAcceptNotice = "111";
        public const string TaxpayerCancelDeclarationNotice = "112";
        public const string ExplanationSupplementNotice = "113";
        public const string ContinueUsingInvoiceNotice = "114";

        public const string SendPortalAccountDeclaration = "150";

        public const string SendInvoice = "200";
        public const string SendInvoicePerOccurrence = "201";
        public const string InvoiceTaxCodeResult = "202";
        public const string SendInvoiceWithoutCode = "203";
        public const string DataCheckResultNotice01 = "204";
        public const string PerOccurrenceInvoiceRequestResponse = "205";
        public const string SendCashRegisterInvoice = "206";
        public const string SendCashRegisterInvoiceWithoutCode = "216";
        public const string SendCasinoSummaryInvoice = "207";
        public const string SendMultiInvoiceAdjustment = "208";
        public const string SendReceiptIntegratedInvoice = "209";
        public const string SendMultiInvoiceWithoutCode = "210";
        public const string SendPersonalIncomeTaxCertificate = "211";
        public const string SendReceiptUsageReport = "212";
        public const string ElectronicDocumentDataCheckNotice01 = "213";

        public const string InvoiceErrorNotice = "300";
        public const string InvoiceErrorProcessResult = "301";
        public const string InvoiceReviewNotice = "302";
        public const string CashRegisterInvoiceErrorNotice = "303";
        public const string ElectronicDocumentErrorNotice = "304";

        public const string SendInvoiceSummary = "400";
        public const string SendInvoiceSummaryWithAdjustmentList = "401";

        public const string SendAuthorizedTaxCodeInvoice = "500";
        public const string SendIneligibleTaxCodeInvoice = "503";
        public const string SendAuthorizedDataCheckNotice01 = "504";
        public const string TaxCodeChangedNotice = "505";
        public const string StopContinueInvoiceDecisionNotice = "506";
        public const string RegistrationInfoNotice = "507";

        public const string RequestSignAuthorizedTaxCodeInvoice = "600";
        public const string TaxOfficeSignedAuthorizedInvoice = "601";
        public const string RequestSignAuthorizedNotice = "602";
        public const string TaxOfficeSignedNotice = "603";

        public const string TechnicalResponse = "999";
        public const string DailyReconciliationReport = "901";
        public const string AuthorizedReconciliationReport = "902";
        public const string InvalidFormatResponse = "-1";
        public const string InvalidSigningDataResponse = "-2";

        private static readonly IReadOnlyDictionary<string, EInvoiceMessageTypeInfo> Catalog =
            BuildCatalog();

        public static IReadOnlyCollection<EInvoiceMessageTypeInfo> All => Catalog.Values.ToList();

        public static bool TryGet(string? code, out EInvoiceMessageTypeInfo info)
        {
            var normalized = Normalize(code);
            if (normalized.Length == 0)
            {
                info = default;
                return false;
            }

            return Catalog.TryGetValue(normalized, out info!);
        }

        public static string GetName(string? code)
        {
            return TryGet(code, out var info) ? info.Name : Normalize(code);
        }

        public static string GetGroup(string? code)
        {
            return TryGet(code, out var info) ? info.Group : string.Empty;
        }

        public static bool IsSendDeclaration(string? code)
            => Normalize(code) is SendDeclaration or SendDeclarationDelegation or SendElectronicDocumentDeclaration;

        public static bool IsSendInvoice(string? code)
            => Normalize(code) is SendInvoice or SendInvoicePerOccurrence;

        public static bool IsInvoiceTaxCodeResult(string? code)
            => string.Equals(Normalize(code), InvoiceTaxCodeResult, StringComparison.Ordinal);

        public static bool IsDataCheckResultNotice01(string? code)
            => string.Equals(Normalize(code), DataCheckResultNotice01, StringComparison.Ordinal);

        public static bool IsInvoiceErrorNotice(string? code)
            => string.Equals(Normalize(code), InvoiceErrorNotice, StringComparison.Ordinal);

        public static bool IsInvoiceErrorProcessResult(string? code)
            => string.Equals(Normalize(code), InvoiceErrorProcessResult, StringComparison.Ordinal);

        public static bool IsDeclarationReceiveNotice(string? code)
            => Normalize(code) is DeclarationReceiveNotice or ElectronicDocumentDeclarationReceiveNotice;

        public static bool IsDeclarationAcceptNotice(string? code)
            => Normalize(code) is DeclarationAcceptNotice or ElectronicDocumentDeclarationAcceptNotice;

        public static bool ShouldUpdateInvoiceXmlFromReceive(string? code)
            => IsInvoiceTaxCodeResult(code);

        public static bool ShouldPreviewWithSellerXsl(string? code)
            => string.Equals(Normalize(code), SendInvoice, StringComparison.Ordinal)
               || string.Equals(Normalize(code), InvoiceTaxCodeResult, StringComparison.Ordinal);

        public static bool ShouldUpdateInvoiceErrorFromReceive(string? code)
            => IsDataCheckResultNotice01(code);

        public static bool ShouldUpdateDeclarationXmlFromReceive(string? code)
            => IsDeclarationAcceptNotice(code);

        public static bool ShouldUpdateDeclarationErrorFromReceive(string? code)
            => IsDeclarationReceiveNotice(code) || IsDeclarationAcceptNotice(code);

        public static bool ShouldUpdateErrorNoticeFromReceive(string? code)
            => IsInvoiceErrorProcessResult(code);

        public static bool IsReceiveWorkQueueTerminalForInvoice(string? code)
            => IsInvoiceTaxCodeResult(code) || IsDataCheckResultNotice01(code);

        public static bool IsReceiveWorkQueueTerminalForErrorNotice(string? code)
            => IsInvoiceErrorProcessResult(code);

        public static bool IsReceiveWorkQueueTerminalForDeclaration(string? code)
            => IsDeclarationReceiveNotice(code) || IsDeclarationAcceptNotice(code);

        public static bool IsReceiveWorkQueueTerminal(string? targetType, string? code)
            => EInvoiceMessageTargetTypes.IsDeclaration(targetType)
                ? IsReceiveWorkQueueTerminalForDeclaration(code)
                : EInvoiceMessageTargetTypes.IsErrorNotice(targetType)
                    ? IsReceiveWorkQueueTerminalForErrorNotice(code)
                    : IsReceiveWorkQueueTerminalForInvoice(code);

        public static bool IsReceiveWorkQueueTerminal(string? code)
            => IsReceiveWorkQueueTerminalForInvoice(code);

        private static string Normalize(string? code) => (code ?? string.Empty).Trim();

        private static IReadOnlyDictionary<string, EInvoiceMessageTypeInfo> BuildCatalog()
        {
            EInvoiceMessageTypeInfo Item(string code, string name, string group)
                => new(code, name, group);

            var items = new[]
            {
                Item(SendDeclaration, "Thông điệp gửi tờ khai đăng ký/thay đổi thông tin sử dụng hóa đơn điện tử", EInvoiceMessageTypeGroups.Registration),
                Item(SendDeclarationDelegation, "Thông điệp gửi tờ khai đăng ký thay đổi thông tin đăng ký sử dụng HĐĐT khi ủy nhiệm/nhận ủy nhiệm lập hóa đơn", EInvoiceMessageTypeGroups.Registration),
                Item(DeclarationReceiveNotice, "Thông điệp thông báo về việc tiếp nhận/không tiếp nhận tờ khai đăng ký/thay đổi thông tin sử dụng HĐĐT", EInvoiceMessageTypeGroups.Registration),
                Item(DeclarationAcceptNotice, "Thông điệp thông báo về việc chấp nhận/không chấp nhận đăng ký/thay đổi thông tin sử dụng hóa đơn điện tử", EInvoiceMessageTypeGroups.Registration),
                Item(DeclarationDelegationAcceptNotice, "Thông điệp thông báo về việc chấp nhận/không chấp nhận đăng ký thay đổi thông tin đăng ký sử dụng HĐĐT khi ủy nhiệm/nhận ủy nhiệm lập hoá đơn", EInvoiceMessageTypeGroups.Registration),
                Item(ExpiredTaxCodeInvoiceNotice, "Thông điệp thông báo về việc hết thời gian sử dụng hóa đơn điện tử có mã qua cổng thông tin điện tử Cục Thuế /qua ủy thác tổ chức cung cấp dịch vụ về hóa đơn điện tử; không thuộc trường hợp sử dụng hóa đơn điện tử không có mã", EInvoiceMessageTypeGroups.Registration),
                Item(SendPerOccurrenceInvoiceRequest, "Thông điệp gửi Đơn đề nghị cấp hóa đơn điện tử có mã của CQT theo từng lần phát sinh", EInvoiceMessageTypeGroups.Registration),
                Item(HighRiskTaxpayerNotice, "Thông điệp thông báo về việc yêu cầu NNT thuộc diện rủi ro cao giải trình hoặc bổ sung tài liệu", EInvoiceMessageTypeGroups.Registration),
                Item(StopUsingInvoiceNotice, "Thông điệp thông báo ngừng sử dụng hóa đơn điện tử", EInvoiceMessageTypeGroups.Registration),
                Item(SendElectronicDocumentDeclaration, "Thông điệp gửi tờ khai đăng ký/thay đổi thông tin sử dụng chứng từ điện tử", EInvoiceMessageTypeGroups.Registration),
                Item(ElectronicDocumentDeclarationReceiveNotice, "Thông điệp thông báo về việc tiếp nhận/không tiếp nhận tờ khai đăng ký/thay đổi thông tin sử dụng CTĐT", EInvoiceMessageTypeGroups.Registration),
                Item(ElectronicDocumentDeclarationAcceptNotice, "Thông điệp thông báo về việc chấp nhận/không chấp nhận đăng ký/thay đổi thông tin sử dụng chứng từ điện tử", EInvoiceMessageTypeGroups.Registration),
                Item(TaxpayerCancelDeclarationNotice, "Thông điệp thông báo về việc NNT hủy tờ khai/thông báo/đề nghị", EInvoiceMessageTypeGroups.Registration),
                Item(ExplanationSupplementNotice, "Thông điệp gửi thông báo về việc giải trình, bổ sung thông tin, tài liệu và chuyển hình thức sử dụng HĐĐT có mã của cơ quan thuế theo từng lần phát sinh", EInvoiceMessageTypeGroups.Registration),
                Item(ContinueUsingInvoiceNotice, "Thông điệp gửi thông báo về việc tiếp tục sử dụng hóa đơn điện tử", EInvoiceMessageTypeGroups.Registration),

                Item(SendPortalAccountDeclaration, "Thông điệp gửi tờ khai đăng ký mới/thay đổi thông tin/Chấm dứt sử dụng tài khoản truy cập Cổng thông tin hóa đơn điện tử, chứng từ điện tử", EInvoiceMessageTypeGroups.PortalAccount),

                Item(SendInvoice, "Thông điệp gửi hóa đơn điện tử tới cơ quan thuế để cấp mã", EInvoiceMessageTypeGroups.InvoiceTransmission),
                Item(SendInvoicePerOccurrence, "Thông điệp gửi hóa đơn điện tử tới cơ quan thuế để cấp mã theo từng lần phát sinh", EInvoiceMessageTypeGroups.InvoiceTransmission),
                Item(InvoiceTaxCodeResult, "Thông điệp thông báo kết quả cấp mã hóa đơn điện tử của cơ quan thuế", EInvoiceMessageTypeGroups.InvoiceTransmission),
                Item(SendInvoiceWithoutCode, "Thông điệp chuyển dữ liệu hóa đơn điện tử không mã đến cơ quan thuế", EInvoiceMessageTypeGroups.InvoiceTransmission),
                Item(DataCheckResultNotice01, "Thông điệp thông báo mẫu số 01/TB-KTDL về việc kết quả kiểm tra dữ liệu hóa đơn điện tử", EInvoiceMessageTypeGroups.InvoiceTransmission),
                Item(PerOccurrenceInvoiceRequestResponse, "Thông điệp phản hồi về hồ sơ đề nghị cấp hóa đơn điện tử có mã của cơ quan thuế theo từng lần phát sinh", EInvoiceMessageTypeGroups.InvoiceTransmission),
                Item(SendCashRegisterInvoice, "Thông điệp gửi hóa đơn khởi tạo từ máy tính tiền đã cấp mã tới cơ quan thuế", EInvoiceMessageTypeGroups.InvoiceTransmission),
                Item(SendCashRegisterInvoiceWithoutCode, "Thông điệp gửi hóa đơn khởi tạo từ máy tính tiền không mã tới cơ quan thuế", EInvoiceMessageTypeGroups.InvoiceTransmission),
                Item(SendCasinoSummaryInvoice, "Thông điệp gửi hóa đơn kèm theo phiếu tổng hợp doanh thu Casino tới Cơ quan thuế", EInvoiceMessageTypeGroups.InvoiceTransmission),
                Item(SendMultiInvoiceAdjustment, "Thông điệp gửi hóa đơn điều chỉnh/thay thế nhiều hóa đơn kèm bảng kê tới cơ quan thuế để cấp mã", EInvoiceMessageTypeGroups.InvoiceTransmission),
                Item(SendReceiptIntegratedInvoice, "Thông điệp gửi hóa đơn tích hợp biên lai tới cơ quan thuế (hóa đơn không mã)", EInvoiceMessageTypeGroups.InvoiceTransmission),
                Item(SendMultiInvoiceWithoutCode, "Thông điệp chuyển dữ liệu hóa đơn không mã điều chỉnh/thay thế nhiều hóa đơn kèm bảng kê tới cơ quan thuế", EInvoiceMessageTypeGroups.InvoiceTransmission),
                Item(SendPersonalIncomeTaxCertificate, "Thông điệp chuyển chứng từ khấu trừ thuế TNCN", EInvoiceMessageTypeGroups.InvoiceTransmission),
                Item(SendReceiptUsageReport, "Thông điệp chuyển báo cáo tình hình sử dụng biên lai", EInvoiceMessageTypeGroups.InvoiceTransmission),
                Item(ElectronicDocumentDataCheckNotice01, "Thông điệp thông báo Mẫu số 01/TB-KTDL về việc kết quả kiểm tra dữ liệu chứng từ điện tử", EInvoiceMessageTypeGroups.InvoiceTransmission),

                Item(InvoiceErrorNotice, "Thông điệp thông báo về hóa đơn điện tử đã lập có sai sót", EInvoiceMessageTypeGroups.InvoiceError),
                Item(InvoiceErrorProcessResult, "Thông điệp gửi thông báo về việc tiếp nhận và kết quả xử lý về việc hóa đơn điện tử/chứng từ điện tử đã lập có sai sót", EInvoiceMessageTypeGroups.InvoiceError),
                Item(InvoiceReviewNotice, "Thông điệp thông báo về hóa đơn điện tử cần rà soát", EInvoiceMessageTypeGroups.InvoiceError),
                Item(CashRegisterInvoiceErrorNotice, "Thông điệp thông báo về hóa đơn điện tử khởi tạo từ máy tính tiền đã lập có sai sót", EInvoiceMessageTypeGroups.InvoiceError),
                Item(ElectronicDocumentErrorNotice, "Thông điệp thông báo về chứng từ điện tử đã lập sai", EInvoiceMessageTypeGroups.InvoiceError),

                Item(SendInvoiceSummary, "Thông điệp chuyển bảng tổng hợp dữ liệu hóa đơn điện tử đến cơ quan thuế", EInvoiceMessageTypeGroups.InvoiceSummary),
                Item(SendInvoiceSummaryWithAdjustmentList, "Thông điệp chuyển bảng tổng hợp dữ liệu hóa đơn điện tử kèm bảng kê điều chỉnh/thay thế đến cơ quan thuế", EInvoiceMessageTypeGroups.InvoiceSummary),

                Item(SendAuthorizedTaxCodeInvoice, "Thông điệp chuyển dữ liệu hóa đơn điện tử do TCTN uỷ quyền cấp mã đến cơ quan thuế", EInvoiceMessageTypeGroups.AuthorizedTransmission),
                Item(SendIneligibleTaxCodeInvoice, "Thông điệp chuyển dữ liệu hóa đơn điện tử không đủ điều kiện cấp mã đến cơ quan thuế", EInvoiceMessageTypeGroups.AuthorizedTransmission),
                Item(SendAuthorizedDataCheckNotice01, "Thông điệp chuyển dữ liệu gửi thông báo mẫu số 01/TB-KTDL về việc kết quả kiểm tra dữ liệu đã được TCUQ gửi cho NNT đến cơ quan thuế", EInvoiceMessageTypeGroups.AuthorizedTransmission),
                Item(TaxCodeChangedNotice, "Thông điệp cung cấp MST có thay đổi thông tin trong ngày", EInvoiceMessageTypeGroups.AuthorizedTransmission),
                Item(StopContinueInvoiceDecisionNotice, "Thông điệp cung cấp quyết định ngừng/tiếp tục sử dụng hóa đơn", EInvoiceMessageTypeGroups.AuthorizedTransmission),
                Item(RegistrationInfoNotice, "Thông điệp cung cấp thông tin đăng ký sử dụng hóa đơn điện tử", EInvoiceMessageTypeGroups.AuthorizedTransmission),

                Item(RequestSignAuthorizedTaxCodeInvoice, "Thông điệp gửi đề nghị ký số hóa đơn cấp mã thành công của các đơn vị được ủy quyền cấp mã", EInvoiceMessageTypeGroups.AuthorizedSigning),
                Item(TaxOfficeSignedAuthorizedInvoice, "Thông điệp Cục Thuế ký số hóa đơn đã được cấp mã thành công gửi Tổ chức ủy quyền cấp mã", EInvoiceMessageTypeGroups.AuthorizedSigning),
                Item(RequestSignAuthorizedNotice, "Thông điệp gửi đề nghị ký số lên thông báo của các đơn vị được ủy quyền cấp mã", EInvoiceMessageTypeGroups.AuthorizedSigning),
                Item(TaxOfficeSignedNotice, "Thông điệp Cục Thuế ký số Thông báo thành công gửi Tổ chức ủy quyền cấp mã", EInvoiceMessageTypeGroups.AuthorizedSigning),

                Item(TechnicalResponse, "Thông điệp phản hồi kỹ thuật", EInvoiceMessageTypeGroups.Other),
                Item(DailyReconciliationReport, "Thông điệp báo cáo đối soát hàng ngày giữa cơ quan thuế và tổ chức truyền nhận", EInvoiceMessageTypeGroups.Other),
                Item(AuthorizedReconciliationReport, "Thông điệp báo cáo đối soát dữ liệu giữa cơ quan thuế và TCTN trong trường hợp ủy quyền cấp mã", EInvoiceMessageTypeGroups.Other),
                Item(InvalidFormatResponse, "Thông điệp phản hồi sai định dạng", EInvoiceMessageTypeGroups.Other),
                Item(InvalidSigningDataResponse, "Thông điệp dữ liệu đề nghị ký số bị lỗi", EInvoiceMessageTypeGroups.Other),
            };

            return items.ToDictionary(x => x.Code, StringComparer.Ordinal);
        }
    }

    public static class EInvoiceMessageTypeGroups
    {
        public const string Registration = "REGISTRATION";
        public const string PortalAccount = "PORTAL_ACCOUNT";
        public const string InvoiceTransmission = "INVOICE_TRANSMISSION";
        public const string InvoiceError = "INVOICE_ERROR";
        public const string InvoiceSummary = "INVOICE_SUMMARY";
        public const string AuthorizedTransmission = "AUTHORIZED_TRANSMISSION";
        public const string AuthorizedSigning = "AUTHORIZED_SIGNING";
        public const string Other = "OTHER";
    }

    public readonly record struct EInvoiceMessageTypeInfo(string Code, string Name, string Group);
}
