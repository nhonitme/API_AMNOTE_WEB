using API_AMNOTE_WEB.Interfaces;

namespace API_AMNOTE_WEB.Controllers
{
    public class OtherVoucherArController : ChitInfoBaseController
    {
        protected override string PermissionMenuCode => "GL_VOUCHER_OTHER";
        protected override string InputType => "AR";
        protected override string SupportedType => "OT";
        protected override string HeaderCodePrefix => "OT";
        protected override string DetailCodePrefix => "OTD";
        protected override string DocumentDisplayName => "Other voucher";
        protected override bool SupportsExcelIntegration => true;
        protected override string? ExcelModuleCd => "OtherVoucherAr";

        protected override IReadOnlyList<string> GetListQueryInputTypes()
            => new[] { InputType, "LOCK" };

        public OtherVoucherArController(IChitInfoRepository repository, IChitInfoWriteService writeService, ILogger<OtherVoucherArController> logger)
            : base(repository, writeService, logger)
        {
        }
    }
}
