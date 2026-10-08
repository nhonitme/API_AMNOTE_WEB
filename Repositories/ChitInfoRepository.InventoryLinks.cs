using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Repositories
{
    public partial class ChitInfoRepository
    {
        public async Task<ChitInventoryLinkStatusDto?> GetInventoryLinkStatusAsync(string companyCd, string sourceChitType, long sourceChitId)
        {
            var normalizedSourceType = (sourceChitType ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(companyCd) || sourceChitId <= 0)
            {
                return null;
            }

            if (normalizedSourceType == "IR" || normalizedSourceType == "IO")
            {
                return await GetInventoryOnlySourceLinkStatusAsync(companyCd, normalizedSourceType, sourceChitId);
            }

            if (!SupportsInventoryInputLinks(normalizedSourceType) && !SupportsInventoryOutputLinks(normalizedSourceType))
            {
                var statusRow = (await QueryInventoryLinkStatusRowsAsync(companyCd, normalizedSourceType, sourceChitId.ToString())).FirstOrDefault();
                if (statusRow == null)
                {
                    return null;
                }

                return new ChitInventoryLinkStatusDto
                {
                    CHIT_ID = statusRow.CHIT_ID,
                    CHIT_CD = statusRow.CHIT_CD,
                    CHIT_TYPE = statusRow.CHIT_TYPE,
                    SOURCE_TOTAL_QUANTITY = 0,
                    LINKED_TOTAL_QUANTITY = 0,
                    REMAINING_QUANTITY = 0,
                    INVENTORY_VOUCHER_COUNT = 0,
                    INVENTORY_STATUS = "NONE",
                    LINKED_CHITDETAIL_IDS = new List<long>(),
                    INVENTORY_VOUCHERS = new List<ChitInventoryLinkVoucherDto>()
                };
            }

            var statusRowResult = (await QueryInventoryLinkStatusRowsAsync(companyCd, normalizedSourceType, sourceChitId.ToString())).FirstOrDefault();
            if (statusRowResult == null)
            {
                return null;
            }

            var inventoryVouchers = (await QueryInventoryLinkVoucherRowsAsync(companyCd, normalizedSourceType, sourceChitId.ToString()))
                .Select(MapInventoryLinkVoucher)
                .ToList();

            return new ChitInventoryLinkStatusDto
            {
                CHIT_ID = statusRowResult.CHIT_ID,
                CHIT_CD = statusRowResult.CHIT_CD,
                CHIT_TYPE = statusRowResult.CHIT_TYPE,
                SOURCE_TOTAL_QUANTITY = statusRowResult.SOURCE_TOTAL_QUANTITY,
                LINKED_TOTAL_QUANTITY = statusRowResult.LINKED_TOTAL_QUANTITY,
                REMAINING_QUANTITY = Math.Max(statusRowResult.SOURCE_TOTAL_QUANTITY - statusRowResult.LINKED_TOTAL_QUANTITY, 0),
                INVENTORY_VOUCHER_COUNT = statusRowResult.INVENTORY_VOUCHER_COUNT,
                INVENTORY_STATUS = statusRowResult.LINKED_TOTAL_QUANTITY > 0
                    ? statusRowResult.SOURCE_TOTAL_QUANTITY > statusRowResult.LINKED_TOTAL_QUANTITY
                        ? "PARTIAL"
                        : "FULL"
                    : "NONE",
                LINKED_CHITDETAIL_IDS = statusRowResult.LINKED_CHITDETAIL_IDS == null
                    ? new List<long>()
                    : statusRowResult.LINKED_CHITDETAIL_IDS.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(value => long.TryParse(value, out var parsed) ? parsed : 0)
                        .Where(parsed => parsed > 0)
                        .Distinct()
                        .ToList(),
                INVENTORY_VOUCHERS = inventoryVouchers
            };
        }

        public async Task<IReadOnlyList<ChitInventoryLinkStatusDto>> GetInventoryLinkStatusesAsync(string companyCd, string sourceChitType, IEnumerable<long> sourceChitIds)
        {
            var normalizedSourceType = (sourceChitType ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(companyCd) || sourceChitIds == null)
            {
                return new List<ChitInventoryLinkStatusDto>();
            }

            var ids = sourceChitIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0)
            {
                return new List<ChitInventoryLinkStatusDto>();
            }

            var statusRows = await QueryInventoryLinkStatusRowsAsync(companyCd, normalizedSourceType, string.Join(",", ids));
            if (!SupportsInventoryInputLinks(normalizedSourceType)
                && !SupportsInventoryOutputLinks(normalizedSourceType)
                && normalizedSourceType != "IR"
                && normalizedSourceType != "IO")
            {
                return statusRows.Select(item => new ChitInventoryLinkStatusDto
                {
                    CHIT_ID = item.CHIT_ID,
                    CHIT_CD = item.CHIT_CD,
                    CHIT_TYPE = item.CHIT_TYPE,
                    SOURCE_TOTAL_QUANTITY = 0,
                    LINKED_TOTAL_QUANTITY = 0,
                    REMAINING_QUANTITY = 0,
                    INVENTORY_VOUCHER_COUNT = 0,
                    INVENTORY_STATUS = "NONE",
                    LINKED_CHITDETAIL_IDS = new List<long>(),
                    INVENTORY_VOUCHERS = new List<ChitInventoryLinkVoucherDto>()
                }).ToList();
            }

            var voucherLookup = new Dictionary<long, List<ChitInventoryLinkVoucherDto>>();
            if (SupportsInventoryInputLinks(normalizedSourceType)
                || SupportsInventoryOutputLinks(normalizedSourceType)
                || normalizedSourceType == "IR"
                || normalizedSourceType == "IO")
            {
                voucherLookup = (await QueryInventoryLinkVoucherRowsAsync(companyCd, normalizedSourceType, string.Join(",", ids)))
                    .GroupBy(item => item.SOURCE_CHIT_ID)
                    .ToDictionary(group => group.Key, group => group.Select(MapInventoryLinkVoucher).ToList());
            }

            return statusRows.Select(item => new ChitInventoryLinkStatusDto
            {
                CHIT_ID = item.CHIT_ID,
                CHIT_CD = item.CHIT_CD,
                CHIT_TYPE = item.CHIT_TYPE,
                SOURCE_TOTAL_QUANTITY = item.SOURCE_TOTAL_QUANTITY,
                LINKED_TOTAL_QUANTITY = item.LINKED_TOTAL_QUANTITY,
                REMAINING_QUANTITY = Math.Max(item.SOURCE_TOTAL_QUANTITY - item.LINKED_TOTAL_QUANTITY, 0),
                INVENTORY_VOUCHER_COUNT = item.INVENTORY_VOUCHER_COUNT,
                INVENTORY_STATUS = item.LINKED_TOTAL_QUANTITY > 0
                    ? item.SOURCE_TOTAL_QUANTITY > item.LINKED_TOTAL_QUANTITY
                        ? "PARTIAL"
                        : "FULL"
                    : "NONE",
                LINKED_CHITDETAIL_IDS = item.LINKED_CHITDETAIL_IDS == null
                    ? new List<long>()
                    : item.LINKED_CHITDETAIL_IDS.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(value => long.TryParse(value, out var parsed) ? parsed : 0)
                        .Where(parsed => parsed > 0)
                        .Distinct()
                        .ToList(),
                INVENTORY_VOUCHERS = voucherLookup.TryGetValue(item.CHIT_ID, out var vouchers)
                    ? vouchers
                    : new List<ChitInventoryLinkVoucherDto>()
            }).ToList();
        }

        private async Task<ChitInventoryLinkStatusDto?> GetInventoryOnlySourceLinkStatusAsync(string companyCd, string sourceChitType, long sourceChitId)
        {
            var normalizedSourceType = (sourceChitType ?? string.Empty).Trim().ToUpperInvariant();
            var statusRow = (await QueryInventoryLinkStatusRowsAsync(companyCd, normalizedSourceType, sourceChitId.ToString())).FirstOrDefault();
            if (statusRow == null)
            {
                return null;
            }

            var inventoryVouchers = (await QueryInventoryLinkVoucherRowsAsync(companyCd, normalizedSourceType, sourceChitId.ToString()))
                .Select(MapInventoryLinkVoucher)
                .ToList();

            return new ChitInventoryLinkStatusDto
            {
                CHIT_ID = statusRow.CHIT_ID,
                CHIT_CD = statusRow.CHIT_CD,
                CHIT_TYPE = statusRow.CHIT_TYPE,
                SOURCE_TOTAL_QUANTITY = statusRow.SOURCE_TOTAL_QUANTITY,
                LINKED_TOTAL_QUANTITY = statusRow.LINKED_TOTAL_QUANTITY,
                REMAINING_QUANTITY = Math.Max(statusRow.SOURCE_TOTAL_QUANTITY - statusRow.LINKED_TOTAL_QUANTITY, 0),
                INVENTORY_VOUCHER_COUNT = statusRow.INVENTORY_VOUCHER_COUNT,
                INVENTORY_STATUS = statusRow.LINKED_TOTAL_QUANTITY > 0
                    ? statusRow.SOURCE_TOTAL_QUANTITY > statusRow.LINKED_TOTAL_QUANTITY
                        ? "PARTIAL"
                        : "FULL"
                    : "NONE",
                LINKED_CHITDETAIL_IDS = statusRow.LINKED_CHITDETAIL_IDS == null
                    ? new List<long>()
                    : statusRow.LINKED_CHITDETAIL_IDS.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(value => long.TryParse(value, out var parsed) ? parsed : 0)
                        .Where(parsed => parsed > 0)
                        .Distinct()
                        .ToList(),
                INVENTORY_VOUCHERS = inventoryVouchers
            };
        }

        private async Task<List<InventoryLinkStatusRow>> QueryInventoryLinkStatusRowsAsync(string companyCd, string chitType, string chitIds)
        {
            const string proc = "CALL getChitInventoryLinkStatusRows(@p_COMPANY_CD, @p_CHIT_TYPE, @p_CHIT_IDS)";
            var rows = await _db.QueryAsync<InventoryLinkStatusRow>(Net_DB.Net_DB_Company, proc, new
            {
                p_COMPANY_CD = companyCd,
                p_CHIT_TYPE = chitType,
                p_CHIT_IDS = chitIds
            });
            return rows.ToList();
        }

        private async Task<List<InventoryLinkVoucherRow>> QueryInventoryLinkVoucherRowsAsync(string companyCd, string chitType, string chitIds)
        {
            const string proc = "CALL getChitInventoryLinkVoucherRows(@p_COMPANY_CD, @p_CHIT_TYPE, @p_CHIT_IDS)";
            var rows = await _db.QueryAsync<InventoryLinkVoucherRow>(Net_DB.Net_DB_Company, proc, new
            {
                p_COMPANY_CD = companyCd,
                p_CHIT_TYPE = chitType,
                p_CHIT_IDS = chitIds
            });
            return rows.ToList();
        }

        public async Task<ChitInventorySourceVoucherDto?> GetInventorySourceVoucherByDetailIdAsync(string companyCd, long chitDetailId)
        {
            if (string.IsNullOrWhiteSpace(companyCd) || chitDetailId <= 0)
            {
                return null;
            }

            const string query = @"
                SELECT
                    header.CHIT_ID,
                    header.CHIT_CD,
                    header.CHIT_TYPE,
                    detail.CHITDETAIL_ID,
                    detail.CHITDETAIL_CD
                FROM chitdetailinfo detail
                INNER JOIN chitinfo header
                    ON header.COMPANY_CD = detail.COMPANY_CD
                   AND header.CHIT_ID = detail.CHIT_ID
                WHERE detail.COMPANY_CD = @p_COMPANY_CD
                  AND detail.CHITDETAIL_ID = @p_CHITDETAIL_ID
                  AND IFNULL(detail.ISDEL, '0') = '0'
                  AND IFNULL(header.ISDEL, '0') = '0'
                  AND IFNULL(header.CHIT_TYPE, '') IN ('PO', 'PD', 'PR', 'SO', 'SD', 'SR')
                LIMIT 1";

            var items = await _db.QueryAsync<ChitInventorySourceVoucherDto>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_CHITDETAIL_ID = chitDetailId
            });

            return items.FirstOrDefault();
        }

        private static ChitInventoryLinkVoucherDto MapInventoryLinkVoucher(InventoryLinkVoucherRow item)
        {
            return new ChitInventoryLinkVoucherDto
            {
                INVENTORY_CHIT_ID = item.INVENTORY_CHIT_ID,
                INVENTORY_CHIT_CD = item.INVENTORY_CHIT_CD,
                INVENTORY_CHIT_NO = item.INVENTORY_CHIT_NO,
                INVENTORY_CHIT_YMD = item.INVENTORY_CHIT_YMD,
                INVENTORY_CHIT_TYPE = item.INVENTORY_CHIT_TYPE,
                LINKED_QUANTITY = item.LINKED_QUANTITY,
                LINKED_LINE_COUNT = item.LINKED_LINE_COUNT
            };
        }
    }
}
