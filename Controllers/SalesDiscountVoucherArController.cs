using API_AMNOTE_WEB.Interfaces;

namespace API_AMNOTE_WEB.Controllers
{
    public class SalesDiscountVoucherArController : ChitInfoBaseController
    {
        protected override string PermissionMenuCode => "AR_SALE_DISCOUNT";
        protected override string InputType => "AR";
        protected override string SupportedType => "SD";
        protected override string HeaderCodePrefix => "SD";
        protected override string DetailCodePrefix => "SDD";
        protected override string DocumentDisplayName => "Sales discount voucher";
        protected override bool SupportsExcelIntegration => true;
        protected override string? ExcelModuleCd => "SalesDiscountVoucherAr";
        protected override IReadOnlyCollection<string> SupportedTypeAliases => new[] { "SALES_DISCOUNT", "SALES_DISCOUNT_VOUCHER" };

        public SalesDiscountVoucherArController(IChitInfoRepository repository, IChitInfoWriteService writeService, ILogger<SalesDiscountVoucherArController> logger)
            : base(repository, writeService, logger)
        {
        }
    }
}
