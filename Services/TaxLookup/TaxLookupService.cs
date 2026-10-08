using System.Text.Json;
using System.Text.Json.Serialization;
using API_AMNOTE_WEB.Models.DTOs;
using Microsoft.Extensions.Options;

namespace API_AMNOTE_WEB.Services.TaxLookup
{
    public class TaxLookupOptions
    {
        public string BaseUrl { get; set; } = "http://115.78.232.22:8000";
    }

    public class TaxLookupService : ITaxLookupService
    {
        public const string HttpClientName = "TaxLookup";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TaxLookupOptions _options;
        private readonly ILogger<TaxLookupService> _logger;

        public TaxLookupService(
            IHttpClientFactory httpClientFactory,
            IOptions<TaxLookupOptions> options,
            ILogger<TaxLookupService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<TaxLookupInfoDto?> LookupAsync(string mst, CancellationToken cancellationToken = default)
        {
            var normalizedMst = (mst ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedMst))
            {
                return null;
            }

            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                var requestUri = $"tra-cuu-mst?mst={Uri.EscapeDataString(normalizedMst)}";
                using var response = await client.GetAsync(requestUri, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Tax lookup failed for MST {Mst} with status {StatusCode}",
                        normalizedMst,
                        response.StatusCode);
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var payload = await JsonSerializer.DeserializeAsync<TaxLookupApiResponse>(stream, JsonOptions, cancellationToken);
                if (payload == null || !string.Equals(payload.TrangThai, "thanh_cong", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                var info = payload.ThongTin;
                if (info == null)
                {
                    return null;
                }

                return new TaxLookupInfoDto
                {
                    OrgType = NormalizeText(info.OrgType),
                    TaxID = NormalizeText(info.TaxID) ?? normalizedMst,
                    Name = NormalizeText(info.Name),
                    Address = NormalizeText(info.Address),
                    TaxDepartment = NormalizeText(info.TaxDepartment),
                    Status = NormalizeText(info.Status),
                    UpdatedAt = NormalizeText(info.UpdatedAt),
                    MaCoQuanThue = NormalizeText(info.MaCoQuanThue),
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tax lookup request failed for MST {Mst}", normalizedMst);
                return null;
            }
        }

        private static string NormalizeText(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private sealed class TaxLookupApiResponse
        {
            [JsonPropertyName("trang_thai")]
            public string? TrangThai { get; set; }

            [JsonPropertyName("mst")]
            public string? Mst { get; set; }

            [JsonPropertyName("thong_tin")]
            public TaxLookupApiInfo? ThongTin { get; set; }
        }

        private sealed class TaxLookupApiInfo
        {
            [JsonPropertyName("orgType")]
            public string? OrgType { get; set; }

            [JsonPropertyName("taxID")]
            public string? TaxID { get; set; }

            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("address")]
            public string? Address { get; set; }

            [JsonPropertyName("taxDepartment")]
            public string? TaxDepartment { get; set; }

            [JsonPropertyName("status")]
            public string? Status { get; set; }

            [JsonPropertyName("updatedAt")]
            public string? UpdatedAt { get; set; }

            [JsonPropertyName("ma_co_quan_thue")]
            public string? MaCoQuanThue { get; set; }
        }
    }
}
