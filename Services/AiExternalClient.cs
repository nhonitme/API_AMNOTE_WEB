using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace API_AMNOTE_WEB.Services
{
    public class AiExternalClient
    {
        public const string HttpClientName = "AmnoteAi";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<AiExternalClient> _logger;

        public AiExternalClient(IHttpClientFactory httpClientFactory, ILogger<AiExternalClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<(bool Ok, string? Body, string? Error)> PostJsonAsync(
            string url,
            object body,
            string? apiKey,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return (false, null, "URL AI trống.");
            }

            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey.Trim());
                }

                var json = JsonSerializer.Serialize(body, JsonOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using var response = await client.SendAsync(request, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "AI POST {Url} failed with {Status}. Body={Body}",
                        url,
                        (int)response.StatusCode,
                        Truncate(responseBody));
                    return (false, responseBody, $"AI trả lỗi HTTP {(int)response.StatusCode}");
                }

                return (true, responseBody, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI POST {Url} failed", url);
                return (false, null, ex.Message);
            }
        }

        public async Task<(bool Ok, string? Body, string? Error)> PostMultipartAsync(
            string url,
            IEnumerable<KeyValuePair<string, string>> formFields,
            string? apiKey,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return (false, null, "URL AI trống.");
            }

            try
            {
                var client = _httpClientFactory.CreateClient(HttpClientName);
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey.Trim());
                }

                var content = new MultipartFormDataContent();
                foreach (var field in formFields)
                {
                    content.Add(new StringContent(field.Value ?? string.Empty, Encoding.UTF8), field.Key);
                }

                request.Content = content;

                using var response = await client.SendAsync(request, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "AI multipart POST {Url} failed with {Status}. Body={Body}",
                        url,
                        (int)response.StatusCode,
                        Truncate(responseBody));
                    return (false, responseBody, $"AI trả lỗi HTTP {(int)response.StatusCode}");
                }

                return (true, responseBody, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI multipart POST {Url} failed", url);
                return (false, null, ex.Message);
            }
        }

        private static string Truncate(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Length <= 500 ? value : value[..500];
        }
    }
}
