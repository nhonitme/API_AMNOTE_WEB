using DevExpress.XtraReports.Web.WebDocumentViewer;

namespace API_AMNOTE_WEB.Reporting
{
    public class CustomWebDocumentViewerExceptionHandler : WebDocumentViewerExceptionHandler
    {
        public override string GetExceptionMessage(Exception ex)
        {
            return ResolveExceptionMessage(ex);
        }

        public override string GetUnknownExceptionMessage(Exception ex)
        {
            return ResolveExceptionMessage(ex);
        }

        private static string ResolveExceptionMessage(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                var message = current.Message?.Trim();
                if (!string.IsNullOrWhiteSpace(message))
                {
                    return message;
                }
            }

            return ex.GetType().Name;
        }
    }
}
