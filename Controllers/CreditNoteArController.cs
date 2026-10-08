using API_AMNOTE_WEB.Interfaces;

namespace API_AMNOTE_WEB.Controllers
{
    public class CreditNoteArController : ChitInfoBaseController
    {
        protected override string PermissionMenuCode => "GL_VOUCHER_CREDIT_NOTE";
        protected override string InputType => "AR";
        protected override string SupportedType => "CN";
        protected override string HeaderCodePrefix => "CN";
        protected override string DetailCodePrefix => "CND";
        protected override string DocumentDisplayName => "Credit note";
        protected override bool SupportsExcelIntegration => true;
        protected override string? ExcelModuleCd => "CreditNoteAr";

        public CreditNoteArController(IChitInfoRepository repository, IChitInfoWriteService writeService, ILogger<CreditNoteArController> logger)
            : base(repository, writeService, logger)
        {
        }
    }
}
