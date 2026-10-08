using API_AMNOTE_WEB.Interfaces;

namespace API_AMNOTE_WEB.Controllers
{
    public class ReceiptVoucherArController : ChitInfoBaseController
    {
        protected override string PermissionMenuCode => "GL_VOUCHER_RECEIPT";
        protected override string InputType => "AR";
        protected override string SupportedType => "RC";
        protected override string HeaderCodePrefix => "RC";
        protected override string DetailCodePrefix => "RCD";
        protected override string DocumentDisplayName => "Receipt voucher";
        protected override bool SupportsExcelIntegration => true;
        protected override string? ExcelModuleCd => "ReceiptVoucherAr";

        public ReceiptVoucherArController(IChitInfoRepository repository, IChitInfoWriteService writeService, ILogger<ReceiptVoucherArController> logger)
            : base(repository, writeService, logger)
        {
        }
    }
}
