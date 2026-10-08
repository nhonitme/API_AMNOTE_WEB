using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace API_AMNOTE_WEB.Services.EInvoiceGateway
{
    public sealed class ViettelEInvoiceGatewayClient : IViettelEInvoiceGatewayClient
    {
        public const string HttpClientName = "ViettelEInvoiceGateway";
        public const string InboundHttpClientName = "ViettelEInvoiceInboundGateway";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IViettelEInvoiceTokenProvider _tokenProvider;
        private readonly EInvoiceGatewayOptions _options;
        private readonly ILogger<ViettelEInvoiceGatewayClient> _logger;

        public ViettelEInvoiceGatewayClient(
            IHttpClientFactory httpClientFactory,
            IViettelEInvoiceTokenProvider tokenProvider,
            IOptions<EInvoiceGatewayOptions> options,
            ILogger<ViettelEInvoiceGatewayClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _tokenProvider = tokenProvider;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<ViettelEInvoiceSendResult> SendDvgpRequestAsync(
            string requestXml,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(requestXml))
            {
                return new ViettelEInvoiceSendResult
                {
                    IsSuccess = false,
                    StatusCode = 0,
                    ResponseBody = "REQUEST_XML is empty."
                };
            }

            return await SendDvgpRequestInternalAsync(requestXml.Trim(), retryOnUnauthorized: true, cancellationToken);
        }

        private async Task<ViettelEInvoiceSendResult> SendDvgpRequestInternalAsync(
            string requestXml,
            bool retryOnUnauthorized,
            CancellationToken cancellationToken)
        {
            var accessToken = await _tokenProvider.TryGetAccessTokenAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return new ViettelEInvoiceSendResult
                {
                    IsSuccess = false,
                    StatusCode = 0,
                    ResponseBody = "Unable to acquire Viettel access token."
                };
            }

            var client = _httpClientFactory.CreateClient(InboundHttpClientName);
            var path = string.IsNullOrWhiteSpace(_options.InboundGatewaySendPath)
                ? "services/tctninboundgateway/api/v1/invoice/dvgp/request"
                : _options.InboundGatewaySendPath.TrimStart('/');

            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.TryAddWithoutValidation("Cookie", $"access_token={accessToken}");
            request.Content = new StringContent(requestXml, Encoding.UTF8, "application/xml");

            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized && retryOnUnauthorized)
            {
                await _tokenProvider.InvalidateAsync(cancellationToken);
                return await SendDvgpRequestInternalAsync(requestXml, retryOnUnauthorized: false, cancellationToken);
            }

            var result = new ViettelEInvoiceSendResult
            {
                IsSuccess = response.IsSuccessStatusCode,
                StatusCode = (int)response.StatusCode,
                ResponseBody = body
            };

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Viettel dvgp request failed. Status={StatusCode}, Body={Body}",
                    result.StatusCode,
                    Truncate(body, 500));
            }

            return result;
        }

        public async Task<IReadOnlyList<ViettelEInvoiceMessageLookupResult>> LookupMessagesAsync(
            string mst,
            string mtdiep,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(mst) || string.IsNullOrWhiteSpace(mtdiep))
            {
                return Array.Empty<ViettelEInvoiceMessageLookupResult>();
            }

            return await SendLookupAsync(mst, mtdiep, retryOnUnauthorized: true, cancellationToken);
        }

        private async Task<IReadOnlyList<ViettelEInvoiceMessageLookupResult>> SendLookupAsync(
            string mst,
            string mtdiep,
            bool retryOnUnauthorized,
            CancellationToken cancellationToken)
        {
            var accessToken = await _tokenProvider.TryGetAccessTokenAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return Array.Empty<ViettelEInvoiceMessageLookupResult>();
            }

            var client = _httpClientFactory.CreateClient(HttpClientName);

            var url =
                $"services/tctnapplication/api/tra-cuu-thong-diep?mst={Uri.EscapeDataString(mst)}&mtdiep={Uri.EscapeDataString(mtdiep)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized && retryOnUnauthorized)
            {
                await _tokenProvider.InvalidateAsync(cancellationToken);
                return await SendLookupAsync(mst, mtdiep, retryOnUnauthorized: false, cancellationToken);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Array.Empty<ViettelEInvoiceMessageLookupResult>();
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Viettel tra-cuu failed. Status={StatusCode}, Mst={Mst}, Mtdiep={Mtdiep}, Body={Body}",
                    (int)response.StatusCode,
                    mst,
                    mtdiep,
                    Truncate(body, 500));
                return Array.Empty<ViettelEInvoiceMessageLookupResult>();
            }

            return ParseLookupResponse(body, mtdiep);
        }

        private static IReadOnlyList<ViettelEInvoiceMessageLookupResult> ParseLookupResponse(string body, string fallbackMtdiep)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return Array.Empty<ViettelEInvoiceMessageLookupResult>();
            }

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                var results = new List<ViettelEInvoiceMessageLookupResult>();
                foreach (var item in root.EnumerateArray())
                {
                    var parsed = ParseLookupElement(item, fallbackMtdiep);
                    if (parsed != null)
                    {
                        results.Add(parsed);
                    }
                }

                return results;
            }

            var single = ParseLookupElement(root, fallbackMtdiep);
            return single == null
                ? Array.Empty<ViettelEInvoiceMessageLookupResult>()
                : new[] { single };
        }

        private static ViettelEInvoiceMessageLookupResult? ParseLookupElement(JsonElement element, string fallbackMtdiep)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var dlieu = ReadString(element, "dlieu");
            if (string.IsNullOrWhiteSpace(dlieu))
            {
                return null;
            }

            return new ViettelEInvoiceMessageLookupResult
            {
                PBAN = ReadString(element, "pban"),
                MNGUI = ReadString(element, "mngui"),
                MNNHAN = ReadString(element, "mnnhan"),
                MLTDIEP = ReadFlexibleString(element, "mltdiep"),
                MTDIEP = ReadString(element, "mtdiep", fallbackMtdiep),
                MTDTCHIEU = ReadString(element, "mtdtchieu"),
                MST = ReadString(element, "mst"),
                SLUONG = ReadInt(element, "sluong"),
                DLieu = dlieu.Trim()
            };
        }

        private static string ReadString(JsonElement element, string propertyName, string fallback = "")
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return fallback;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString()?.Trim() ?? fallback,
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "1",
                JsonValueKind.False => "0",
                _ => fallback
            };
        }

        private static string ReadFlexibleString(JsonElement element, string propertyName)
        {
            return ReadString(element, propertyName);
        }

        private static int ReadInt(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var value))
            {
                return 0;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            {
                return number;
            }

            return int.TryParse(ReadString(element, propertyName), out var parsed) ? parsed : 0;
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value[..maxLength];
        }
    }
}
