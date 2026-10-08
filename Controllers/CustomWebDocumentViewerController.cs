using DevExpress.AspNetCore.Reporting.WebDocumentViewer;
using DevExpress.AspNetCore.Reporting.WebDocumentViewer.Native.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [Authorize]
    [Route("DXXRDV")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class CustomWebDocumentViewerController : WebDocumentViewerController
    {
        public CustomWebDocumentViewerController(
            IWebDocumentViewerMvcControllerService controllerService)
            : base(controllerService)
        {
        }
    }
}
