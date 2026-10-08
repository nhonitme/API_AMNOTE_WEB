namespace API_AMNOTE_WEB.Models
{
    public static class EInvoiceDeclarationCqtStatus
    {
        public const int NotSent = 0;
        public const int SentWaiting = 1;
        public const int Accepted = 2;
        public const int Rejected = 3;
        public const int SendError = 4;
    }
}
