using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services;
using API_AMNOTE_WEB.Transmission;
using Microsoft.Extensions.Options;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.EInvoiceMessage
{
    public sealed class EInvoiceMessagePollingBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly EInvoiceGatewayOptions _options;
        private readonly IViettelEInvoiceTokenProvider _tokenProvider;
        private readonly IEInvoiceMessageWorkSignal _workSignal;
        private readonly ILogger<EInvoiceMessagePollingBackgroundService> _logger;

        public EInvoiceMessagePollingBackgroundService(
            IServiceScopeFactory scopeFactory,
            IOptions<EInvoiceGatewayOptions> options,
            IViettelEInvoiceTokenProvider tokenProvider,
            IEInvoiceMessageWorkSignal workSignal,
            ILogger<EInvoiceMessagePollingBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _tokenProvider = tokenProvider;
            _workSignal = workSignal;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
            {
                _logger.LogInformation("E-invoice message polling is disabled.");
                return;
            }

            _logger.LogInformation(
                "E-invoice message worker started (wake-on-work). PollIntervalSeconds={PollIntervalSeconds}, TokenRefreshMinutes={TokenRefreshMinutes}, SendConcurrency={SendConcurrency}, ReceiveConcurrency={ReceiveConcurrency}, PendingBatchSize={PendingBatchSize}",
                _options.PollIntervalSeconds,
                _options.TokenRefreshMinutes,
                Math.Max(1, _options.SendConcurrency),
                Math.Max(1, _options.ReceiveConcurrency),
                Math.Max(1, _options.PendingBatchSize));

            await ResetStuckWorkAsync(stoppingToken);

            // Drain leftover queue rows after process restart.
            _workSignal.NotifyWorkAvailable();

            var pollInterval = TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _workSignal.WaitAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                await DrainWorkQueuesAsync(pollInterval, stoppingToken);
            }
        }

        private async Task ResetStuckWorkAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var messageRepository = scope.ServiceProvider.GetRequiredService<IEInvoiceMessageRepository>();
                await messageRepository.ResetStuckWorkQueueAsync();
                stoppingToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "E-invoice work queue stuck reset failed; continuing with wake-on-work.");
            }
        }

        private async Task DrainWorkQueuesAsync(TimeSpan pollInterval, CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var hadWork = false;

                try
                {
                    var accessToken = await _tokenProvider.TryGetAccessTokenAsync(stoppingToken);
                    if (string.IsNullOrWhiteSpace(accessToken))
                    {
                        // Still probe queues so we can idle when empty even if login is down.
                        hadWork = await HasPendingWorkAsync(stoppingToken);
                        if (hadWork)
                        {
                            _logger.LogWarning(
                                "Viettel e-invoice login unavailable. Worker will retry while pending work remains.");
                        }
                    }
                    else
                    {
                        var hadSend = await ProcessPendingSendAsync(stoppingToken);
                        var hadReceive = await ProcessPendingReceiveAsync(stoppingToken);
                        hadWork = hadSend || hadReceive;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "E-invoice message polling cycle failed.");
                    hadWork = true;
                }

                if (!hadWork)
                {
                    _logger.LogDebug("E-invoice work queues empty. Worker idle until next enqueue.");
                    return;
                }

                try
                {
                    await Task.Delay(pollInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private async Task<bool> HasPendingWorkAsync(CancellationToken stoppingToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var messageRepository = scope.ServiceProvider.GetRequiredService<IEInvoiceMessageRepository>();

            var pendingSend = await messageRepository.GetPendingSendWorkQueueAsync(_options.PendingBatchSize);
            if (pendingSend.Count > 0)
            {
                return true;
            }

            stoppingToken.ThrowIfCancellationRequested();

            var pendingReceive = await messageRepository.GetPendingWorkQueueAsync(_options.PendingBatchSize);
            return pendingReceive.Count > 0;
        }

        private async Task<bool> ProcessPendingSendAsync(CancellationToken stoppingToken)
        {
            using var claimScope = _scopeFactory.CreateScope();
            var claimRepository = claimScope.ServiceProvider.GetRequiredService<IEInvoiceMessageRepository>();

            var pendingItems = await claimRepository.ClaimPendingSendWorkQueueAsync(_options.PendingBatchSize);
            if (pendingItems.Count == 0)
            {
                return false;
            }

            var concurrency = Math.Max(1, _options.SendConcurrency);
            using var gate = new SemaphoreSlim(concurrency, concurrency);

            var tasks = pendingItems.Select(item => ProcessSendItemWithScopeAsync(item, gate, stoppingToken));
            await Task.WhenAll(tasks);

            return true;
        }

        private async Task ProcessSendItemWithScopeAsync(
            EInvoiceMessagePendingSendItem item,
            SemaphoreSlim gate,
            CancellationToken stoppingToken)
        {
            await gate.WaitAsync(stoppingToken);
            try
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    await ReleaseClaimAsync(item.COMPANY_CD, item.SEND_MTDIEP, "WAIT_SEND", null);
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var messageRepository = scope.ServiceProvider.GetRequiredService<IEInvoiceMessageRepository>();
                var gatewayClient = scope.ServiceProvider.GetRequiredService<IViettelEInvoiceGatewayClient>();
                await ProcessSendWorkQueueItemAsync(messageRepository, gatewayClient, item, stoppingToken);
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<bool> ProcessPendingReceiveAsync(CancellationToken stoppingToken)
        {
            using var claimScope = _scopeFactory.CreateScope();
            var claimRepository = claimScope.ServiceProvider.GetRequiredService<IEInvoiceMessageRepository>();

            var pendingItems = await claimRepository.ClaimPendingReceiveWorkQueueAsync(_options.PendingBatchSize);
            if (pendingItems.Count == 0)
            {
                return false;
            }

            var concurrency = Math.Max(1, _options.ReceiveConcurrency);
            using var gate = new SemaphoreSlim(concurrency, concurrency);

            var tasks = pendingItems.Select(item => ProcessReceiveItemWithScopeAsync(item, gate, stoppingToken));
            await Task.WhenAll(tasks);

            return true;
        }

        private async Task ProcessReceiveItemWithScopeAsync(
            EInvoiceMessageWorkQueueItem item,
            SemaphoreSlim gate,
            CancellationToken stoppingToken)
        {
            await gate.WaitAsync(stoppingToken);
            try
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    await ReleaseClaimAsync(item.COMPANY_CD, item.SEND_MTDIEP, "WAIT_RESPONSE", null);
                    return;
                }

                using var scope = _scopeFactory.CreateScope();
                var messageRepository = scope.ServiceProvider.GetRequiredService<IEInvoiceMessageRepository>();
                var gatewayClient = scope.ServiceProvider.GetRequiredService<IViettelEInvoiceGatewayClient>();
                var receiveApplyCoordinator = scope.ServiceProvider.GetRequiredService<MessageReceiveApplyCoordinator>();
                await ProcessReceiveWorkQueueItemAsync(
                    messageRepository,
                    gatewayClient,
                    receiveApplyCoordinator,
                    item,
                    stoppingToken);
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task ReleaseClaimAsync(
            string companyCd,
            string sendMtdiep,
            string workStatus,
            string? errorMessage)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var messageRepository = scope.ServiceProvider.GetRequiredService<IEInvoiceMessageRepository>();
                await messageRepository.UpdateWorkQueueStatusAsync(companyCd, sendMtdiep, workStatus, errorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "E-invoice claim release failed. Company={CompanyCd}, SendMtdiep={SendMtdiep}, Status={WorkStatus}",
                    companyCd,
                    sendMtdiep,
                    workStatus);
            }
        }

        private async Task ProcessSendWorkQueueItemAsync(
            IEInvoiceMessageRepository messageRepository,
            IViettelEInvoiceGatewayClient gatewayClient,
            EInvoiceMessagePendingSendItem item,
            CancellationToken stoppingToken)
        {
            try
            {
                var sendResult = await gatewayClient.SendDvgpRequestAsync(item.REQUEST_XML, stoppingToken);
                if (!sendResult.IsSuccess)
                {
                    await messageRepository.UpdateWorkQueueStatusAsync(
                        item.COMPANY_CD,
                        item.SEND_MTDIEP,
                        "WAIT_SEND",
                        TruncateError(sendResult.ResponseBody));

                    await TryApplyDeclarationSendFailureAsync(messageRepository, item, TruncateError(sendResult.ResponseBody));
                    await TryApplyErrorNoticeSendFailureAsync(messageRepository, item, TruncateError(sendResult.ResponseBody));
                    await TryApplyPitSendStatusAsync(messageRepository, item, 4, TruncateError(sendResult.ResponseBody));

                    _logger.LogWarning(
                        "E-invoice send failed. Company={CompanyCd}, SendMtdiep={SendMtdiep}, Status={StatusCode}",
                        item.COMPANY_CD,
                        item.SEND_MTDIEP,
                        sendResult.StatusCode);
                    return;
                }

                await messageRepository.UpdateWorkQueueStatusAsync(
                    item.COMPANY_CD,
                    item.SEND_MTDIEP,
                    "WAIT_RESPONSE",
                    null);

                await TryApplyDeclarationSentAsync(messageRepository, item);
                await TryApplyPitSendStatusAsync(messageRepository, item, 1, null);

                _logger.LogInformation(
                    "E-invoice REQUEST_XML sent. Company={CompanyCd}, SendMtdiep={SendMtdiep}",
                    item.COMPANY_CD,
                    item.SEND_MTDIEP);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                await messageRepository.UpdateWorkQueueStatusAsync(
                    item.COMPANY_CD,
                    item.SEND_MTDIEP,
                    "WAIT_SEND",
                    null);
                throw;
            }
            catch (Exception ex)
            {
                await messageRepository.UpdateWorkQueueStatusAsync(
                    item.COMPANY_CD,
                    item.SEND_MTDIEP,
                    "WAIT_SEND",
                    TruncateError(ex.Message));

                await TryApplyDeclarationSendFailureAsync(messageRepository, item, TruncateError(ex.Message));
                await TryApplyErrorNoticeSendFailureAsync(messageRepository, item, TruncateError(ex.Message));
                await TryApplyPitSendStatusAsync(messageRepository, item, 4, TruncateError(ex.Message));

                _logger.LogWarning(
                    ex,
                    "E-invoice send failed. Company={CompanyCd}, SendMtdiep={SendMtdiep}",
                    item.COMPANY_CD,
                    item.SEND_MTDIEP);
            }
        }

        private async Task ProcessReceiveWorkQueueItemAsync(
            IEInvoiceMessageRepository messageRepository,
            IViettelEInvoiceGatewayClient gatewayClient,
            MessageReceiveApplyCoordinator receiveApplyCoordinator,
            EInvoiceMessageWorkQueueItem item,
            CancellationToken stoppingToken)
        {
            try
            {
                var lookupMessages = await gatewayClient.LookupMessagesAsync(item.MST, item.SEND_MTDIEP, stoppingToken);
                if (lookupMessages.Count == 0)
                {
                    await messageRepository.UpdateWorkQueueStatusAsync(
                        item.COMPANY_CD,
                        item.SEND_MTDIEP,
                        "WAIT_RESPONSE",
                        null);
                    return;
                }

                var validMessages = lookupMessages
                    .Where(lookup => !string.IsNullOrWhiteSpace(lookup.DLieu))
                    .ToList();

                if (validMessages.Count == 0)
                {
                    await messageRepository.UpdateWorkQueueStatusAsync(
                        item.COMPANY_CD,
                        item.SEND_MTDIEP,
                        "WAIT_RESPONSE",
                        null);
                    return;
                }

                var targetType = EInvoiceMessageTargetTypes.Normalize(item.TARGET_TYPE);
                var isDeclaration = EInvoiceMessageTargetTypes.IsDeclaration(targetType);
                var isErrorNotice = EInvoiceMessageTargetTypes.IsErrorNotice(targetType);

                var preferSuccessResult = isDeclaration
                    ? await ShouldPreferDeclarationAcceptAsync(messageRepository, item, validMessages)
                    : isErrorNotice
                        ? await ShouldPreferErrorNoticeProcessAsync(messageRepository, item, validMessages)
                        : await ShouldPreferInvoiceTaxCodeAsync(messageRepository, item, validMessages);

                var savedCount = 0;
                var terminalReceived = false;
                var receiveErrorMessage = validMessages
                    .Select(lookup => MessageReceiveParser
                        .TryParse(targetType, lookup.MLTDIEP, lookup.DLieu)
                        ?.Error)
                    .FirstOrDefault(error => !string.IsNullOrWhiteSpace(error));

                foreach (var lookup in validMessages)
                {
                    var receiveMtdiep = string.IsNullOrWhiteSpace(lookup.MTDIEP) ? item.SEND_MTDIEP : lookup.MTDIEP;
                    var receiveMtdtchieu = GatewayMessageMatcher.ResolveReference(lookup, item.SEND_MTDIEP);
                    var alreadyExists = await messageRepository.ReceiveExistsAsync(item.COMPANY_CD, receiveMtdiep);

                    if (alreadyExists)
                    {
                        terminalReceived = TrackTerminalMessage(
                            targetType,
                            lookup,
                            terminalReceived);
                        continue;
                    }

                    try
                    {
                        await messageRepository.InsertReceiveAsync(
                            item.COMPANY_CD,
                            _options.SystemUserId,
                            new EInvoiceMessageReceiveRequest
                            {
                                PBAN = lookup.PBAN,
                                MNGUI = lookup.MNGUI,
                                MNNHAN = lookup.MNNHAN,
                                MLTDIEP = lookup.MLTDIEP,
                                MTDIEP = receiveMtdiep,
                                MTDTCHIEU = receiveMtdtchieu,
                                MST = string.IsNullOrWhiteSpace(lookup.MST) ? item.MST : lookup.MST,
                                SLUONG = lookup.SLUONG,
                                RESPONSE_XML = lookup.DLieu,
                                ERROR_MESSAGE = MessageReceiveParser
                                    .TryParse(targetType, lookup.MLTDIEP, lookup.DLieu)
                                    ?.Error ?? string.Empty
                            });

                        await TryApplyMgdDTuFromReceiveAsync(
                            messageRepository,
                            item,
                            targetType,
                            lookup,
                            receiveMtdiep);

                        savedCount++;
                        terminalReceived = TrackTerminalMessage(
                            targetType,
                            lookup,
                            terminalReceived);
                    }
                    catch (Exception insertEx)
                    {
                        _logger.LogWarning(
                            insertEx,
                            "E-invoice receive insert failed. Company={CompanyCd}, SendMtdiep={SendMtdiep}, ReceiveMtdiep={ReceiveMtdiep}, Mltdiep={Mltdiep}",
                            item.COMPANY_CD,
                            item.SEND_MTDIEP,
                            receiveMtdiep,
                            lookup.MLTDIEP);
                    }
                }

                if (item.TARGET_ID.HasValue && !string.IsNullOrWhiteSpace(item.DB_NAME))
                {
                    await receiveApplyCoordinator.ApplyAsync(item, validMessages, preferSuccessResult);
                }

                if (!terminalReceived)
                {
                    await messageRepository.UpdateWorkQueueStatusAsync(
                        item.COMPANY_CD,
                        item.SEND_MTDIEP,
                        "WAIT_RESPONSE",
                        null);
                    return;
                }

                await messageRepository.UpdateWorkQueueStatusAsync(
                    item.COMPANY_CD,
                    item.SEND_MTDIEP,
                    "RECEIVED",
                    receiveErrorMessage);

                _logger.LogInformation(
                    "E-invoice response saved. Company={CompanyCd}, SendMtdiep={SendMtdiep}, SavedCount={SavedCount}",
                    item.COMPANY_CD,
                    item.SEND_MTDIEP,
                    savedCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                await messageRepository.UpdateWorkQueueStatusAsync(
                    item.COMPANY_CD,
                    item.SEND_MTDIEP,
                    "WAIT_RESPONSE",
                    null);
                throw;
            }
            catch (Exception ex)
            {
                await messageRepository.UpdateWorkQueueStatusAsync(
                    item.COMPANY_CD,
                    item.SEND_MTDIEP,
                    "WAIT_RESPONSE",
                    TruncateError(ex.Message));

                _logger.LogWarning(
                    ex,
                    "E-invoice response polling failed. Company={CompanyCd}, SendMtdiep={SendMtdiep}",
                    item.COMPANY_CD,
                    item.SEND_MTDIEP);
            }
        }

        private static string TruncateError(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= 1000 ? trimmed : trimmed[..1000];
        }

        private static async Task<bool> ShouldPreferInvoiceTaxCodeAsync(
            IEInvoiceMessageRepository messageRepository,
            EInvoiceMessageWorkQueueItem item,
            IReadOnlyList<ViettelEInvoiceMessageLookupResult> validMessages)
        {
            var hasTaxCodeResultInBatch = validMessages
                .Any(lookup => EInvoiceMessageTypeCodes.IsInvoiceTaxCodeResult(lookup.MLTDIEP));

            if (hasTaxCodeResultInBatch)
            {
                return true;
            }

            return await messageRepository.ReceiveTypeExistsAsync(
                item.COMPANY_CD,
                item.SEND_MTDIEP,
                EInvoiceMessageTypeCodes.InvoiceTaxCodeResult);
        }

        private static async Task<bool> ShouldPreferDeclarationAcceptAsync(
            IEInvoiceMessageRepository messageRepository,
            EInvoiceMessageWorkQueueItem item,
            IReadOnlyList<ViettelEInvoiceMessageLookupResult> validMessages)
        {
            var hasAcceptInBatch = validMessages
                .Any(lookup => EInvoiceMessageTypeCodes.IsDeclarationAcceptNotice(lookup.MLTDIEP));

            if (hasAcceptInBatch)
            {
                return true;
            }

            return await messageRepository.ReceiveTypeExistsAsync(
                item.COMPANY_CD,
                item.SEND_MTDIEP,
                EInvoiceMessageTypeCodes.DeclarationAcceptNotice);
        }

        private static async Task<bool> ShouldPreferErrorNoticeProcessAsync(
            IEInvoiceMessageRepository messageRepository,
            EInvoiceMessageWorkQueueItem item,
            IReadOnlyList<ViettelEInvoiceMessageLookupResult> validMessages)
        {
            var hasProcessResultInBatch = validMessages
                .Any(lookup => EInvoiceMessageTypeCodes.IsInvoiceErrorProcessResult(lookup.MLTDIEP));

            if (hasProcessResultInBatch)
            {
                return true;
            }

            return await messageRepository.ReceiveTypeExistsAsync(
                item.COMPANY_CD,
                item.SEND_MTDIEP,
                EInvoiceMessageTypeCodes.InvoiceErrorProcessResult);
        }

        private static bool TrackTerminalMessage(
            string targetType,
            ViettelEInvoiceMessageLookupResult lookup,
            bool terminalReceived)
        {
            var outcome = MessageReceiveParser.TryParse(targetType, lookup.MLTDIEP, lookup.DLieu);
            return terminalReceived || outcome?.Terminal == true;
        }

        private async Task TryApplyDeclarationSentAsync(
            IEInvoiceMessageRepository messageRepository,
            EInvoiceMessagePendingSendItem item)
        {
            if (!EInvoiceMessageTargetTypes.IsDeclaration(item.TARGET_TYPE)
                || !item.TARGET_ID.HasValue
                || string.IsNullOrWhiteSpace(item.DB_NAME))
            {
                return;
            }

            await messageRepository.ApplyCqtStatusToDeclarationAsync(
                item.DB_NAME,
                item.COMPANY_CD,
                _options.SystemUserId,
                item.TARGET_ID.Value,
                EInvoiceDeclarationCqtStatus.SentWaiting);
        }

        private async Task TryApplyDeclarationSendFailureAsync(
            IEInvoiceMessageRepository messageRepository,
            EInvoiceMessagePendingSendItem item,
            string? errorMessage)
        {
            if (!EInvoiceMessageTargetTypes.IsDeclaration(item.TARGET_TYPE)
                || !item.TARGET_ID.HasValue
                || string.IsNullOrWhiteSpace(item.DB_NAME))
            {
                return;
            }

            await messageRepository.ApplyCqtStatusToDeclarationAsync(
                item.DB_NAME,
                item.COMPANY_CD,
                _options.SystemUserId,
                item.TARGET_ID.Value,
                EInvoiceDeclarationCqtStatus.SendError,
                errorMessage);
        }

        private async Task TryApplyMgdDTuFromReceiveAsync(
            IEInvoiceMessageRepository messageRepository,
            EInvoiceMessageWorkQueueItem item,
            string targetType,
            ViettelEInvoiceMessageLookupResult lookup,
            string receiveMtdiep)
        {
            if (!item.TARGET_ID.HasValue
                || string.IsNullOrWhiteSpace(item.DB_NAME)
                || !GatewayMessageMatcher.ShouldAssignTransactionCode(lookup))
            {
                return;
            }

            await messageRepository.ApplyMgdDTuToTargetAsync(
                item.DB_NAME,
                item.COMPANY_CD,
                _options.SystemUserId,
                targetType,
                item.TARGET_ID.Value,
                receiveMtdiep);

            _logger.LogInformation(
                "E-invoice MGDDTU updated from gateway send echo. Company={CompanyCd}, SendMtdiep={SendMtdiep}, TargetType={TargetType}, TargetId={TargetId}, MgdDTu={MgdDTu}, Mltdiep={Mltdiep}, Mngui={Mngui}",
                item.COMPANY_CD,
                item.SEND_MTDIEP,
                targetType,
                item.TARGET_ID,
                receiveMtdiep,
                lookup.MLTDIEP,
                lookup.MNGUI);
        }

        private async Task TryApplyErrorNoticeSendFailureAsync(
            IEInvoiceMessageRepository messageRepository,
            EInvoiceMessagePendingSendItem item,
            string? errorMessage)
        {
            if (!EInvoiceMessageTargetTypes.IsErrorNotice(item.TARGET_TYPE)
                || !item.TARGET_ID.HasValue
                || string.IsNullOrWhiteSpace(item.DB_NAME))
            {
                return;
            }

            await messageRepository.ApplyReceiveErrorToErrorNoticeAsync(
                item.DB_NAME,
                item.COMPANY_CD,
                _options.SystemUserId,
                item.TARGET_ID.Value,
                errorMessage);
        }

        private static Task TryApplyPitSendStatusAsync(
            IEInvoiceMessageRepository messageRepository,
            EInvoiceMessagePendingSendItem item,
            int status,
            string? errorMessage)
        {
            var targetType = EInvoiceMessageTargetTypes.Normalize(item.TARGET_TYPE);
            if (!API_AMNOTE_WEB.TaxWithholding.PitKinds.IsTarget(targetType)
                || !item.TARGET_ID.HasValue
                || string.IsNullOrWhiteSpace(item.DB_NAME))
                return Task.CompletedTask;

            return messageRepository.ApplyPitSendStatusAsync(
                item.DB_NAME,
                item.COMPANY_CD,
                targetType,
                item.TARGET_ID.Value,
                status,
                errorMessage);
        }

    }
}
