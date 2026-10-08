using API_AMNOTE_WEB.Interfaces;

namespace API_AMNOTE_WEB.Controllers
{
    public class OffsetVoucherApController : ChitInfoBaseController
    {
        protected override string PermissionMenuCode => "GL_VOUCHER_OFFSET";
        protected override string InputType => "AP";
        protected override string SupportedType => "CO";
        protected override string HeaderCodePrefix => "CO";
        protected override string DetailCodePrefix => "COD";
        protected override string DocumentDisplayName => "Offset voucher";
        protected override bool SupportsExcelIntegration => true;
        protected override string? ExcelModuleCd => "OffsetVoucherAp";

        public OffsetVoucherApController(IChitInfoRepository repository, IChitInfoWriteService writeService, ILogger<OffsetVoucherApController> logger)
            : base(repository, writeService, logger)
        {
        }
    }
}
