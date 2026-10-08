using API_AMNOTE_WEB.Interfaces;

namespace API_AMNOTE_WEB.Controllers
{
    public class SalesReturnVoucherArController : ChitInfoBaseController
    {
        protected override string PermissionMenuCode => "AR_SALE_RETURN";
        protected override string InputType => "AR";
        protected override string SupportedType => "SR";
        protected override string HeaderCodePrefix => "SR";
        protected override string DetailCodePrefix => "SRD";
        protected override string DocumentDisplayName => "Sales return voucher";
        protected override bool SupportsExcelIntegration => true;
        protected override string? ExcelModuleCd => "SalesReturnVoucherAr";
        protected override IReadOnlyCollection<string> SupportedTypeAliases => new[] { "SALES_RETURN", "SALES_RETURN_VOUCHER" };

        public SalesReturnVoucherArController(IChitInfoRepository repository, IChitInfoWriteService writeService, ILogger<SalesReturnVoucherArController> logger)
            : base(repository, writeService, logger)
        {
        }
    }
}
