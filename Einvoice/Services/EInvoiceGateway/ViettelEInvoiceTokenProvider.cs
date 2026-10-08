using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace API_AMNOTE_WEB.Services.EInvoiceGateway
{
    public sealed class ViettelEInvoiceTokenProvider : IViettelEInvoiceTokenProvider
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly EInvoiceGatewayOptions _options;
        private readonly ILogger<ViettelEInvoiceTokenProvider> _logger;
        private readonly SemaphoreSlim _sync = new(1, 1);

        private string? _accessToken;
        private DateTime _nextRefreshAtUtc = DateTime.MinValue;

        public ViettelEInvoiceTokenProvider(
            IHttpClientFactory httpClientFactory,
            IOptions<EInvoiceGatewayOptions> options,
            ILogger<ViettelEInvoiceTokenProvider> logger)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
            _logger = logger;
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_options.Username)
            && !string.IsNullOrWhiteSpace(_options.Password);

        public async Task<string?> TryGetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            await _sync.WaitAsync(cancellationToken);
            try
            {
                if (NeedsRefresh())
                {
                    var loggedIn = await TryLoginAsync(cancellationToken);
                    if (!loggedIn)
                    {
                        _accessToken = null;
                        return null;
                    }
                }

                return _accessToken;
            }
            finally
            {
                _sync.Release();
            }
        }

        public async Task InvalidateAsync(CancellationToken cancellationToken = default)
        {
            await _sync.WaitAsync(cancellationToken);
            try
            {
                _accessToken = null;
                _nextRefreshAtUtc = DateTime.MinValue;
            }
            finally
            {
                _sync.Release();
            }
        }

        private bool NeedsRefresh()
        {
            return string.IsNullOrWhiteSpace(_accessToken)
                || DateTime.UtcNow >= _nextRefreshAtUtc;
        }

        private async Task<bool> TryLoginAsync(CancellationToken cancellationToken)
        {
            if (!IsConfigured)
            {
                _logger.LogWarning(
                    "EInvoiceGateway Username/Password is not configured. Set EInvoiceGateway:Password in config or EInvoiceGateway__Password environment variable.");
                return false;
            }

            var maxAttempts = Math.Max(1, _options.LoginRetryCount);
            Exception? lastError = null;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await LoginOnceAsync(cancellationToken);
                    return true;
                }
                catch (Exception ex) when (attempt < maxAttempts)
                {
                    lastError = ex;
                    _logger.LogWarning(
                        ex,
                        "Viettel e-invoice login attempt {Attempt}/{MaxAttempts} failed, retrying in {DelaySeconds}s.",
                        attempt,
                        maxAttempts,
                        _options.LoginRetryDelaySeconds);

                    await Task.Delay(
                        TimeSpan.FromSeconds(Math.Max(1, _options.LoginRetryDelaySeconds)),
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            if (lastError != null)
            {
                _logger.LogWarning(
                    lastError,
                    "Viettel e-invoice login failed after {MaxAttempts} attempts.",
                    maxAttempts);
            }

            return false;
        }

        private async Task LoginOnceAsync(CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient(ViettelEInvoiceGatewayClient.HttpClientName);
            using var content = new StringContent(
                JsonSerializer.Serialize(new
                {
                    username = _options.Username,
                    password = _options.Password
                }),
                Encoding.UTF8,
                "application/json");

            using var response = await client.PostAsync("auth/login", content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Viettel e-invoice login failed ({(int)response.StatusCode}): {body}");
            }

            var login = JsonSerializer.Deserialize<ViettelEInvoiceLoginResponse>(body, JsonOptions)
                ?? throw new InvalidOperationException("Viettel e-invoice login returned empty payload.");

            if (string.IsNullOrWhiteSpace(login.access_token))
            {
                throw new InvalidOperationException("Viettel e-invoice login did not return access_token.");
            }

            _accessToken = login.access_token;
            _nextRefreshAtUtc = DateTime.UtcNow.AddMinutes(Math.Max(1, _options.TokenRefreshMinutes));

            _logger.LogInformation(
                "Viettel e-invoice token refreshed. Next refresh at {NextRefreshUtc:O}",
                _nextRefreshAtUtc);
        }
    }
}
