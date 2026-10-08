using API_AMNOTE_WEB.Interfaces;

namespace API_AMNOTE_WEB.Controllers
{
    public class PurchaseServiceVoucherApController : ChitInfoBaseController
    {
        protected override string PermissionMenuCode => "AP_PURCHASE_SERVICE";
        protected override string InputType => "AP";
        protected override string SupportedType => "PS";
        protected override string HeaderCodePrefix => "PS";
        protected override string DetailCodePrefix => "PSD";
        protected override string DocumentDisplayName => "Purchase service voucher";
        protected override bool SupportsExcelIntegration => true;
        protected override string? ExcelModuleCd => "PurchaseServiceVoucherAp";

        public PurchaseServiceVoucherApController(IChitInfoRepository repository, IChitInfoWriteService writeService, ILogger<PurchaseServiceVoucherApController> logger)
            : base(repository, writeService, logger)
        {
        }
    }

}
