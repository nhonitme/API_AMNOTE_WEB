using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.BackgroundJobs.EInvoiceMessage;
using API_AMNOTE_WEB.Transmission;

namespace API_AMNOTE_WEB.Repositories
{
    public class EInvoiceMessageRepository : IEInvoiceMessageRepository
    {
        private sealed class MttOutboxRow
        {
            public string SIGNED_XML { get; set; } = "";
            public string USER_ID { get; set; } = "";
            public long INVOICE_ID { get; set; }
        }
        public async Task DispatchMttOutboxAsync(string dbName, string companyCd, string? batchId = null)
        {
            var ids = batchId == null ? await _db.QueryAsync<string>(Net_DB.Net_DB_Company,
                "CALL getEInvoiceMttOutbox(@companyCd)", new { companyCd }, sDBName: dbName) : new[] { batchId };
            foreach (var id in ids)
            {
                await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company, dbName);
                try
                {
                    var row = await session.QueryFirstOrDefaultAsync<MttOutboxRow>("CALL lockEInvoiceMttOutbox(@companyCd,@id)", new { companyCd, id });
                    if (row != null)
                    {
                        if (!await SendExistsAsync(companyCd, id))
                        {
                            var header = System.Xml.Linq.XDocument.Parse(row.SIGNED_XML).Root!.Element("TTChung")!;
                            string Field(string name) => header.Element(name)!.Value;
                            await EnqueueOutboundAsync(companyCd, row.USER_ID, EInvoiceMessageTargetTypes.Invoice, row.INVOICE_ID,
                                new EInvoicePackagedMessage(row.SIGNED_XML,Field("PBan"),Field("MNGui"),Field("MNNhan"),Field("MLTDiep"),id,"",Field("MST"),int.Parse(Field("SLuong"))));
                        }
                        await session.ExecuteAsync("CALL setEInvoiceMttQueued(@companyCd,@id)", new { companyCd, id });
                    }
                    session.Commit();
                }
                catch { session.Rollback(); throw; }
            }
        }
        public async Task<bool> ApplyMttReceiveAsync(string dbName, string companyCd, string userId, string mtdiep, string xml)
        {
            var receive = MessageReceiveParser.TryParseMtt(xml);
            if (receive == null) return false;

            var result = await _db.QueryAsync<int>(Net_DB.Net_DB_Company,
                "CALL setEInvoiceMttReceive(@companyCd,@mtdiep,@accepted,@error,@userId)",
                new
                {
                    companyCd,
                    mtdiep,
                    accepted = receive.Accepted ? 1 : 0,
                    error = receive.Error,
                    userId
                },
                sDBName: dbName);
            return result.FirstOrDefault() > 0;
        }
        private const string InsertSendQuery = @"CALL setEInvoiceMessageSend(
            @p_COMPANY_CD,
            @p_INVOICE_ID,
            @p_PBAN,
            @p_MNGUI,
            @p_MNNHAN,
            @p_MLTDIEP,
            @p_MTDIEP,
            @p_MTDTCHIEU,
            @p_MST,
            @p_SLUONG,
            @p_REQUEST_XML,
            @p_ERROR_MESSAGE,
            @p_USERID
        )";

        private const string InsertWorkQueueQuery = @"CALL setEInvoiceMessageWorkQueue(
            @p_COMPANY_CD,
            @p_DB_NAME,
            @p_MST,
            @p_TARGET_TYPE,
            @p_TARGET_ID,
            @p_SEND_MTDIEP,
            @p_WORK_STATUS,
            @p_ERROR_MESSAGE
        )";

        private const string InsertReceiveQuery = @"CALL setEInvoiceMessageReceive(
            @p_COMPANY_CD,
            @p_PBAN,
            @p_MNGUI,
            @p_MNNHAN,
            @p_MLTDIEP,
            @p_MTDIEP,
            @p_MTDTCHIEU,
            @p_MST,
            @p_SLUONG,
            @p_RESPONSE_XML,
            @p_ERROR_MESSAGE,
            @p_USERID
        )";

        private const string GetPendingWorkQueueQuery = "CALL getEInvoiceMessageWorkQueuePending(@p_LIMIT)";

        private const string GetPendingSendWorkQueueQuery = "CALL getEInvoiceMessageWorkQueuePendingSend(@p_LIMIT)";

        private const string UpdateWorkQueueStatusQuery = @"CALL setEInvoiceMessageWorkQueueStatus(
            @p_COMPANY_CD,
            @p_SEND_MTDIEP,
            @p_WORK_STATUS,
            @p_ERROR_MESSAGE
        )";

        private const string ReceiveExistsQuery = "CALL existsEInvoiceMessageReceive(@p_COMPANY_CD, @p_MTDIEP)";

        private const string SendExistsQuery = "CALL existsEInvoiceMessageSend(@p_COMPANY_CD, @p_MTDIEP, @p_INVOICE_ID)";

        private const string ReceiveTypeExistsQuery = "CALL existsEInvoiceMessageReceiveType(@p_COMPANY_CD, @p_MTDTCHIEU, @p_MLTDIEP)";

        private const string GetReceiveMessagesByLookupCodeQuery = "CALL getEInvoiceMessageReceiveByLookupCode(@p_COMPANY_CD, @p_MTRA_CUU)";

        private const string GetReceiveMessageByIdQuery = "CALL getEInvoiceMessageReceiveById(@p_COMPANY_CD, @p_RECEIVE_ID)";

        private const string GetSendMessageByMtdiepQuery = "CALL getEInvoiceMessageSendByMtdiep(@p_COMPANY_CD, @p_MTDIEP)";

        private const string ApplyReceiveXmlQuery = @"CALL setEInvoiceInfoReceiveXml(
            @p_COMPANY_CD,
            @p_INVOICE_ID,
            @p_XML_FTP_PATH,
            @p_MCCQT,
            @p_INVOICE_STATUS,
            @p_USERID
        )";

        private const string ApplyReceiveErrorQuery = @"CALL setEInvoiceInfoReceiveErrorMessage(
            @p_COMPANY_CD,
            @p_INVOICE_ID,
            @p_ERROR_MESSAGE,
            @p_INVOICE_STATUS,
            @p_USERID
        )";

        private const string ApplyDeclarationReceiveXmlQuery = @"CALL setEInvoiceTkhaiReceiveXml(
            @p_COMPANY_CD,
            @p_TKHAI_ID,
            @p_XML,
            @p_CQT_STATUS,
            @p_MCCQT,
            @p_USERID
        )";

        private const string ApplyDeclarationReceiveErrorQuery = @"CALL setEInvoiceTkhaiReceiveErrorMessage(
            @p_COMPANY_CD,
            @p_TKHAI_ID,
            @p_ERROR_MESSAGE,
            @p_CQT_STATUS,
            @p_USERID
        )";

        private const string ApplyDeclarationCqtStatusQuery = @"CALL setEInvoiceTkhaiCqtStatus(
            @p_COMPANY_CD,
            @p_TKHAI_ID,
            @p_CQT_STATUS,
            @p_ERROR_MESSAGE,
            @p_USERID
        )";

        private const string ApplyErrorNoticeReceiveXmlQuery = @"CALL setEInvoiceTbaoReceiveXml(
            @p_COMPANY_CD,
            @p_TBAO_ID,
            @p_XML,
            @p_USERID
        )";

        private const string ApplyErrorNoticeReceiveErrorQuery = @"CALL setEInvoiceTbaoReceiveErrorMessage(
            @p_COMPANY_CD,
            @p_TBAO_ID,
            @p_ERROR_MESSAGE,
            @p_USERID
        )";

        private const string ApplyInvoiceMgdDTuQuery = @"CALL setEInvoiceInfoMgdDTu(
            @p_COMPANY_CD,
            @p_INVOICE_ID,
            @p_MGDDTu,
            @p_USERID
        )";

        private const string ApplyDeclarationMgdDTuQuery = @"CALL setEInvoiceTkhaiMgdDTu(
            @p_COMPANY_CD,
            @p_TKHAI_ID,
            @p_MGDDTU,
            @p_USERID
        )";

        private const string ApplyErrorNoticeMgdDTuQuery = @"CALL setEInvoiceTbaoMgdDTu(
            @p_COMPANY_CD,
            @p_TBAO_ID,
            @p_MGDDTU,
            @p_USERID
        )";

        private readonly DapperExecutor _db;
        private readonly ICompanyDatabaseResolver _companyDatabaseResolver;
        private readonly IEInvoiceXmlStorageService _xmlStorageService;
        private readonly IEInvoiceMessageWorkSignal _workSignal;
        private readonly IMasterDataCacheService _cache;

        public EInvoiceMessageRepository(
            DapperExecutor db,
            ICompanyDatabaseResolver companyDatabaseResolver,
            IEInvoiceXmlStorageService xmlStorageService,
            IEInvoiceMessageWorkSignal workSignal,
            IMasterDataCacheService cache)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _companyDatabaseResolver = companyDatabaseResolver ?? throw new ArgumentNullException(nameof(companyDatabaseResolver));
            _xmlStorageService = xmlStorageService ?? throw new ArgumentNullException(nameof(xmlStorageService));
            _workSignal = workSignal ?? throw new ArgumentNullException(nameof(workSignal));
            _cache = cache;
        }

        public async Task EnqueueOutboundAsync(
            string companyCd,
            string userId,
            string targetType,
            long? targetId,
            EInvoicePackagedMessage packagedMessage)
        {
            var dbName = await _companyDatabaseResolver.ResolveDatabaseNameAsync(companyCd);
            var normalizedTargetType = EInvoiceMessageTargetTypes.Normalize(targetType);

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Manager);
            try
            {
                await session.QuerySingleAsync<long>(InsertSendQuery, new
                {
                    p_COMPANY_CD = companyCd,
                    p_INVOICE_ID = EInvoiceMessageTargetTypes.IsDeclaration(normalizedTargetType)
                        || API_AMNOTE_WEB.TaxWithholding.PitKinds.IsTarget(normalizedTargetType)
                        || EInvoiceMessageTargetTypes.IsErrorNotice(normalizedTargetType)
                        ? null
                        : targetId,
                    p_PBAN = packagedMessage.PBAN,
                    p_MNGUI = packagedMessage.MNGUI,
                    p_MNNHAN = packagedMessage.MNNHAN,
                    p_MLTDIEP = packagedMessage.MLTDIEP,
                    p_MTDIEP = packagedMessage.MTDIEP,
                    p_MTDTCHIEU = packagedMessage.MTDTCHIEU,
                    p_MST = packagedMessage.MST,
                    p_SLUONG = packagedMessage.SLUONG,
                    p_REQUEST_XML = packagedMessage.RequestXml,
                    p_ERROR_MESSAGE = string.Empty,
                    p_USERID = userId
                });

                await session.QuerySingleAsync<long>(InsertWorkQueueQuery, new
                {
                    p_COMPANY_CD = companyCd,
                    p_DB_NAME = dbName,
                    p_MST = packagedMessage.MST,
                    p_TARGET_TYPE = normalizedTargetType,
                    p_TARGET_ID = targetId,
                    p_SEND_MTDIEP = packagedMessage.MTDIEP,
                    p_WORK_STATUS = "WAIT_SEND",
                    p_ERROR_MESSAGE = string.Empty
                });

                session.Commit();
                _workSignal.NotifyWorkAvailable();
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<long> InsertReceiveAsync(string companyCd, string userId, EInvoiceMessageReceiveRequest request)
        {
            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Manager);
            try
            {
                var receiveId = await session.QuerySingleAsync<long>(InsertReceiveQuery, new
                {
                    p_COMPANY_CD = companyCd,
                    p_PBAN = request.PBAN,
                    p_MNGUI = request.MNGUI,
                    p_MNNHAN = request.MNNHAN,
                    p_MLTDIEP = request.MLTDIEP,
                    p_MTDIEP = request.MTDIEP,
                    p_MTDTCHIEU = request.MTDTCHIEU,
                    p_MST = request.MST,
                    p_SLUONG = request.SLUONG,
                    p_RESPONSE_XML = request.RESPONSE_XML,
                    p_ERROR_MESSAGE = request.ERROR_MESSAGE,
                    p_USERID = userId
                });

                session.Commit();
                return receiveId;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<IReadOnlyList<EInvoiceMessageWorkQueueItem>> GetPendingWorkQueueAsync(int limit)
        {
            var rows = await _db.QueryAsync<EInvoiceMessageWorkQueueItem>(
                Net_DB.Net_DB_Manager,
                GetPendingWorkQueueQuery,
                new { p_LIMIT = limit });
            return rows.ToList();
        }

        public async Task<IReadOnlyList<EInvoiceMessagePendingSendItem>> GetPendingSendWorkQueueAsync(int limit)
        {
            var rows = await _db.QueryAsync<EInvoiceMessagePendingSendItem>(
                Net_DB.Net_DB_Manager,
                GetPendingSendWorkQueueQuery,
                new { p_LIMIT = limit });
            return rows.ToList();
        }

        public Task UpdateWorkQueueStatusAsync(string companyCd, string sendMtdiep, string workStatus, string? errorMessage)
        {
            return _db.ExecuteAsync(
                Net_DB.Net_DB_Manager,
                UpdateWorkQueueStatusQuery,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_SEND_MTDIEP = sendMtdiep,
                    p_WORK_STATUS = workStatus,
                    p_ERROR_MESSAGE = errorMessage ?? string.Empty
                });
        }

        public async Task<bool> ReceiveExistsAsync(string companyCd, string mtdiep)
        {
            if (string.IsNullOrWhiteSpace(companyCd) || string.IsNullOrWhiteSpace(mtdiep))
            {
                return false;
            }

            var count = await _db.QuerySingleAsync<int>(
                Net_DB.Net_DB_Manager,
                ReceiveExistsQuery,
                new
                {
                    p_COMPANY_CD = companyCd.Trim(),
                    p_MTDIEP = mtdiep.Trim()
                });

            return count > 0;
        }

        public async Task<bool> SendExistsAsync(string companyCd, string mtdiep, long? invoiceId = null)
        {
            if (string.IsNullOrWhiteSpace(companyCd))
            {
                return false;
            }

            var normalizedMtdiep = Common.NormalizeNullableText(mtdiep);
            if (string.IsNullOrWhiteSpace(normalizedMtdiep) && (invoiceId is null or <= 0))
            {
                return false;
            }

            var count = await _db.QuerySingleAsync<int>(
                Net_DB.Net_DB_Manager,
                SendExistsQuery,
                new
                {
                    p_COMPANY_CD = companyCd.Trim(),
                    p_MTDIEP = normalizedMtdiep ?? string.Empty,
                    p_INVOICE_ID = invoiceId
                });

            return count > 0;
        }

        public async Task<bool> ReceiveTypeExistsAsync(string companyCd, string mtdtchieu, string mltdiep)
        {
            if (string.IsNullOrWhiteSpace(companyCd)
                || string.IsNullOrWhiteSpace(mtdtchieu)
                || string.IsNullOrWhiteSpace(mltdiep))
            {
                return false;
            }

            var count = await _db.QuerySingleAsync<int>(
                Net_DB.Net_DB_Manager,
                ReceiveTypeExistsQuery,
                new
                {
                    p_COMPANY_CD = companyCd.Trim(),
                    p_MTDTCHIEU = mtdtchieu.Trim(),
                    p_MLTDIEP = mltdiep.Trim()
                });

            return count > 0;
        }

        public async Task<IReadOnlyList<EInvoiceMessageReceiveInfo>> GetReceiveMessagesByLookupCodeAsync(string companyCd, string mtraCuu)
        {
            if (string.IsNullOrWhiteSpace(companyCd) || string.IsNullOrWhiteSpace(mtraCuu))
            {
                return Array.Empty<EInvoiceMessageReceiveInfo>();
            }

            var rows = await _db.QueryAsync<EInvoiceMessageReceiveInfo>(
                Net_DB.Net_DB_Manager,
                GetReceiveMessagesByLookupCodeQuery,
                new
                {
                    p_COMPANY_CD = companyCd.Trim(),
                    p_MTRA_CUU = mtraCuu.Trim()
                });

            var messages = rows.ToList();
            foreach (var message in messages)
            {
                if (EInvoiceMessageTypeCodes.TryGet(message.MLTDIEP, out var info))
                {
                    message.MLTDIEP_NAME = info.Name;
                }
            }

            return messages;
        }

        public async Task<EInvoiceMessageReceiveInfo?> GetReceiveMessageByIdAsync(string companyCd, long receiveId)
        {
            if (string.IsNullOrWhiteSpace(companyCd) || receiveId <= 0)
            {
                return null;
            }

            var rows = await _db.QueryAsync<EInvoiceMessageReceiveInfo>(
                Net_DB.Net_DB_Manager,
                GetReceiveMessageByIdQuery,
                new
                {
                    p_COMPANY_CD = companyCd.Trim(),
                    p_RECEIVE_ID = receiveId
                });

            var message = rows.FirstOrDefault();
            if (message != null && EInvoiceMessageTypeCodes.TryGet(message.MLTDIEP, out var info))
            {
                message.MLTDIEP_NAME = info.Name;
            }

            return message;
        }

        public async Task<EInvoiceMessageSendInfo?> GetSendMessageByMtdiepAsync(string companyCd, string mtdiep)
        {
            var normalizedMtdiep = Common.NormalizeNullableText(mtdiep);
            if (string.IsNullOrWhiteSpace(companyCd) || string.IsNullOrWhiteSpace(normalizedMtdiep))
            {
                return null;
            }

            var rows = await _db.QueryAsync<EInvoiceMessageSendInfo>(
                Net_DB.Net_DB_Manager,
                GetSendMessageByMtdiepQuery,
                new
                {
                    p_COMPANY_CD = companyCd.Trim(),
                    p_MTDIEP = normalizedMtdiep,
                });

            var message = rows.FirstOrDefault();
            if (message != null && EInvoiceMessageTypeCodes.TryGet(message.MLTDIEP, out var info))
            {
                message.MLTDIEP_NAME = info.Name;
            }

            return message;
        }

        public async Task ApplyReceiveXmlToInvoiceAsync(
            string dbName,
            string companyCd,
            string userId,
            long invoiceId,
            string xml,
            string? mccqt = null,
            int? invoiceStatus = null)
        {
            if (string.IsNullOrWhiteSpace(dbName))
            {
                throw new ArgumentException("DB_NAME is required", nameof(dbName));
            }

            var xmlFtpPath = await _xmlStorageService.UploadInvoiceXmlAsync(companyCd, invoiceId, xml);

            await _db.ExecuteAsync(
                Net_DB.Net_DB_Company,
                ApplyReceiveXmlQuery,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_INVOICE_ID = invoiceId,
                    p_XML_FTP_PATH = xmlFtpPath,
                    p_MCCQT = mccqt,
                    p_INVOICE_STATUS = invoiceStatus,
                    p_USERID = userId
                },
                sDBName: dbName);
        }

        public Task ApplyReceiveErrorToInvoiceAsync(
            string dbName,
            string companyCd,
            string userId,
            long invoiceId,
            string errorMessage,
            int? invoiceStatus = null)
        {
            if (string.IsNullOrWhiteSpace(dbName))
            {
                throw new ArgumentException("DB_NAME is required", nameof(dbName));
            }

            return _db.ExecuteAsync(
                Net_DB.Net_DB_Company,
                ApplyReceiveErrorQuery,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_INVOICE_ID = invoiceId,
                    p_ERROR_MESSAGE = errorMessage,
                    p_INVOICE_STATUS = invoiceStatus ?? EInvoiceInvoiceStatusCodes.CodeError,
                    p_USERID = userId
                },
                sDBName: dbName);
        }

        public async Task ApplyReceiveXmlToDeclarationAsync(string dbName, string companyCd, string userId, long tkhaiId, string xml, int? cqtStatus = null, string? mccqt = null)
        {
            if (string.IsNullOrWhiteSpace(dbName))
            {
                throw new ArgumentException("DB_NAME is required", nameof(dbName));
            }

            await _db.ExecuteAsync(
                Net_DB.Net_DB_Company,
                ApplyDeclarationReceiveXmlQuery,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_TKHAI_ID = tkhaiId,
                    p_XML = xml,
                    p_CQT_STATUS = cqtStatus,
                    p_MCCQT = mccqt,
                    p_USERID = userId
                },
                sDBName: dbName);
            await _cache.ClearAsync("einvoice-seller", companyCd);
        }

        public Task ApplyReceiveErrorToDeclarationAsync(string dbName, string companyCd, string userId, long tkhaiId, string errorMessage, int? cqtStatus = null)
        {
            if (string.IsNullOrWhiteSpace(dbName))
            {
                throw new ArgumentException("DB_NAME is required", nameof(dbName));
            }

            return _db.ExecuteAsync(
                Net_DB.Net_DB_Company,
                ApplyDeclarationReceiveErrorQuery,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_TKHAI_ID = tkhaiId,
                    p_ERROR_MESSAGE = errorMessage,
                    p_CQT_STATUS = cqtStatus ?? EInvoiceDeclarationCqtStatus.Rejected,
                    p_USERID = userId
                },
                sDBName: dbName);
        }

        public Task ApplyCqtStatusToDeclarationAsync(string dbName, string companyCd, string userId, long tkhaiId, int cqtStatus, string? errorMessage = null)
        {
            if (string.IsNullOrWhiteSpace(dbName))
            {
                throw new ArgumentException("DB_NAME is required", nameof(dbName));
            }

            return _db.ExecuteAsync(
                Net_DB.Net_DB_Company,
                ApplyDeclarationCqtStatusQuery,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_TKHAI_ID = tkhaiId,
                    p_CQT_STATUS = cqtStatus,
                    p_ERROR_MESSAGE = errorMessage,
                    p_USERID = userId
                },
                sDBName: dbName);
        }

        public Task ApplyReceiveXmlToErrorNoticeAsync(string dbName, string companyCd, string userId, long tbaoId, string xml)
        {
            if (string.IsNullOrWhiteSpace(dbName))
            {
                throw new ArgumentException("DB_NAME is required", nameof(dbName));
            }

            return _db.ExecuteAsync(
                Net_DB.Net_DB_Company,
                ApplyErrorNoticeReceiveXmlQuery,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_TBAO_ID = tbaoId,
                    p_XML = xml,
                    p_USERID = userId
                },
                sDBName: dbName);
        }

        public Task ApplyReceiveErrorToErrorNoticeAsync(string dbName, string companyCd, string userId, long tbaoId, string? errorMessage)
        {
            if (string.IsNullOrWhiteSpace(dbName))
            {
                throw new ArgumentException("DB_NAME is required", nameof(dbName));
            }

            return _db.ExecuteAsync(
                Net_DB.Net_DB_Company,
                ApplyErrorNoticeReceiveErrorQuery,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_TBAO_ID = tbaoId,
                    p_ERROR_MESSAGE = errorMessage ?? string.Empty,
                    p_USERID = userId
                },
                sDBName: dbName);
        }

        public Task ApplyMgdDTuToTargetAsync(string dbName, string companyCd, string userId, string targetType, long targetId, string mgdDTu)
        {
            if (API_AMNOTE_WEB.TaxWithholding.PitKinds.IsTarget(targetType))
                return _db.ExecuteAsync(Net_DB.Net_DB_Company,"CALL setPitMgdDTu(@companyCd,@targetType,@targetId,@mgdDTu)",new{companyCd,targetType,targetId,mgdDTu},sDBName:dbName);
            if (string.IsNullOrWhiteSpace(dbName))
            {
                throw new ArgumentException("DB_NAME is required", nameof(dbName));
            }

            if (string.IsNullOrWhiteSpace(mgdDTu))
            {
                throw new ArgumentException("MGDDTU is required", nameof(mgdDTu));
            }

            var normalizedTargetType = EInvoiceMessageTargetTypes.Normalize(targetType);
            if (EInvoiceMessageTargetTypes.IsDeclaration(normalizedTargetType))
            {
                return _db.ExecuteAsync(
                    Net_DB.Net_DB_Company,
                    ApplyDeclarationMgdDTuQuery,
                    new
                    {
                        p_COMPANY_CD = companyCd,
                        p_TKHAI_ID = targetId,
                        p_MGDDTU = mgdDTu.Trim(),
                        p_USERID = userId
                    },
                    sDBName: dbName);
            }

            if (EInvoiceMessageTargetTypes.IsErrorNotice(normalizedTargetType))
            {
                return _db.ExecuteAsync(
                    Net_DB.Net_DB_Company,
                    ApplyErrorNoticeMgdDTuQuery,
                    new
                    {
                        p_COMPANY_CD = companyCd,
                        p_TBAO_ID = targetId,
                        p_MGDDTU = mgdDTu.Trim(),
                        p_USERID = userId
                    },
                    sDBName: dbName);
            }

            return _db.ExecuteAsync(
                Net_DB.Net_DB_Company,
                ApplyInvoiceMgdDTuQuery,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_INVOICE_ID = targetId,
                    p_MGDDTu = mgdDTu.Trim(),
                    p_USERID = userId
                },
                sDBName: dbName);
        }

        public Task ApplyPitSendStatusAsync(string dbName, string companyCd, string targetType, long targetId, int status, string? errorMessage)
        {
            if (!API_AMNOTE_WEB.TaxWithholding.PitKinds.IsTarget(targetType))
                throw new ArgumentException("PIT target is required", nameof(targetType));
            return _db.ExecuteAsync(
                Net_DB.Net_DB_Company,
                "CALL setPitSendStatus(@companyCd,@targetType,@targetId,@status,@errorMessage)",
                new { companyCd, targetType, targetId, status, errorMessage = errorMessage ?? string.Empty },
                sDBName: dbName);
        }
    }
}
