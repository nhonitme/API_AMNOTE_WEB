using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace API_AMNOTE_WEB.Repositories
{
    public sealed partial class InventoryVoucherRepository : IInventoryVoucherRepository
    {
        private sealed class InventoryReference
        {
            public long Id { get; set; }
            public string Code { get; set; } = string.Empty;
        }

        private sealed class TotalRecordResult
        {
            public int TOTAL_RECORDS { get; set; }
        }

        private sealed class InventoryVoucherHeader
        {
            public long TRANSFER_ID { get; set; }
            public long? CHIT_ID_COGS { get; set; }
            public long? CHITDETAIL_ID_COGS { get; set; }
            public string IS_COGS { get; set; } = "0";
            public string COMPANY_CD { get; set; } = string.Empty;
            public string TRANSFER_CD { get; set; } = string.Empty;
            public string? TRANSFER_NO { get; set; }
            public string? TRANSFER_YMD { get; set; }
            public string CHIT_TYPE { get; set; } = string.Empty;
            public string INPUT_TYPE { get; set; } = string.Empty;
            public decimal AMOUNT { get; set; }
            public decimal? REVENUE_AMOUNT { get; set; }
            public decimal TOTAL_QTY { get; set; }
            public string? REMARK { get; set; }
            public long? CUSTOMER_ID { get; set; }
            public string? CUSTOMER_CD { get; set; }
            public int? SORT { get; set; }
            public string? ISDEL { get; set; }
            [API_AMNOTE_WEB.Helpers.BackendAudit]
            public string? CREATE_BY { get; set; }
            [API_AMNOTE_WEB.Helpers.BackendAudit]
            public DateTime? CREATE_AT { get; set; }
            [API_AMNOTE_WEB.Helpers.BackendAudit]
            public string? UPDATE_BY { get; set; }
            [API_AMNOTE_WEB.Helpers.BackendAudit]
            public DateTime? UPDATE_AT { get; set; }
        }

        private sealed class InventoryVoucherIdentity
        {
            public long TRANSFER_ID { get; set; }
            public string TRANSFER_CD { get; set; } = string.Empty;
        }

        private sealed class InventoryVoucherCdExistsResult
        {
            public int EXISTS_YN { get; set; }
        }

        private sealed class InventoryInputVoucherRow
        {
            public long INPUT_ID { get; set; }
            public long INVENTORY_ID { get; set; }
            public long CHITDETAIL_ID { get; set; }
        }

        private sealed class InventoryOutputVoucherRow
        {
            public long OUTPUT_ID { get; set; }
            public long INVENTORY_ID { get; set; }
            public long CHITDETAIL_ID { get; set; }
        }

        private sealed class InventoryCogsSourceRow
        {
            public long INVENTORY_CHIT_ID { get; set; }
        }

        private readonly DapperExecutor _db;
        private readonly IActivityLogService _activityLogService;
        private readonly ISysCodeSequenceRepository _sequenceRepository;
        private readonly ILogger<InventoryVoucherRepository> _logger;

        public InventoryVoucherRepository(
            DapperExecutor db,
            IActivityLogService activityLogService,
            ISysCodeSequenceRepository sequenceRepository,
            ILogger<InventoryVoucherRepository> logger)
        {
            _db = db;
            _activityLogService = activityLogService;
            _sequenceRepository = sequenceRepository;
            _logger = logger;
        }

        public async Task<(IReadOnlyList<InventoryVoucherDto> Items, int TotalRecords)> GetInventoryVouchersPagedAsync(
            string companyCd,
            string inputType,
            string chitType,
            long? chitId = null,
            string? searchText = null,
            string? fromYmd = null,
            string? toYmd = null,
            int pageNumber = 1,
            int pageSize = 20)
        {
            var normalizedChitType = Common.NormalizeInventoryChitType(chitType);
            var result = await GetInventoryHeadersPagedAsync(
                companyCd,
                inputType,
                normalizedChitType,
                chitId,
                searchText,
                fromYmd,
                toYmd,
                pageNumber,
                pageSize);
            var headers = result.Items.ToList();
            var headerIds = headers.Select(item => item.TRANSFER_ID).Where(id => id > 0).Distinct().ToList();
            var inputLookup = new Dictionary<long, List<InventoryInput>>();
            var outputLookup = new Dictionary<long, List<InventoryOutput>>();

            if (headerIds.Count > 0 && Common.UsesInventoryInput(normalizedChitType))
            {
                inputLookup = (await GetInventoryInputsByChitIdsAsync(companyCd, headerIds))
                    .GroupBy(item => item.INVENTORY_ID.GetValueOrDefault())
                    .Where(group => group.Key > 0)
                    .ToDictionary(group => group.Key, group => group.OrderBy(item => item.SORT ?? int.MaxValue).ThenBy(item => item.INPUT_ID ?? long.MaxValue).ToList());
            }

            if (headerIds.Count > 0 && Common.UsesInventoryOutput(normalizedChitType))
            {
                outputLookup = (await GetInventoryOutputsByChitIdsAsync(companyCd, headerIds))
                    .GroupBy(item => item.INVENTORY_ID.GetValueOrDefault())
                    .Where(group => group.Key > 0)
                    .ToDictionary(group => group.Key, group => group.OrderBy(item => item.SORT ?? int.MaxValue).ThenBy(item => item.OUTPUT_ID ?? long.MaxValue).ToList());
            }

            var vouchers = headers.Select(item => MapInventoryVoucher(item, inputLookup, outputLookup)).ToList();
            if (normalizedChitType == "IO" && vouchers.Count > 0)
            {
                await PopulateInventoryCogsAsync(companyCd, vouchers);
            }
            return (vouchers, result.TotalRecords);
        }

        public async Task<IReadOnlyList<InventoryCogsIssueLinkDto>> GetInventoryIssueLinksByCogsVoucherIdAsync(string companyCd, long cogsVoucherId)
        {
            var (links, _) = await _db.QueryMultipleAsync<InventoryCogsIssueLinkDto, InventoryCogsSourceRow>(
                Net_DB.Net_DB_Company,
                "CALL getInventoryIssueLinksByCogsVoucher(@companyCd, @cogsVoucherId, @cogsDetailId)",
                new { companyCd, cogsVoucherId, cogsDetailId = (long?)null });
            return links.ToList();
        }

        public async Task<long?> GetInventoryIssueIdByCogsVoucherIdAsync(string companyCd, long cogsVoucherId, long? cogsDetailId = null)
        {
            if (cogsVoucherId <= 0)
            {
                return null;
            }

            var (_, candidates) = await _db.QueryMultipleAsync<InventoryCogsIssueLinkDto, InventoryCogsSourceRow>(
                Net_DB.Net_DB_Company,
                "CALL getInventoryIssueLinksByCogsVoucher(@companyCd, @cogsVoucherId, @cogsDetailId)",
                new { companyCd, cogsVoucherId, cogsDetailId });

            var source = candidates.SingleOrDefault();
            return source is { INVENTORY_CHIT_ID: > 0 } ? source.INVENTORY_CHIT_ID : null;
        }

        public async Task<long> SaveInventoryVoucherAsync(string companyCd, string inputType, string userId, InventoryVoucherRequest request, string? databaseName = null)
        {
            var normalizedChitType = Common.NormalizeInventoryChitType(request.CHIT_TYPE);
            var existingHeader = request.CHIT_ID.GetValueOrDefault() > 0
                ? (await GetInventoryHeadersPagedAsync(companyCd, inputType, normalizedChitType, request.CHIT_ID, null, null, null, 1, 1, databaseName)).Items.FirstOrDefault()
                : null;
            var oldData = existingHeader == null
                ? string.Empty
                : JsonSerializer.Serialize(new
                {
                    Header = existingHeader,
                    Inputs = await GetInventoryInputsByChitIdsAsync(companyCd, new[] { existingHeader.TRANSFER_ID }, databaseName),
                    Outputs = await GetInventoryOutputsByChitIdsAsync(companyCd, new[] { existingHeader.TRANSFER_ID }, databaseName)
                });

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company, databaseName);
            try
            {
                InventoryCogsDto? existingCogs = null;
                if (request.CHIT_ID.GetValueOrDefault() > 0 && existingHeader == null)
                    throw new InvalidOperationException($"Inventory voucher {request.CHIT_ID} does not exist.");
                var isCogsEnabled = normalizedChitType == "IO" && request.IS_COGS == "1";
                if (request.CHITDETAIL_ID_COGS is <= 0 || (!isCogsEnabled && request.CHITDETAIL_ID_COGS.HasValue))
                    throw new ArgumentException("A COGS detail link requires a positive detail ID and an IO with COGS enabled.");
                if (normalizedChitType == "IO")
                {
                    if (existingHeader != null)
                    {
                        await LockInventoryIssueAsync(session, companyCd, existingHeader.TRANSFER_ID);
                        if (existingHeader.CHIT_ID_COGS.HasValue)
                        {
                            await EnsureCogsPeriodOpenAsync(session, companyCd, existingHeader.TRANSFER_YMD);
                            existingCogs = await ReadInventoryCogsAsync(session, companyCd, existingHeader.TRANSFER_ID);
                            if (existingCogs == null)
                                throw new InvalidOperationException($"Issue voucher {existingHeader.TRANSFER_ID}: linked COGS voucher was not found.");
                            EnsureCogsUnlocked(existingCogs, existingHeader.TRANSFER_ID);
                            if (!existingHeader.CHITDETAIL_ID_COGS.HasValue)
                                throw new InvalidOperationException("Historical COGS link requires migration before editing.");
                            if (!isCogsEnabled && request.CLEAR_COGS_LINK != true)
                                throw new InvalidOperationException("Confirm clearing the linked OT COGS voucher before turning off IS_COGS.");
                        }
                    }

                    if (isCogsEnabled)
                        await EnsureCogsPeriodOpenAsync(session, companyCd, request.CHIT_YMD);
                }
                var activeInputs = Common.UsesInventoryInput(normalizedChitType)
                    ? (request.INPUTS ?? new List<InventoryInput>()).Where(IsValidInventoryInput).ToList()
                    : new List<InventoryInput>();
                var activeOutputs = Common.UsesInventoryOutput(normalizedChitType)
                    ? (request.OUTPUTS ?? new List<InventoryOutput>()).Where(IsValidInventoryOutput).ToList()
                    : new List<InventoryOutput>();

                var requestedInputs = Common.UsesInventoryInput(normalizedChitType)
                    ? (request.INPUTS ?? new List<InventoryInput>()).Where(x => x.ISDEL != "1").ToList()
                    : new List<InventoryInput>();
                var allRequestedOutputs = Common.UsesInventoryOutput(normalizedChitType)
                    ? (request.OUTPUTS ?? new List<InventoryOutput>()).Where(x => x.ISDEL != "1").ToList()
                    : new List<InventoryOutput>();
                if (requestedInputs.Count != activeInputs.Count || allRequestedOutputs.Count != activeOutputs.Count)
                    throw new ArgumentException("Mỗi dòng phải có mã hàng và mã kho hợp lệ.");
                var references = activeInputs.Select(x => (x.PRODUCT_ID, x.PRODUCT_CD, x.STORE_ID, x.STORE_CD))
                    .Concat(activeOutputs.Select(x => (x.PRODUCT_ID, x.PRODUCT_CD, x.STORE_ID, x.STORE_CD))).ToList();
                if (references.Count > 0)
                {
                    var productIds = references.Select(x => x.PRODUCT_ID).Distinct().ToArray();
                    var storeIds = references.Select(x => x.STORE_ID).Distinct().ToArray();
                    var products = (await session.QueryAsync<InventoryReference>(
                        "SELECT PRODUCT_ID AS Id, PRODUCT_CD AS Code FROM product_info WHERE COMPANY_CD = @companyCd AND COALESCE(ISDEL, '0') = '0' AND PRODUCT_ID IN @productIds",
                        new { companyCd, productIds })).ToDictionary(x => x.Id, x => x.Code);
                    var stores = (await session.QueryAsync<InventoryReference>(
                        "SELECT STORE_ID AS Id, STORE_CD AS Code FROM store_info WHERE COMPANY_CD = @companyCd AND COALESCE(ISDEL, '0') = '0' AND STORE_ID IN @storeIds",
                        new { companyCd, storeIds })).ToDictionary(x => x.Id, x => x.Code);
                    foreach (var line in references)
                    {
                        if (!products.TryGetValue(line.PRODUCT_ID!.Value, out var productCode) || !string.Equals(productCode.Trim(), line.PRODUCT_CD?.Trim(), StringComparison.OrdinalIgnoreCase))
                            throw new ArgumentException("Mã hàng không hợp lệ hoặc không thuộc công ty hiện tại.");
                        if (!stores.TryGetValue(line.STORE_ID!.Value, out var storeCode) || !string.Equals(storeCode.Trim(), line.STORE_CD?.Trim(), StringComparison.OrdinalIgnoreCase))
                            throw new ArgumentException("Mã kho không hợp lệ hoặc không thuộc công ty hiện tại.");
                    }
                }

                if (normalizedChitType == "IO")
                {
                    if (request.OUTPUTS == null)
                        throw new ArgumentException("OUTPUTS is required for inventory COGS.");
                    var requestedOutputs = request.OUTPUTS.Where(x => x.ISDEL != "1").ToList();
                    if (requestedOutputs.Count == 0 || requestedOutputs.Count != activeOutputs.Count)
                        throw new ArgumentException("Every issue output requires product and warehouse.");
                    if (activeOutputs.Any(x => x.AMOUNT_CC < 0 || x.UNIT_PRICE_CC < 0))
                        throw new ArgumentException("Issue amount and unit price cannot be negative.");
                    if (existingCogs != null)
                    {
                        if (existingCogs.DETAILS.Any(x => x.OUTPUT_ID > 0))
                        {
                            var ids = activeOutputs.Where(x => x.OUTPUT_ID > 0).Select(x => x.OUTPUT_ID!.Value).ToList();
                            if (ids.Distinct().Count() != ids.Count || ids.Any(id => !existingCogs.DETAILS.Any(x => x.OUTPUT_ID == id)))
                                throw new InvalidOperationException($"Issue voucher {request.CHIT_ID}: duplicate or foreign OUTPUT_ID.");
                        }
                    }
                }

                if (existingHeader == null)
                {
                    ResetCopiedInventoryInputLinesForCreate(activeInputs);
                    ResetCopiedInventoryOutputLinesForCreate(activeOutputs);
                }

                var inputAmount = activeInputs.Sum(item => item.AMOUNT_CC);
                // IO header AMOUNT always sums issue AMOUNT_TOTAL (independent of
                // IS_COGS); REVENUE_AMOUNT separately sums REVENUE_AMOUNT_TOTAL.
                // COGS accounting entries continue to use AMOUNT_CC.
                var outputAmount = activeOutputs.Sum(item => item.AMOUNT_TOTAL);
                var amount = normalizedChitType switch
                {
                    "IR" => inputAmount,
                    "IO" => outputAmount,
                    "IA" => Math.Max(inputAmount, outputAmount),
                    _ => 0
                };
                var totalQty = request.TOTAL_QTY ?? normalizedChitType switch
                {
                    "IR" => activeInputs.Sum(item => item.QUANTITY),
                    "IO" => activeOutputs.Sum(item => item.QUANTITY),
                    "IA" => activeOutputs.Sum(item => item.QUANTITY) > 0 ? activeOutputs.Sum(item => item.QUANTITY) : activeInputs.Sum(item => item.QUANTITY),
                    _ => 0
                };
                var voucherCd = ResolveInventoryVoucherCode(existingHeader, request);
                var voucherNo = await ResolveInventoryVoucherNoAsync(
                    session,
                    companyCd,
                    normalizedChitType,
                    existingHeader,
                    request);
                if (normalizedChitType == "IO" && string.IsNullOrWhiteSpace(voucherNo))
                    throw new ArgumentException("Issue voucher number is required for COGS.");
                var remark = Common.NormalizeNullableText(request.REMARK)
                    ?? Common.NormalizeNullableText(request.NOTE)
                    ?? existingHeader?.REMARK;
                const string upsertHeaderQuery = @"CALL setChitInventoryTransfer(
                    @p_TRANSFER_ID,
                    @p_COMPANY_CD,
                    @p_TRANSFER_CD,
                    @p_TRANSFER_NO,
                    @p_TRANSFER_YMD,
                    @p_CHIT_TYPE,
                    @p_INPUT_TYPE,
                    @p_IS_COGS,
                    @p_AMOUNT,
                    @p_REVENUE_AMOUNT,
                    @p_TOTAL_QTY,
                    @p_REMARK,
                    @p_CUSTOMER_ID,
                    @p_CUSTOMER_CD,
                    @p_SORT,
                    @p_USER
                )";

                var identity = await session.QuerySingleAsync<InventoryVoucherIdentity>(upsertHeaderQuery, new
                {
                    p_TRANSFER_ID = request.CHIT_ID,
                    p_COMPANY_CD = companyCd,
                    p_TRANSFER_CD = voucherCd,
                    p_TRANSFER_NO = voucherNo,
                    p_TRANSFER_YMD = request.CHIT_YMD,
                    p_CHIT_TYPE = normalizedChitType,
                    p_INPUT_TYPE = inputType,
                    p_IS_COGS = string.Equals(request.IS_COGS, "1", StringComparison.Ordinal) ? "1" : "0",
                    p_AMOUNT = normalizedChitType == "IO" ? outputAmount : request.AMOUNT ?? amount,
                    p_REVENUE_AMOUNT = normalizedChitType == "IO"
                        ? activeOutputs.Sum(x => x.REVENUE_AMOUNT_TOTAL)
                        : 0m,
                    p_TOTAL_QTY = totalQty,
                    p_REMARK = remark,
                    p_CUSTOMER_ID = request.CUSTOMER_ID,
                    p_CUSTOMER_CD = request.CUSTOMER_CD,
                    p_SORT = 0,
                    p_USER = userId
                });
                var voucherId = identity.TRANSFER_ID;
                var savedVoucherCd = Common.NormalizeNullableText(identity.TRANSFER_CD) ?? voucherCd ?? string.Empty;

                if (Common.UsesInventoryInput(normalizedChitType) || Common.UsesInventoryOutput(normalizedChitType))
                {
                    await EnsureUniqueInventorySourceLinesAsync(
                        session,
                        companyCd,
                        voucherId,
                        Common.UsesInventoryInput(normalizedChitType) ? activeInputs : Enumerable.Empty<InventoryInput>(),
                        Common.UsesInventoryOutput(normalizedChitType) ? activeOutputs : Enumerable.Empty<InventoryOutput>());
                }

                if (Common.UsesInventoryInput(normalizedChitType))
                {
                    var existingInputIds = await GetInventoryInputIdsByChitIdAsync(session, companyCd, voucherId);
                    var currentInputIds = new HashSet<long>();

                    for (var index = 0; index < activeInputs.Count; index++)
                    {
                        var input = activeInputs[index];
                        var savedInputId = await UpsertInventoryInputAsync(
                            session,
                            companyCd,
                            userId,
                            voucherId,
                            savedVoucherCd,
                            normalizedChitType,
                            input.SORT ?? index + 1,
                            input);
                        currentInputIds.Add(savedInputId);
                    }

                    foreach (var removedInputId in existingInputIds.Except(currentInputIds))
                    {
                        await SoftDeleteInventoryInputAsync(session, companyCd, userId, inputId: removedInputId);
                    }
                }
                else
                {
                    await SoftDeleteInventoryInputAsync(session, companyCd, userId, chitId: voucherId);
                }

                if (Common.UsesInventoryOutput(normalizedChitType))
                {
                    var existingOutputIds = await GetInventoryOutputIdsByChitIdAsync(session, companyCd, voucherId);
                    var currentOutputIds = new HashSet<long>();

                    for (var index = 0; index < activeOutputs.Count; index++)
                    {
                        var output = activeOutputs[index];
                        if (normalizedChitType == "IO") output.SORT = index + 1;
                        var savedOutputId = await UpsertInventoryOutputAsync(
                            session,
                            companyCd,
                            userId,
                            voucherId,
                            savedVoucherCd,
                            normalizedChitType,
                            output.SORT ?? index + 1,
                            output);
                        currentOutputIds.Add(savedOutputId);
                        output.OUTPUT_ID = savedOutputId;
                    }

                    foreach (var removedOutputId in existingOutputIds.Except(currentOutputIds))
                    {
                        await SoftDeleteInventoryOutputAsync(session, companyCd, userId, outputId: removedOutputId);
                    }
                }
                else
                {
                    await SoftDeleteInventoryOutputAsync(session, companyCd, userId, chitId: voucherId);
                }

                // Persist the snapshot only when explicitly supplied; omitted BOM preserves prior snapshot.
                // CLIENT_ROW_KEY follows each output through UpsertInventoryOutputAsync (no output schema change).
                if (normalizedChitType == "IO" && request.BOM != null)
                    await SaveBomSnapshotAsync(session, companyCd, userId, voucherId, request.BOM, activeOutputs);

                var savedCogs = isCogsEnabled
                    ? await SaveInventoryCogsAsync(session, companyCd, userId, voucherId, voucherNo!, request.CHIT_YMD!, activeOutputs, request.COGS_AMOUNT, existingCogs, request.CHITDETAIL_ID_COGS)
                    : null;
                if (!isCogsEnabled && existingCogs != null && request.CLEAR_COGS_LINK == true)
                    await UnlinkInventoryCogsAsync(session, companyCd, userId, voucherId, existingCogs.CHIT_ID);

                var newData = JsonSerializer.Serialize(new
                {
                    Header = new
                    {
                        COMPANY_CD = companyCd,
                        INPUT_TYPE = inputType,
                        TRANSFER_ID = voucherId,
                        TRANSFER_CD = savedVoucherCd,
                        TRANSFER_NO = voucherNo,
                        TRANSFER_YMD = request.CHIT_YMD,
                        CHIT_TYPE = normalizedChitType,
                        AMOUNT = request.AMOUNT ?? amount,
                        TOTAL_QTY = totalQty,
                        REMARK = remark,
                        CUSTOMER_ID = request.CUSTOMER_ID,
                        CUSTOMER_CD = request.CUSTOMER_CD,
                        UPDATE_BY = userId
                    },
                    Inputs = activeInputs,
                    Outputs = activeOutputs,
                    Cogs = savedCogs
                });

                await _activityLogService.LogAsync(
                    session.Connection,
                    session.Transaction,
                    companyCd,
                    existingHeader == null ? "INSERT" : "UPDATE",
                    "InventoryVoucher",
                    "chit_inventory_info",
                    savedVoucherCd.Length > 0 ? savedVoucherCd : voucherId.ToString(),
                    oldData,
                    newData,
                    $"Upsert inventory voucher ({inputType}/{normalizedChitType})");

                session.Commit();
                return voucherId;
            }
            catch (Exception ex)
            {
                session.Rollback();
                _logger.LogError(ex, "Failed to save inventory voucher {CompanyCd}/{InputType}/{ChitType}/{ChitId}", companyCd, inputType, normalizedChitType, request.CHIT_ID);
                throw;
            }
        }

        public async Task<int> DeleteInventoryVoucherAsync(string companyCd, string inputType, string chitType, long chitId, string userId)
        {
            var normalizedChitType = Common.NormalizeInventoryChitType(chitType);
            var existingHeader = (await GetInventoryHeadersPagedAsync(companyCd, inputType, normalizedChitType, chitId, null, null, null, 1, 1)).Items.FirstOrDefault();
            var oldData = existingHeader == null
                ? string.Empty
                : JsonSerializer.Serialize(new
                {
                    Header = existingHeader,
                    Inputs = await GetInventoryInputsByChitIdsAsync(companyCd, new[] { chitId }),
                    Outputs = await GetInventoryOutputsByChitIdsAsync(companyCd, new[] { chitId })
                });

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                if (normalizedChitType == "IO")
                {
                    await LockInventoryIssueAsync(session, companyCd, chitId);
                    if (existingHeader?.CHIT_ID_COGS is > 0)
                    {
                        var cogs = await ReadInventoryCogsAsync(session, companyCd, chitId);
                        if (cogs == null)
                            throw new InvalidOperationException($"Issue voucher {chitId}: linked COGS voucher was not found.");
                        EnsureCogsUnlocked(cogs, chitId);
                        await EnsureCogsPeriodOpenAsync(session, companyCd, cogs.CHIT_YMD);
                        await UnlinkInventoryCogsAsync(session, companyCd, userId, chitId, cogs.CHIT_ID);
                    }
                }
                const string query = "CALL delChitInventoryTransfer(@p_COMPANY_CD, @p_INPUT_TYPE, @p_TRANSFER_ID, @p_UPDATE_BY)";
                var result = await session.ExecuteAsync(query, new
                {
                    p_COMPANY_CD = companyCd,
                    p_INPUT_TYPE = inputType,
                    p_TRANSFER_ID = chitId,
                    p_UPDATE_BY = userId
                });

                await SoftDeleteInventoryInputAsync(session, companyCd, userId, chitId: chitId);
                await SoftDeleteInventoryOutputAsync(session, companyCd, userId, chitId: chitId);
                if (normalizedChitType == "IO")
                {
                    // Keep historical BOM allocation audit rows, but hide them with the deleted voucher.
                    // Install Database/Inventory/05_inventory_bom_snapshot.sql before deploying this change.
                    await session.ExecuteAsync(
                        @"UPDATE chit_inventory_bom_material SET ISDEL = '1', UPDATE_BY = @userId
                          WHERE COMPANY_CD = @companyCd AND INVENTORY_ID = @chitId AND ISDEL = '0'",
                        new { companyCd, chitId, userId });
                    await session.ExecuteAsync(
                        @"UPDATE chit_inventory_bom_finished SET ISDEL = '1', UPDATE_BY = @userId
                          WHERE COMPANY_CD = @companyCd AND INVENTORY_ID = @chitId AND ISDEL = '0'",
                        new { companyCd, chitId, userId });
                }

                if (result >= 0)
                {
                    await _activityLogService.LogAsync(
                        session.Connection,
                        session.Transaction,
                        companyCd,
                        "DELETE",
                        "InventoryVoucher",
                        "chit_inventory_info",
                        existingHeader?.TRANSFER_CD ?? chitId.ToString(),
                        oldData,
                        string.Empty,
                        $"Delete inventory voucher ({inputType}/{normalizedChitType})");
                }

                session.Commit();
                return result;
            }
            catch (Exception ex)
            {
                session.Rollback();
                _logger.LogError(ex, "Failed to delete inventory voucher {CompanyCd}/{InputType}/{ChitType}/{ChitId}", companyCd, inputType, normalizedChitType, chitId);
                throw;
            }
        }

        public async Task<bool> InventoryVoucherCdExistsAsync(string companyCd, string inputType, string chitCd, long? excludeChitId = null)
        {
            const string query = "CALL checkChitInventoryTransferCd(@p_COMPANY_CD, @p_TRANSFER_CD, @p_EXCLUDE_TRANSFER_ID)";
            var result = (await _db.QueryAsync<InventoryVoucherCdExistsResult>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_TRANSFER_CD = chitCd,
                p_EXCLUDE_TRANSFER_ID = excludeChitId
            })).FirstOrDefault();

            return result?.EXISTS_YN == 1;
        }

        private async Task<(IReadOnlyList<InventoryVoucherHeader> Items, int TotalRecords)> GetInventoryHeadersPagedAsync(
            string companyCd,
            string inputType,
            string chitType,
            long? transferId = null,
            string? searchText = null,
            string? fromYmd = null,
            string? toYmd = null,
            int pageNumber = 1,
            int pageSize = 20,
            string? databaseName = null)
        {
            const string query = "CALL getChitInventoryTransfer(@p_COMPANY_CD, @p_INPUT_TYPE, @p_CHIT_TYPE, @p_TRANSFER_ID, @p_SEARCH_TEXT, @p_FROM_YMD, @p_TO_YMD, @p_PAGE_NUMBER, @p_PAGE_SIZE)";

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company, databaseName);
            using var grid = await session.QueryMultipleAsync(query, new
            {
                p_COMPANY_CD = companyCd,
                p_INPUT_TYPE = inputType,
                p_CHIT_TYPE = chitType,
                p_TRANSFER_ID = transferId,
                p_SEARCH_TEXT = searchText,
                p_FROM_YMD = fromYmd,
                p_TO_YMD = toYmd,
                p_PAGE_NUMBER = pageNumber,
                p_PAGE_SIZE = pageSize
            });

            var items = (await grid.ReadAsync<InventoryVoucherHeader>()).ToList();
            var totalRow = (await grid.ReadAsync<TotalRecordResult>()).FirstOrDefault();

            return (items, totalRow?.TOTAL_RECORDS ?? items.Count);
        }

        private async Task<IReadOnlyList<InventoryInput>> GetInventoryInputsByChitIdsAsync(string companyCd, IEnumerable<long> chitIds, string? databaseName = null)
        {
            var ids = chitIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0)
            {
                return new List<InventoryInput>();
            }

            const string query = "CALL getChitInventoryInputByChitId(@p_COMPANY_CD, @p_CHIT_IDS)";
            var items = await _db.QueryAsync<InventoryInput>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_CHIT_IDS = string.Join(',', ids)
            }, databaseName);

            return items.ToList();
        }

        private async Task<IReadOnlyList<InventoryOutput>> GetInventoryOutputsByChitIdsAsync(string companyCd, IEnumerable<long> chitIds, string? databaseName = null)
        {
            var ids = chitIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0)
            {
                return new List<InventoryOutput>();
            }

            const string query = "CALL getChitInventoryOutputByChitId(@p_COMPANY_CD, @p_CHIT_IDS)";
            var items = await _db.QueryAsync<InventoryOutput>(Net_DB.Net_DB_Company, query, new
            {
                p_COMPANY_CD = companyCd,
                p_CHIT_IDS = string.Join(',', ids)
            }, databaseName);

            return items.ToList();
        }

        private static string ResolveInventoryVoucherCode(
            InventoryVoucherHeader? existingHeader,
            InventoryVoucherRequest request)
        {
            if (request.CHIT_ID.GetValueOrDefault() > 0)
            {
                return Common.NormalizeNullableText(existingHeader?.TRANSFER_CD) ?? Common.GenerateKeyCd("C");
            }

            return Common.GenerateKeyCd("C");
        }

        private async Task<string?> ResolveInventoryVoucherNoAsync(
            DapperSession session,
            string companyCd,
            string chitType,
            InventoryVoucherHeader? existingHeader,
            InventoryVoucherRequest request)
        {
            var requestedNo = Common.NormalizeNullableText(request.CHIT_NO);

            if (request.CHIT_ID.GetValueOrDefault() > 0)
            {
                var existingNo = Common.NormalizeNullableText(existingHeader?.TRANSFER_NO);
                if (string.Equals(requestedNo, existingNo, StringComparison.OrdinalIgnoreCase))
                {
                    return existingHeader?.TRANSFER_NO;
                }
            }

            var resolvedNo = await CodeSequenceHelper.ResolveRequiredCodeAsync(
                _sequenceRepository,
                session,
                companyCd,
                chitType,
                requestedNo,
                "Inventory voucher no",
                Common.ParseNullableDateTimeText(request.CHIT_YMD));

            return Common.NormalizeNullableText(resolvedNo) ?? requestedNo ?? existingHeader?.TRANSFER_NO;
        }

        private static InventoryVoucherDto MapInventoryVoucher(
            InventoryVoucherHeader item,
            IReadOnlyDictionary<long, List<InventoryInput>> inputLookup,
            IReadOnlyDictionary<long, List<InventoryOutput>> outputLookup)
        {
            return new InventoryVoucherDto
            {
                CHIT_ID = item.TRANSFER_ID,
                COMPANY_CD = item.COMPANY_CD,
                INPUT_TYPE = item.INPUT_TYPE,
                CHIT_CD = item.TRANSFER_CD,
                CHIT_NO = item.TRANSFER_NO,
                CHIT_YMD = item.TRANSFER_YMD,
                CHIT_TYPE = Common.NormalizeInventoryChitType(item.CHIT_TYPE),
                IS_COGS = item.IS_COGS,
                CHITDETAIL_ID_COGS = item.CHITDETAIL_ID_COGS,
                AMOUNT = item.AMOUNT,
                REVENUE_AMOUNT = item.REVENUE_AMOUNT,
                TOTAL_QTY = item.TOTAL_QTY,
                REMARK = item.REMARK,
                CUSTOMER_ID = item.CUSTOMER_ID,
                CUSTOMER_CD = item.CUSTOMER_CD,
                PAYER_INFO = null,
                ISDEL = item.ISDEL,
                IS_LOCK = "0",
                ISEXCEL = "0",
                EMAIL_EPAY = null,
                IS_CONFIRMED = "0",
                NOTE = item.REMARK,
                DAY_OF_PAYMENT = null,
                TIME_FOR_PAYMENT = null,
                IS_PAYMENT = "0",
                CHIT_CD_COGS = null,
                DESCRIPTION_VIET = item.REMARK,
                DESCRIPTION_ENG = null,
                DESCRIPTION_KOR = null,
                INPUTS = inputLookup.TryGetValue(item.TRANSFER_ID, out var inputs) ? inputs : new List<InventoryInput>(),
                OUTPUTS = outputLookup.TryGetValue(item.TRANSFER_ID, out var outputs) ? outputs : new List<InventoryOutput>()
            };
        }

        private static bool IsValidInventoryInput(InventoryInput? input)
        {
            if (input == null || string.Equals(input.ISDEL, "1", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return (input.PRODUCT_ID ?? 0) > 0
                && (input.STORE_ID ?? 0) > 0;
        }

        private static bool IsValidInventoryOutput(InventoryOutput? output)
        {
            if (output == null || string.Equals(output.ISDEL, "1", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return (output.PRODUCT_ID ?? 0) > 0
                && (output.STORE_ID ?? 0) > 0;
        }

        private static void ResetCopiedInventoryInputLinesForCreate(IEnumerable<InventoryInput> inputs)
        {
            foreach (var input in inputs.Where(item => item.INPUT_ID.GetValueOrDefault() > 0))
            {
                input.INPUT_ID = null;
                input.INPUT_CD = string.Empty;
                input.INVENTORY_ID = null;
                input.INVENTORY_CD = string.Empty;
                input.CREATE_BY = string.Empty;
                input.CREATE_AT = null;
                input.UPDATE_BY = string.Empty;
                input.UPDATE_AT = null;
            }
        }

        private static void ResetCopiedInventoryOutputLinesForCreate(IEnumerable<InventoryOutput> outputs)
        {
            foreach (var output in outputs.Where(item => item.OUTPUT_ID.GetValueOrDefault() > 0))
            {
                output.OUTPUT_ID = null;
                output.OUTPUT_CD = string.Empty;
                output.INVENTORY_ID = null;
                output.INVENTORY_CD = string.Empty;
                output.CREATE_BY = string.Empty;
                output.CREATE_AT = null;
                output.UPDATE_BY = string.Empty;
                output.UPDATE_AT = null;
            }
        }

        private static async Task<HashSet<long>> GetInventoryInputIdsByChitIdAsync(
            DapperSession session,
            string companyCd,
            long chitId)
        {
            if (chitId <= 0)
            {
                return new HashSet<long>();
            }

            var ids = await session.QueryAsync<long>(
                "CALL getChitInventoryInputByChitId(@p_COMPANY_CD, @p_CHIT_IDS)",
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_CHIT_IDS = chitId.ToString()
                });

            return ids.Where(id => id > 0).ToHashSet();
        }

        private static async Task<HashSet<long>> GetInventoryOutputIdsByChitIdAsync(
            DapperSession session,
            string companyCd,
            long chitId)
        {
            if (chitId <= 0)
            {
                return new HashSet<long>();
            }

            var ids = await session.QueryAsync<long>(
                "CALL getChitInventoryOutputByChitId(@p_COMPANY_CD, @p_CHIT_IDS)",
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_CHIT_IDS = chitId.ToString()
                });

            return ids.Where(id => id > 0).ToHashSet();
        }

        private static Task SoftDeleteInventoryInputAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long? inputId = null,
            long? chitId = null)
        {
            return session.ExecuteAsync(
                "CALL delChitInventoryInput(@p_COMPANY_CD, @p_INPUT_ID, @p_INVENTORY_ID, @p_USER)",
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_INPUT_ID = inputId > 0 ? inputId : null,
                    p_INVENTORY_ID = chitId > 0 ? chitId : null,
                    p_USER = userId
                });
        }

        private static Task SoftDeleteInventoryOutputAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long? outputId = null,
            long? chitId = null)
        {
            return session.ExecuteAsync(
                "CALL delChitInventoryOutput(@p_COMPANY_CD, @p_OUTPUT_ID, @p_INVENTORY_ID, @p_USER)",
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_OUTPUT_ID = outputId > 0 ? outputId : null,
                    p_INVENTORY_ID = chitId > 0 ? chitId : null,
                    p_USER = userId
                });
        }

        private static Task<long> UpsertInventoryInputAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long chitId,
            string? chitCd,
            string? chitType,
            int? fallbackSort,
            InventoryInput input)
        {
            var inventoryChitId = chitId > 0 ? (long?)chitId : input.INVENTORY_ID.GetValueOrDefault() > 0 ? input.INVENTORY_ID : null;
            var inventoryChitCd = Common.NormalizeNullableText(chitCd) ?? Common.NormalizeNullableText(input.INVENTORY_CD);
            var inventoryChitType = Common.NormalizeReferenceChitType(chitType) ?? Common.NormalizeReferenceChitType(input.CHIT_TYPE);
            var sourceDetailId = input.CHITDETAIL_ID.GetValueOrDefault() > 0 ? input.CHITDETAIL_ID : null;
            var sourceDetailCd = Common.NormalizeNullableText(input.CHITDETAIL_CD);

            return session.QuerySingleAsync<long>(
                "CALL setChitInventoryInput(@p_INPUT_ID, @p_INPUT_CD, @p_INVENTORY_ID, @p_INVENTORY_CD, @p_CHIT_TYPE, @p_COMPANY_CD, @p_PRODUCT_ID, @p_PRODUCT_CD, @p_STORE_ID, @p_STORE_CD, @p_UNIT_ID, @p_UNIT_CD, @p_QUANTITY, @p_UNIT_PRICE_CC, @p_FC_TYPE, @p_UNIT_PRICE_FC, @p_EXCHANGE_RATES, @p_AMOUNT_CC, @p_AMOUNT_TOTAL, @p_AMOUNT_FC, @p_SUMMARY, @p_INVENTORY_YMD, @p_STATE, @p_CHITDETAIL_ID, @p_CHITDETAIL_CD, @p_SORT, @p_USER)",
                new
                {
                    p_INPUT_ID = input.INPUT_ID > 0 ? input.INPUT_ID : (long?)null,
                    p_INPUT_CD = input.INPUT_CD,
                    p_INVENTORY_ID = inventoryChitId,
                    p_INVENTORY_CD = string.IsNullOrWhiteSpace(inventoryChitCd) ? null : inventoryChitCd,
                    p_CHIT_TYPE = string.IsNullOrWhiteSpace(inventoryChitType) ? null : inventoryChitType,
                    p_COMPANY_CD = companyCd,
                    p_PRODUCT_ID = input.PRODUCT_ID,
                    p_PRODUCT_CD = input.PRODUCT_CD,
                    p_STORE_ID = input.STORE_ID,
                    p_STORE_CD = input.STORE_CD,
                    p_UNIT_ID = input.UNIT_ID,
                    p_UNIT_CD = input.UNIT_CD,
                    p_QUANTITY = input.QUANTITY,
                    p_UNIT_PRICE_CC = input.UNIT_PRICE_CC,
                    p_FC_TYPE = string.IsNullOrWhiteSpace(input.FC_TYPE) ? "VND" : input.FC_TYPE,
                    p_UNIT_PRICE_FC = input.UNIT_PRICE_FC,
                    p_EXCHANGE_RATES = input.EXCHANGE_RATES,
                    p_AMOUNT_CC = input.AMOUNT_CC,
                    p_AMOUNT_TOTAL = input.AMOUNT_TOTAL != 0 ? input.AMOUNT_TOTAL : input.AMOUNT_CC,
                    p_AMOUNT_FC = input.AMOUNT_FC,
                    p_SUMMARY = input.SUMMARY,
                    p_INVENTORY_YMD = input.INVENTORY_YMD,
                    p_STATE = string.IsNullOrWhiteSpace(input.STATE) ? "1" : input.STATE,
                    p_CHITDETAIL_ID = sourceDetailId,
                    p_CHITDETAIL_CD = string.IsNullOrWhiteSpace(sourceDetailCd) ? null : sourceDetailCd,
                    p_SORT = input.SORT.GetValueOrDefault() > 0 ? input.SORT : fallbackSort ?? 0,
                    p_USER = userId
                });
        }

        private static Task<long> UpsertInventoryOutputAsync(
            DapperSession session,
            string companyCd,
            string userId,
            long chitId,
            string? chitCd,
            string? chitType,
            int? fallbackSort,
            InventoryOutput output)
        {
            var inventoryChitId = chitId > 0 ? (long?)chitId : output.INVENTORY_ID.GetValueOrDefault() > 0 ? output.INVENTORY_ID : null;
            var inventoryChitCd = Common.NormalizeNullableText(chitCd) ?? Common.NormalizeNullableText(output.INVENTORY_CD);
            var inventoryChitType = Common.NormalizeReferenceChitType(chitType) ?? Common.NormalizeReferenceChitType(output.CHIT_TYPE);
            var sourceDetailId = output.CHITDETAIL_ID.GetValueOrDefault() > 0 ? output.CHITDETAIL_ID : null;
            var sourceDetailCd = Common.NormalizeNullableText(output.CHITDETAIL_CD);

            return session.QuerySingleAsync<long>(
                "CALL setChitInventoryOutput(@p_OUTPUT_ID, @p_OUTPUT_CD, @p_INVENTORY_ID, @p_INVENTORY_CD, @p_CHIT_TYPE, @p_COMPANY_CD, @p_PRODUCT_ID, @p_PRODUCT_CD, @p_STORE_ID, @p_STORE_CD, @p_UNIT_ID, @p_UNIT_CD, @p_QUANTITY, @p_UNIT_PRICE_CC, @p_REVENUE_UNIT_PRICE_CC, @p_FC_TYPE, @p_UNIT_PRICE_FC, @p_REVENUE_UNIT_PRICE_FC, @p_EXCHANGE_RATES, @p_AMOUNT_CC, @p_AMOUNT_TOTAL, @p_REVENUE_AMOUNT_CC, @p_AMOUNT_FC, @p_REVENUE_AMOUNT_FC, @p_REVENUE_AMOUNT_TOTAL, @p_SUMMARY, @p_INVENTORY_YMD, @p_STATE, @p_CHITDETAIL_ID, @p_CHITDETAIL_CD, @p_SORT, @p_USER)",
                new
                {
                    p_OUTPUT_ID = output.OUTPUT_ID > 0 ? output.OUTPUT_ID : (long?)null,
                    p_OUTPUT_CD = output.OUTPUT_CD,
                    p_INVENTORY_ID = inventoryChitId,
                    p_INVENTORY_CD = string.IsNullOrWhiteSpace(inventoryChitCd) ? null : inventoryChitCd,
                    p_CHIT_TYPE = string.IsNullOrWhiteSpace(inventoryChitType) ? null : inventoryChitType,
                    p_COMPANY_CD = companyCd,
                    p_PRODUCT_ID = output.PRODUCT_ID,
                    p_PRODUCT_CD = output.PRODUCT_CD,
                    p_STORE_ID = output.STORE_ID,
                    p_STORE_CD = output.STORE_CD,
                    p_UNIT_ID = output.UNIT_ID,
                    p_UNIT_CD = output.UNIT_CD,
                    p_QUANTITY = output.QUANTITY,
                    p_UNIT_PRICE_CC = output.UNIT_PRICE_CC,
                    p_REVENUE_UNIT_PRICE_CC = output.REVENUE_UNIT_PRICE_CC,
                    p_FC_TYPE = string.IsNullOrWhiteSpace(output.FC_TYPE) ? "VND" : output.FC_TYPE,
                    p_UNIT_PRICE_FC = output.UNIT_PRICE_FC,
                    p_REVENUE_UNIT_PRICE_FC = output.REVENUE_UNIT_PRICE_FC,
                    p_EXCHANGE_RATES = output.EXCHANGE_RATES,
                    p_AMOUNT_CC = output.AMOUNT_CC,
                    p_AMOUNT_TOTAL = output.AMOUNT_TOTAL != 0 ? output.AMOUNT_TOTAL : output.AMOUNT_CC,
                    p_REVENUE_AMOUNT_CC = output.REVENUE_AMOUNT_CC,
                    p_AMOUNT_FC = output.AMOUNT_FC,
                    p_REVENUE_AMOUNT_FC = output.REVENUE_AMOUNT_FC,
                    p_REVENUE_AMOUNT_TOTAL = output.REVENUE_AMOUNT_TOTAL,
                    p_SUMMARY = output.SUMMARY,
                    p_INVENTORY_YMD = output.INVENTORY_YMD,
                    p_STATE = string.IsNullOrWhiteSpace(output.STATE) ? "1" : output.STATE,
                    p_CHITDETAIL_ID = sourceDetailId,
                    p_CHITDETAIL_CD = string.IsNullOrWhiteSpace(sourceDetailCd) ? null : sourceDetailCd,
                    p_SORT = output.SORT.GetValueOrDefault() > 0 ? output.SORT : fallbackSort ?? 0,
                    p_USER = userId
                });
        }

        private static async Task EnsureUniqueInventorySourceLinesAsync(
            DapperSession session,
            string companyCd,
            long currentInventoryChitId,
            IEnumerable<InventoryInput> inputs,
            IEnumerable<InventoryOutput> outputs)
        {
            var inputDetailIds = inputs
                .Where(IsValidInventoryInput)
                .Select(item => item.CHITDETAIL_ID.GetValueOrDefault())
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            var outputDetailIds = outputs
                .Where(IsValidInventoryOutput)
                .Select(item => item.CHITDETAIL_ID.GetValueOrDefault())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (inputDetailIds.Count == 0 && outputDetailIds.Count == 0)
            {
                return;
            }

            using var grid = await session.QueryMultipleAsync(
                "CALL getInventorySourceConflicts(@p_COMPANY_CD, @p_INPUT_CHITDETAIL_IDS, @p_OUTPUT_CHITDETAIL_IDS)",
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_INPUT_CHITDETAIL_IDS = string.Join(",", inputDetailIds),
                    p_OUTPUT_CHITDETAIL_IDS = string.Join(",", outputDetailIds)
                });

            var inputRows = (await grid.ReadAsync<InventoryInputVoucherRow>()).ToList();
            var outputRows = (await grid.ReadAsync<InventoryOutputVoucherRow>()).ToList();

            var inputConflicts = inputRows
                .Where(item => item.INPUT_ID > 0 && item.CHITDETAIL_ID > 0 && item.INVENTORY_ID > 0 && item.INVENTORY_ID != currentInventoryChitId)
                .Select(item => item.CHITDETAIL_ID)
                .Distinct()
                .OrderBy(id => id)
                .ToList();
            if (inputConflicts.Count > 0)
            {
                throw new InvalidOperationException($"Chi tiet nguon da duoc lien ket voi phieu nhap kho khac: {string.Join(", ", inputConflicts)}");
            }

            var outputConflicts = outputRows
                .Where(item => item.OUTPUT_ID > 0 && item.CHITDETAIL_ID > 0 && item.INVENTORY_ID > 0 && item.INVENTORY_ID != currentInventoryChitId)
                .Select(item => item.CHITDETAIL_ID)
                .Distinct()
                .OrderBy(id => id)
                .ToList();
            if (outputConflicts.Count > 0)
            {
                throw new InvalidOperationException($"Chi tiet nguon da duoc lien ket voi phieu xuat kho khac: {string.Join(", ", outputConflicts)}");
            }
        }
    }
}
