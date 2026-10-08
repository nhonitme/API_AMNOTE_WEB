using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IEInvoiceMessageRepository
    {
        Task<bool> ApplyMttReceiveAsync(string dbName, string companyCd, string userId, string mtdiep, string xml);
        Task DispatchMttOutboxAsync(string dbName, string companyCd, string? batchId = null);
        Task EnqueueOutboundAsync(
            string companyCd,
            string userId,
            string targetType,
            long? targetId,
            EInvoicePackagedMessage packagedMessage);

        Task<long> InsertReceiveAsync(string companyCd, string userId, EInvoiceMessageReceiveRequest request);

        Task<bool> ReceiveExistsAsync(string companyCd, string mtdiep);

        Task<bool> SendExistsAsync(string companyCd, string mtdiep, long? invoiceId = null);

        Task<bool> ReceiveTypeExistsAsync(string companyCd, string mtdtchieu, string mltdiep);

        Task<IReadOnlyList<EInvoiceMessageReceiveInfo>> GetReceiveMessagesByLookupCodeAsync(string companyCd, string mtraCuu);

        Task<EInvoiceMessageReceiveInfo?> GetReceiveMessageByIdAsync(string companyCd, long receiveId);

        Task<EInvoiceMessageSendInfo?> GetSendMessageByMtdiepAsync(string companyCd, string mtdiep);

        Task<IReadOnlyList<EInvoiceMessageWorkQueueItem>> GetPendingWorkQueueAsync(int limit);

        Task<IReadOnlyList<EInvoiceMessagePendingSendItem>> GetPendingSendWorkQueueAsync(int limit);

        Task UpdateWorkQueueStatusAsync(string companyCd, string sendMtdiep, string workStatus, string? errorMessage);

        Task ApplyReceiveXmlToInvoiceAsync(
            string dbName,
            string companyCd,
            string userId,
            long invoiceId,
            string xml,
            string? mccqt = null,
            int? invoiceStatus = null);

        Task ApplyReceiveErrorToInvoiceAsync(
            string dbName,
            string companyCd,
            string userId,
            long invoiceId,
            string errorMessage,
            int? invoiceStatus = null);

        Task ApplyReceiveXmlToDeclarationAsync(string dbName, string companyCd, string userId, long tkhaiId, string xml, int? cqtStatus = null, string? mccqt = null);

        Task ApplyReceiveErrorToDeclarationAsync(string dbName, string companyCd, string userId, long tkhaiId, string errorMessage, int? cqtStatus = null);

        Task ApplyCqtStatusToDeclarationAsync(string dbName, string companyCd, string userId, long tkhaiId, int cqtStatus, string? errorMessage = null);

        Task ApplyReceiveXmlToErrorNoticeAsync(string dbName, string companyCd, string userId, long tbaoId, string xml);

        Task ApplyReceiveErrorToErrorNoticeAsync(string dbName, string companyCd, string userId, long tbaoId, string? errorMessage);

        Task ApplyMgdDTuToTargetAsync(string dbName, string companyCd, string userId, string targetType, long targetId, string mgdDTu);
        Task ApplyPitSendStatusAsync(string dbName, string companyCd, string targetType, long targetId, int status, string? errorMessage);
    }
}
