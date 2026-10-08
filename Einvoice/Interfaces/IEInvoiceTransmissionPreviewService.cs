namespace API_AMNOTE_WEB.Interfaces

{

    public interface IEInvoiceTransmissionPreviewService

    {

        Task<string> BuildReceivePreviewHtmlAsync(string companyCd, long receiveId, long? invoiceId = null);

    }

}


