using API_AMNOTE_WEB.Interfaces;

namespace API_AMNOTE_WEB.Controllers
{
    public class DebitNoteApController : ChitInfoBaseController
    {
        protected override string PermissionMenuCode => "GL_VOUCHER_DEBIT_NOTE";
        protected override string InputType => "AP";
        protected override string SupportedType => "DN";
        protected override string HeaderCodePrefix => "DN";
        protected override string DetailCodePrefix => "DND";
        protected override string DocumentDisplayName => "Debit note";
        protected override bool SupportsExcelIntegration => true;
        protected override string? ExcelModuleCd => "DebitNoteAp";

        public DebitNoteApController(IChitInfoRepository repository, IChitInfoWriteService writeService, ILogger<DebitNoteApController> logger)
            : base(repository, writeService, logger)
        {
        }
    }
}
