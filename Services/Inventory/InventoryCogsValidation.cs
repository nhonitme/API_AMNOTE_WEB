using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Services.Inventory
{
    internal static class InventoryCogsValidation
    {
        public static void Validate(long transferId, InventoryCogsDto header, IReadOnlyList<InventoryOutput> outputs, int persistedDetailCount)
        {
            if (header.CHIT_ID <= 0 || string.IsNullOrWhiteSpace(header.CHIT_CD) ||
                string.IsNullOrWhiteSpace(header.CHIT_NO) || header.IS_LOCK is not ("0" or "1"))
                throw new InvalidOperationException($"Issue voucher {transferId}: invalid COGS header.");
            if (persistedDetailCount < 1 || header.DETAILS.Count != 1)
                throw new InvalidOperationException($"Issue voucher {transferId}: exactly one linked COGS detail is required.");
            var detail = header.DETAILS[0];
            if (detail.CHITDETAIL_ID <= 0 || string.IsNullOrWhiteSpace(detail.CHITDETAIL_CD) ||
                detail.OUTPUT_ID != 0 || detail.DEBIT != "632" || (detail.CREDIT != "156" && detail.CREDIT != "1561") || detail.AMOUNT < 0 ||
                (persistedDetailCount == 1 && detail.AMOUNT != header.AMOUNT) || outputs.Any(x => x.AMOUNT_CC < 0))
                throw new InvalidOperationException($"Issue voucher {transferId}: invalid aggregate COGS detail.");
        }
    }
}
