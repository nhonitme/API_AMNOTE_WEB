using API_AMNOTE_WEB.Interfaces;

namespace API_AMNOTE_WEB.Controllers
{
    public class PurchaseVoucherApController : ChitInfoBaseController
    {
        protected override string PermissionMenuCode => "AP_PURCHASE_GOODS";
        protected override string InputType => "AP";
        protected override string SupportedType => "PO";
        protected override string HeaderCodePrefix => "PV";
        protected override string DetailCodePrefix => "PVD";
        protected override string DocumentDisplayName => "Purchase voucher";
        protected override IReadOnlyCollection<string> SupportedTypeAliases => new[] { "PURCHASE_VOUCHER" };
        protected override bool SupportsExcelIntegration => true;
        protected override string? ExcelModuleCd => "PurchaseVoucherAp";

        public PurchaseVoucherApController(IChitInfoRepository repository, IChitInfoWriteService writeService, ILogger<PurchaseVoucherApController> logger)
            : base(repository, writeService, logger)
        {
        }
    }
}
