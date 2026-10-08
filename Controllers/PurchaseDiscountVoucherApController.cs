using API_AMNOTE_WEB.Interfaces;

namespace API_AMNOTE_WEB.Controllers
{
    public class PurchaseDiscountVoucherApController : ChitInfoBaseController
    {
        protected override string PermissionMenuCode => "AP_PURCHASE_DISCOUNT";
        protected override string InputType => "AP";
        protected override string SupportedType => "PD";
        protected override string HeaderCodePrefix => "PD";
        protected override string DetailCodePrefix => "PDD";
        protected override string DocumentDisplayName => "Purchase discount voucher";
        protected override bool SupportsExcelIntegration => true;
        protected override string? ExcelModuleCd => "PurchaseDiscountVoucherAp";
        protected override IReadOnlyCollection<string> SupportedTypeAliases => new[] { "PURCHASE_DISCOUNT", "PURCHASE_DISCOUNT_VOUCHER" };

        public PurchaseDiscountVoucherApController(IChitInfoRepository repository, IChitInfoWriteService writeService, ILogger<PurchaseDiscountVoucherApController> logger)
            : base(repository, writeService, logger)
        {
        }
    }
}
