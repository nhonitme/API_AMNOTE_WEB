using API_AMNOTE_WEB.Interfaces;

namespace API_AMNOTE_WEB.Controllers
{
    public class OtherVoucherApController : ChitInfoBaseController
    {
        protected override string PermissionMenuCode => "GL_VOUCHER_OTHER";
        protected override string InputType => "AP";
        protected override string SupportedType => "OT";
        protected override string HeaderCodePrefix => "OT";
        protected override string DetailCodePrefix => "OTD";
        protected override string DocumentDisplayName => "Other voucher";
        protected override bool SupportsExcelIntegration => true;
        protected override string? ExcelModuleCd => "OtherVoucherAp";

        protected override IReadOnlyList<string> GetListQueryInputTypes()
            => new[] { InputType, "LOCK" };

        public OtherVoucherApController(IChitInfoRepository repository, IChitInfoWriteService writeService, ILogger<OtherVoucherApController> logger)
            : base(repository, writeService, logger)
        {
        }
    }
}
