using API_AMNOTE_WEB.Interfaces;

namespace API_AMNOTE_WEB.Controllers
{
    public class PurchaseReturnVoucherApController : ChitInfoBaseController
    {
        protected override string PermissionMenuCode => "AP_RETURN_GOODS";
        protected override string InputType => "AP";
        protected override string SupportedType => "PR";
        protected override string HeaderCodePrefix => "PR";
        protected override string DetailCodePrefix => "PRD";
        protected override string DocumentDisplayName => "Purchase return voucher";
        protected override bool SupportsExcelIntegration => true;
        protected override string? ExcelModuleCd => "PurchaseReturnVoucherAp";
        protected override IReadOnlyCollection<string> SupportedTypeAliases => new[] { "PURCHASE_RETURN", "PURCHASE_RETURN_VOUCHER" };

        public PurchaseReturnVoucherApController(IChitInfoRepository repository, IChitInfoWriteService writeService, ILogger<PurchaseReturnVoucherApController> logger)
            : base(repository, writeService, logger)
        {
        }
    }
}
