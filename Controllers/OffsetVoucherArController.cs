using API_AMNOTE_WEB.Interfaces;

namespace API_AMNOTE_WEB.Controllers
{
    public class OffsetVoucherArController : ChitInfoBaseController
    {
        protected override string PermissionMenuCode => "GL_VOUCHER_OFFSET";
        protected override string InputType => "AR";
        protected override string SupportedType => "CO";
        protected override string HeaderCodePrefix => "CO";
        protected override string DetailCodePrefix => "COD";
        protected override string DocumentDisplayName => "Offset voucher";
        protected override bool SupportsExcelIntegration => true;
        protected override string? ExcelModuleCd => "OffsetVoucherAr";

        public OffsetVoucherArController(IChitInfoRepository repository, IChitInfoWriteService writeService, ILogger<OffsetVoucherArController> logger)
            : base(repository, writeService, logger)
        {
        }
    }
}
