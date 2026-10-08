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
            if (header.DETAILS.Select(x => x.OUTPUT_ID).Distinct().Count() != header.DETAILS.Count ||
                header.DETAILS.Select(x => x.CHITDETAIL_ID).Distinct().Count() != header.DETAILS.Count ||
                outputs.Select(x => x.OUTPUT_ID).Distinct().Count() != outputs.Count)
                throw new InvalidOperationException($"Issue voucher {transferId}: duplicate COGS link.");
            foreach (var output in outputs)
            {
                var detail = header.DETAILS.SingleOrDefault(x => x.OUTPUT_ID == output.OUTPUT_ID);
                if (output.OUTPUT_ID.GetValueOrDefault() <= 0 || detail == null || detail.CHITDETAIL_ID <= 0 ||
                    output.CHITDETAIL_ID_COGS != detail.CHITDETAIL_ID ||
                    string.IsNullOrWhiteSpace(detail.CHITDETAIL_CD) || string.IsNullOrWhiteSpace(detail.DEBIT) || string.IsNullOrWhiteSpace(detail.CREDIT) ||
                    output.AMOUNT_CC < 0 || detail.AMOUNT != Math.Round(output.AMOUNT_CC, 2, MidpointRounding.AwayFromZero))
                    throw new InvalidOperationException($"Issue voucher {transferId}, output {output.OUTPUT_ID}: missing or invalid COGS detail/link/amount.");
            }
            if (header.DETAILS.Count != outputs.Count || persistedDetailCount != outputs.Count ||
                header.AMOUNT != header.DETAILS.Sum(x => x.AMOUNT))
                throw new InvalidOperationException($"Issue voucher {transferId}: COGS detail count or total does not match.");
        }
    }
}
