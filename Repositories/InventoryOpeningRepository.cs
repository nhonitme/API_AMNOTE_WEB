using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace API_AMNOTE_WEB.Repositories
{
    public sealed class InventoryOpeningRepository : IInventoryOpeningRepository
    {
        private const string ChitType = "IRO";
        private const string InputType = "INV";
        private const string OpeningState = "0";

        private sealed class TransferIdentity
        {
            public long TRANSFER_ID { get; set; }
            public string TRANSFER_CD { get; set; } = string.Empty;
        }

        private sealed class TransferHeader
        {
            public long TRANSFER_ID { get; set; }
            public string TRANSFER_CD { get; set; } = string.Empty;
            public string? TRANSFER_NO { get; set; }
            public string? TRANSFER_YMD { get; set; }
            public string? REMARK { get; set; }
            public int? SORT { get; set; }
        }

        private sealed class MasterRef
        {
            public long ID { get; set; }
            public string CD { get; set; } = string.Empty;
            public long? UNIT_ID { get; set; }
            public string? UNIT_CD { get; set; }
        }

        private sealed class ExistsCountResult
        {
            public long EXISTS_CNT { get; set; }
        }

        private readonly DapperExecutor _db;
        private readonly ISysCodeSequenceRepository _sequenceRepository;
        private readonly IActivityLogService _activityLogService;
        private readonly ILogger<InventoryOpeningRepository> _logger;

        public InventoryOpeningRepository(
            DapperExecutor db,
            ISysCodeSequenceRepository sequenceRepository,
            IActivityLogService activityLogService,
            ILogger<InventoryOpeningRepository> logger)
        {
            _db = db;
            _sequenceRepository = sequenceRepository;
            _activityLogService = activityLogService;
            _logger = logger;
        }

        public async Task<IReadOnlyList<InventoryOpening>> GetListAsync(string companyCd, long? inputId = null)
        {
            var language = Common.GetCurrentLanguage();

            const string query = "CALL sp_inventory_opening_get(@p_COMPANY_CD, @p_INPUT_ID, @p_LANGUAGE)";
            var items = await _db.QueryAsync<InventoryOpening>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_INPUT_ID = inputId ?? 0,                
                p_LANGUAGE = language
            });

            return items.ToList();
        }

        public async Task<InventoryOpening?> GetByIdAsync(string companyCd, long inputId)
        {
            if (inputId <= 0)
            {
                return null;
            }

            return (await GetListAsync(companyCd, inputId)).FirstOrDefault();
        }

        public async Task<bool> ProductStoreExistsAsync(
            string companyCd,
            long productId,
            long storeId,
            long? excludeInputId = null)
        {
            const string query = "CALL sp_inventory_opening_check_product_store(@p_COMPANY_CD, @p_PRODUCT_ID, @p_STORE_ID, @p_EXCLUDE_INPUT_ID)";
            var count = (await _db.QueryAsync<ExistsCountResult>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_PRODUCT_ID = productId,
                p_STORE_ID = storeId,
                p_EXCLUDE_INPUT_ID = excludeInputId ?? 0
            })).FirstOrDefault()?.EXISTS_CNT ?? 0;

            return count > 0;
        }

        public async Task<InventoryOpening> CreateAsync(string companyCd, string userId, InventoryOpeningRequest request)
        {
            var normalized = await NormalizeRequestAsync(companyCd, request, excludeInputId: null);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                var transferYmd = DateTime.Now.ToString("yyyyMMdd");
                var transferCd = Common.GenerateKeyCd("C");
                var transferNo = await ResolveTransferNoAsync(session, companyCd, transferYmd);

                var identity = await UpsertTransferAsync(
                    session,
                    companyCd,
                    userId,
                    transferId: 0,
                    transferCd,
                    transferNo,
                    transferYmd,
                    amount: normalized.AmountCc,
                    totalQty: normalized.Quantity,
                    remark: normalized.Summary,
                    sort: 0);

                var savedTransferId = identity.TRANSFER_ID;
                var savedTransferCd = Common.NormalizeNullableText(identity.TRANSFER_CD) ?? transferCd;

                var inputId = await UpsertInputAsync(
                    session,
                    companyCd,
                    userId,
                    inputId: 0,
                    inputCd: string.Empty,
                    transferId: savedTransferId,
                    transferCd: savedTransferCd,
                    normalized);

                await _activityLogService.LogAsync(
                    session.Connection,
                    session.Transaction,
                    companyCd,
                    "INSERT",
                    "InventoryOpening",
                    "chit_inventory_input",
                    savedTransferCd,
                    string.Empty,
                    JsonSerializer.Serialize(new { TRANSFER_ID = savedTransferId, INPUT_ID = inputId, normalized }),
                    "Create inventory opening (IRO)");

                session.Commit();

                return await GetByIdAsync(companyCd, inputId)
                    ?? throw new InvalidOperationException(await Common.getLanguage("INV_OPENING_LOAD_CREATED_FAIL", Common.GetCurrentLanguage()));
            }
            catch (Exception ex)
            {
                session.Rollback();
                _logger.LogError(ex, "Failed to create inventory opening for company {CompanyCd}", companyCd);
                throw;
            }
        }

        public async Task<InventoryOpening> UpdateAsync(string companyCd, string userId, long inputId, InventoryOpeningRequest request)
        {
            var existing = await GetByIdAsync(companyCd, inputId)
                ?? throw new KeyNotFoundException(await Common.getLanguage("INV_OPENING_NOT_FOUND", Common.GetCurrentLanguage()));

            var normalized = await NormalizeRequestAsync(companyCd, request, excludeInputId: inputId);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                var header = await GetTransferHeaderAsync(session, companyCd, existing.TRANSFER_ID)
                    ?? throw new KeyNotFoundException(await Common.getLanguage("INV_OPENING_TRANSFER_NOT_FOUND", Common.GetCurrentLanguage()));

                await UpsertInputAsync(
                    session,
                    companyCd,
                    userId,
                    inputId: existing.INPUT_ID,
                    inputCd: existing.INPUT_CD,
                    transferId: existing.TRANSFER_ID,
                    transferCd: existing.TRANSFER_CD,
                    normalized);

                await UpsertTransferAsync(
                    session,
                    companyCd,
                    userId,
                    transferId: existing.TRANSFER_ID,
                    transferCd: header.TRANSFER_CD,
                    transferNo: header.TRANSFER_NO,
                    transferYmd: header.TRANSFER_YMD ?? DateTime.Now.ToString("yyyyMMdd"),
                    amount: normalized.AmountCc,
                    totalQty: normalized.Quantity,
                    remark: normalized.Summary ?? header.REMARK,
                    sort: header.SORT ?? 0);

                await _activityLogService.LogAsync(
                    session.Connection,
                    session.Transaction,
                    companyCd,
                    "UPDATE",
                    "InventoryOpening",
                    "chit_inventory_input",
                    existing.TRANSFER_CD,
                    JsonSerializer.Serialize(existing),
                    JsonSerializer.Serialize(normalized),
                    "Update inventory opening (IRO)");

                session.Commit();

                return await GetByIdAsync(companyCd, inputId)
                    ?? throw new InvalidOperationException(await Common.getLanguage("INV_OPENING_LOAD_UPDATED_FAIL", Common.GetCurrentLanguage()));
            }
            catch (Exception ex)
            {
                session.Rollback();
                _logger.LogError(ex, "Failed to update inventory opening {InputId} for company {CompanyCd}", inputId, companyCd);
                throw;
            }
        }

        public async Task<int> DeleteAsync(string companyCd, string userId, IReadOnlyList<long> inputIds)
        {
            var ids = inputIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0)
            {
                return 0;
            }

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                var deleted = 0;
                foreach (var inputId in ids)
                {
                    var existing = await GetByIdAsync(companyCd, inputId);
                    if (existing == null)
                    {
                        continue;
                    }

                    await session.ExecuteAsync(
                        "CALL delChitInventoryInput(@p_COMPANY_CD, @p_INPUT_ID, @p_CHIT_ID, @p_USER)",
                        new
                        {
                            p_COMPANY_CD = companyCd,
                            p_INPUT_ID = existing.INPUT_ID,
                            p_CHIT_ID = existing.TRANSFER_ID,
                            p_USER = userId
                        });

                    await session.ExecuteAsync(
                        "CALL delChitInventoryTransfer(@p_COMPANY_CD, @p_INPUT_TYPE, @p_TRANSFER_ID, @p_UPDATE_BY)",
                        new
                        {
                            p_COMPANY_CD = companyCd,
                            p_INPUT_TYPE = InputType,
                            p_TRANSFER_ID = existing.TRANSFER_ID,
                            p_UPDATE_BY = userId
                        });

                    await _activityLogService.LogAsync(
                        session.Connection,
                        session.Transaction,
                        companyCd,
                        "DELETE",
                        "InventoryOpening",
                        "chit_inventory_input",
                        existing.TRANSFER_CD,
                        JsonSerializer.Serialize(existing),
                        string.Empty,
                        "Delete inventory opening (IRO)");

                    deleted++;
                }

                session.Commit();
                return deleted;
            }
            catch (Exception ex)
            {
                session.Rollback();
                _logger.LogError(ex, "Failed to delete inventory opening rows for company {CompanyCd}", companyCd);
                throw;
            }
        }

        private async Task<NormalizedOpening> NormalizeRequestAsync(
            string companyCd,
            InventoryOpeningRequest request,
            long? excludeInputId)
        {
            var product = await ResolveProductAsync(companyCd, request.PRODUCT_ID, request.PRODUCT_CD);
            if (product == null)
            {
                throw new ArgumentException(await Common.getLanguage("INV_OPENING_PRODUCT_CD_REQUIRED", Common.GetCurrentLanguage()));
            }

            var store = await ResolveStoreAsync(companyCd, request.STORE_ID, request.STORE_CD);
            if (store == null)
            {
                throw new ArgumentException(await Common.getLanguage("INV_OPENING_STORE_CD_REQUIRED", Common.GetCurrentLanguage()));
            }

            var productStoreExists = await ProductStoreExistsAsync(companyCd, product.ID, store.ID, excludeInputId);
            if (productStoreExists)
            {
                throw new InvalidOperationException(await Common.getLanguage("INV_OPENING_PRODUCT_STORE_EXISTS", Common.GetCurrentLanguage()));
            }

            var unit = await ResolveUnitAsync(companyCd, request.UNIT_ID, request.UNIT_CD, product);
            var quantity = request.QUANTITY;
            var unitPrice = request.UNIT_PRICE_CC;
            var amount = request.AMOUNT_CC.HasValue
                ? request.AMOUNT_CC.Value
                : (quantity * unitPrice);

            return new NormalizedOpening(
                product.ID,
                product.CD,
                store.ID,
                store.CD,
                unit.ID,
                unit.CD,
                quantity,
                unitPrice,
                amount,
                Common.NormalizeNullableText(request.SUMMARY) ?? string.Empty);
        }

        private async Task<MasterRef?> ResolveProductAsync(string companyCd, long? productId, string? productCd)
        {
            const string query = "CALL sp_inventory_opening_product_get(@p_COMPANY_CD, @p_PRODUCT_ID, @p_PRODUCT_CD)";
            return (await _db.QueryAsync<MasterRef>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_PRODUCT_ID = productId.GetValueOrDefault(),
                p_PRODUCT_CD = Common.NormalizeNullableText(productCd) ?? string.Empty
            })).FirstOrDefault();
        }

        private async Task<MasterRef?> ResolveStoreAsync(string companyCd, long? storeId, string? storeCd)
        {
            const string query = "CALL sp_inventory_opening_store_get(@p_COMPANY_CD, @p_STORE_ID, @p_STORE_CD)";
            return (await _db.QueryAsync<MasterRef>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_STORE_ID = storeId.GetValueOrDefault(),
                p_STORE_CD = Common.NormalizeNullableText(storeCd) ?? string.Empty
            })).FirstOrDefault();
        }

        private async Task<(long ID, string CD)> ResolveUnitAsync(
            string companyCd,
            long? unitId,
            string? unitCd,
            MasterRef product)
        {
            if (unitId.GetValueOrDefault() > 0 || !string.IsNullOrWhiteSpace(Common.NormalizeNullableText(unitCd)))
            {
                const string query = "CALL sp_inventory_opening_unit_get(@p_COMPANY_CD, @p_UNIT_ID, @p_UNIT_CD)";
                var row = (await _db.QueryAsync<MasterRef>(Net_DB.Net_DB_Company, query, new
                {
                    p_COMPANY_CD = companyCd,
                    p_UNIT_ID = unitId.GetValueOrDefault(),
                    p_UNIT_CD = Common.NormalizeNullableText(unitCd) ?? string.Empty
                })).FirstOrDefault();

                if (row != null)
                {
                    return (row.ID, row.CD);
                }

                if (unitId.GetValueOrDefault() > 0)
                {
                    throw new ArgumentException(await Common.getLanguage("INV_OPENING_UNIT_CD_REQUIRED", Common.GetCurrentLanguage()));
                }
            }

            if (product.UNIT_ID.GetValueOrDefault() > 0)
            {
                return (product.UNIT_ID.GetValueOrDefault(), product.UNIT_CD ?? string.Empty);
            }

            throw new ArgumentException(await Common.getLanguage("INV_OPENING_UNIT_CD_REQUIRED", Common.GetCurrentLanguage()));
        }

        private async Task<string> ResolveTransferNoAsync(DapperSession session, string companyCd, string transferYmd)
        {
            try
            {
                return await CodeSequenceHelper.ResolveRequiredCodeAsync(
                    _sequenceRepository,
                    session,
                    companyCd,
                    ChitType,
                    requestedCode: null,
                    "Inventory opening no",
                    Common.ParseNullableDateTimeText(transferYmd));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "IRO sequence missing; fallback GenerateKeyCd for TRANSFER_NO");
                return Common.GenerateKeyCd("C");
            }
        }

        private static async Task<TransferIdentity> UpsertTransferAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long transferId,
            string transferCd,
            string? transferNo,
            string transferYmd,
            decimal amount,
            decimal totalQty,
            string? remark,
            int sort)
        {
            const string query = @"CALL setChitInventoryTransfer(
                @p_TRANSFER_ID,
                @p_COMPANY_CD,
                @p_TRANSFER_CD,
                @p_TRANSFER_NO,
                @p_TRANSFER_YMD,
                @p_CHIT_TYPE,
                @p_INPUT_TYPE,
                @p_AMOUNT,
                @p_TOTAL_QTY,
                @p_REMARK,
                @p_SORT,
                @p_USER
            )";

            return await session.QuerySingleAsync<TransferIdentity>(query, new
            {
                p_TRANSFER_ID = transferId > 0 ? transferId : (long?)null,
                p_COMPANY_CD = companyCd,
                p_TRANSFER_CD = transferCd,
                p_TRANSFER_NO = transferNo,
                p_TRANSFER_YMD = transferYmd,
                p_CHIT_TYPE = ChitType,
                p_INPUT_TYPE = InputType,
                p_AMOUNT = amount,
                p_TOTAL_QTY = totalQty,
                p_REMARK = remark,
                p_SORT = sort,
                p_USER = userId
            });
        }

        private static async Task<long> UpsertInputAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long inputId,
            string? inputCd,
            long transferId,
            string transferCd,
            NormalizedOpening normalized)
        {
            var inventoryYmd = DateTime.Today;

            return await session.QuerySingleAsync<long>(
                "CALL setChitInventoryInput(@p_INPUT_ID, @p_INPUT_CD, @p_CHIT_ID, @p_CHIT_CD, @p_CHIT_TYPE, @p_COMPANY_CD, @p_PRODUCT_ID, @p_PRODUCT_CD, @p_STORE_ID, @p_STORE_CD, @p_UNIT_ID, @p_UNIT_CD, @p_QUANTITY, @p_UNIT_PRICE_CC, @p_FC_TYPE, @p_UNIT_PRICE_FC, @p_EXCHANGE_RATES, @p_AMOUNT_CC, @p_AMOUNT_FC, @p_SUMMARY, @p_INVENTORY_YMD, @p_STATE, @p_CHITDETAIL_ID, @p_CHITDETAIL_CD, @p_SORT, @p_USER)",
                new
                {
                    p_INPUT_ID = inputId > 0 ? inputId : (long?)null,
                    p_INPUT_CD = inputCd ?? string.Empty,
                    p_CHIT_ID = transferId,
                    p_CHIT_CD = transferCd,
                    p_CHIT_TYPE = ChitType,
                    p_COMPANY_CD = companyCd,
                    p_PRODUCT_ID = normalized.ProductId,
                    p_PRODUCT_CD = normalized.ProductCd,
                    p_STORE_ID = normalized.StoreId,
                    p_STORE_CD = normalized.StoreCd,
                    p_UNIT_ID = normalized.UnitId,
                    p_UNIT_CD = normalized.UnitCd,
                    p_QUANTITY = normalized.Quantity,
                    p_UNIT_PRICE_CC = normalized.UnitPriceCc,
                    p_FC_TYPE = "VND",
                    p_UNIT_PRICE_FC = normalized.UnitPriceCc,
                    p_EXCHANGE_RATES = 1d,
                    p_AMOUNT_CC = normalized.AmountCc,
                    p_AMOUNT_FC = normalized.AmountCc,
                    p_SUMMARY = normalized.Summary,
                    p_INVENTORY_YMD = inventoryYmd,
                    p_STATE = OpeningState,
                    p_CHITDETAIL_ID = (long?)null,
                    p_CHITDETAIL_CD = (string?)null,
                    p_SORT = 1,
                    p_USER = userId
                });
        }

        private static async Task<TransferHeader?> GetTransferHeaderAsync(
            DapperSession session,
            string companyCd,
            long transferId)
        {
            const string query = "CALL sp_inventory_opening_transfer_get(@p_COMPANY_CD, @p_TRANSFER_ID)";
            return (await session.QueryAsync<TransferHeader>(query, new
            {
                p_COMPANY_CD = companyCd,
                p_TRANSFER_ID = transferId
            })).FirstOrDefault();
        }

        private sealed record NormalizedOpening(
            long ProductId,
            string ProductCd,
            long StoreId,
            string StoreCd,
            long UnitId,
            string UnitCd,
            decimal Quantity,
            decimal UnitPriceCc,
            decimal AmountCc,
            string Summary);
    }
}
