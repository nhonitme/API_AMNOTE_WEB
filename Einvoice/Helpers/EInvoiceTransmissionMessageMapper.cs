using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Einvoice.Helpers
{
    public static class EInvoiceTransmissionMessageMapper
    {
        public static EInvoiceTransmissionMessageDto FromReceive(EInvoiceMessageReceiveInfo message, long? invoiceId = null)
        {
            return new EInvoiceTransmissionMessageDto
            {
                MESSAGE_KEY = BuildReceiveKey(message.RECEIVE_ID),
                MESSAGE_KIND = EInvoiceTransmissionMessageKinds.Receive,
                RECEIVE_ID = message.RECEIVE_ID,
                INVOICE_ID = invoiceId,
                COMPANY_CD = message.COMPANY_CD,
                PBAN = message.PBAN,
                MNGUI = message.MNGUI,
                MNNHAN = message.MNNHAN,
                MLTDIEP = message.MLTDIEP,
                MLTDIEP_NAME = message.MLTDIEP_NAME,
                MTDIEP = message.MTDIEP,
                MTDTCHIEU = message.MTDTCHIEU,
                MTRA_CUU = message.MTRA_CUU,
                MST = message.MST,
                SLUONG = message.SLUONG,
                ERROR_MESSAGE = message.ERROR_MESSAGE,
                CREATE_BY = message.CREATE_BY,
                CREATE_AT = message.CREATE_AT,
            };
        }

        public static string BuildReceiveKey(long receiveId) => $"RECEIVE:{receiveId}";
    }
}
