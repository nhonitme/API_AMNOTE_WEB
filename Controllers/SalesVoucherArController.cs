using API_AMNOTE_WEB.Interfaces;

namespace API_AMNOTE_WEB.Controllers
{
    public class SalesVoucherArController : ChitInfoBaseController
    {
        protected override string PermissionMenuCode => "GL_VOUCHER_SALE";
        protected override string InputType => "AR";
        protected override string SupportedType => "SO";
        protected override string HeaderCodePrefix => "SO";
        protected override string DetailCodePrefix => "SOD";
        protected override string DocumentDisplayName => "Sales voucher";
        protected override bool SupportsExcelIntegration => true;
        protected override string? ExcelModuleCd => "SalesVoucherAr";

        public SalesVoucherArController(IChitInfoRepository repository, IChitInfoWriteService writeService, ILogger<SalesVoucherArController> logger)
            : base(repository, writeService, logger)
        {
        }
    }
}
