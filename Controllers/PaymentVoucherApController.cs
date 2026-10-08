using API_AMNOTE_WEB.Interfaces;

namespace API_AMNOTE_WEB.Controllers
{
    public class PaymentVoucherApController : ChitInfoBaseController
    {
        protected override string PermissionMenuCode => "GL_VOUCHER_PAYMENT";
        protected override string InputType => "AP";
        protected override string SupportedType => "PM";
        protected override string HeaderCodePrefix => "DN";
        protected override string DetailCodePrefix => "DND";
        protected override string DocumentDisplayName => "Payment voucher";
        protected override bool SupportsExcelIntegration => true;
        protected override string? ExcelModuleCd => "PaymentVoucherAp";

        public PaymentVoucherApController(IChitInfoRepository repository, IChitInfoWriteService writeService, ILogger<PaymentVoucherApController> logger)
            : base(repository, writeService, logger)
        {
        }
    }
}
