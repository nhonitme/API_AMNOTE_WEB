using System.Text.Json;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models.DTOs;
using Microsoft.Extensions.Options;

namespace API_AMNOTE_WEB.Services
{
    public class AiSystemService : IAiSystemService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly ILicenseAiService _licenseAiService;
        private readonly ICompanyDatabaseResolver _databaseResolver;
        private readonly AiExternalClient _aiClient;
        private readonly AmnoteAiOptions _options;
        private readonly ILogger<AiSystemService> _logger;

        public AiSystemService(
            ILicenseAiService licenseAiService,
            ICompanyDatabaseResolver databaseResolver,
            AiExternalClient aiClient,
            IOptions<AmnoteAiOptions> options,
            ILogger<AiSystemService> logger)
        {
            _licenseAiService = licenseAiService;
            _databaseResolver = databaseResolver;
            _aiClient = aiClient;
            _options = options.Value;
            _logger = logger;
        }

        public Task<AiLicenseStatusDto> GetStatusAsync(string companyCd)
            => _licenseAiService.GetStatusAsync(companyCd);

        public async Task<AiInitResultDto> InitAsync(string companyCd, CancellationToken cancellationToken = default)
        {
            var (ok, message, status) = await _licenseAiService.CheckAsync(companyCd, requireInitData: false);
            if (!ok)
            {
                return new AiInitResultDto
                {
                    Success = false,
                    Message = message,
                    Status = status
                };
            }

            string tenDatabase;
            try
            {
                tenDatabase = await _databaseResolver.ResolveDatabaseNameAsync(companyCd);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Resolve database failed for AI init. Company={CompanyCd}", companyCd);
                tenDatabase = string.Empty;
            }

            var apiKey = (_options.ApiKey ?? string.Empty).Trim();
            var url = _options.ResolveNapDuLieuAutoUrl();
            var steps = new List<AiInitStepResultDto>
            {
                new() { Step = "ConnectDB", Success = true, Message = "OK" },
                new() { Step = "AnalystData", Success = true, Message = "OK" }
            };

            var voucher = await CallNapAsync(
                url,
                apiKey,
                new { client_id = companyCd, ten_database = tenDatabase },
                "voucher",
                cancellationToken);
            steps.Add(voucher);
            if (!voucher.Success)
            {
                return FailInit(steps, voucher.Message, status);
            }

            var product = await CallNapAsync(
                url,
                apiKey,
                new { client_id = companyCd, loai_cot = "hang_hoa", ten_database = tenDatabase },
                "product",
                cancellationToken);
            steps.Add(product);
            if (!product.Success)
            {
                return FailInit(steps, product.Message, status);
            }

            var customer = await CallNapAsync(
                url,
                apiKey,
                new { client_id = companyCd, loai_cot = "khach_hang", ten_database = tenDatabase },
                "customer",
                cancellationToken);
            steps.Add(customer);
            if (!customer.Success)
            {
                return FailInit(steps, customer.Message, status);
            }

            await _licenseAiService.MarkInitDataAsync(companyCd);
            steps.Add(new AiInitStepResultDto { Step = "Success", Success = true, Message = "OK" });
            var refreshed = await _licenseAiService.GetStatusAsync(companyCd);
            return new AiInitResultDto
            {
                Success = true,
                Message = "Khởi tạo hệ thống AI thành công.",
                Steps = steps,
                Status = refreshed
            };
        }

        public async Task<AiSuggestAccountsResponse> SuggestAccountsAsync(
            string companyCd,
            AiSuggestAccountsRequest request,
            CancellationToken cancellationToken = default)
        {
            var (ok, message, _) = await _licenseAiService.CheckAsync(companyCd, requireInitData: true);
            if (!ok)
            {
                throw new InvalidOperationException(message);
            }

            var items = (request.Items ?? [])
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.LineKey) && !string.IsNullOrWhiteSpace(x.TenHang))
                .ToList();
            if (items.Count == 0)
            {
                return new AiSuggestAccountsResponse();
            }

            var danhSach = items.Select(x => new
            {
                ten_hang = x.TenHang.Trim(),
                ten_kh = (x.TenKh ?? string.Empty).Trim(),
                loai_hoa_don = (x.LoaiHoaDon ?? string.Empty).Trim()
            }).ToList();

            var body = new
            {
                client_id = companyCd,
                danh_sach = danhSach
            };

            var (postOk, responseBody, error) = await _aiClient.PostJsonAsync(
                _options.ResolveGoiYHangLoatUrl(),
                body,
                _options.ApiKey,
                cancellationToken);

            if (!postOk)
            {
                throw new InvalidOperationException(error ?? "Gọi AI gợi ý tài khoản thất bại.");
            }

            var parsed = ParseSuggestResults(responseBody);
            if (parsed == null || parsed.Count != items.Count)
            {
                throw new InvalidOperationException("AI trả kết quả gợi ý không hợp lệ.");
            }

            var result = new AiSuggestAccountsResponse();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var suggest = parsed[i];
                result.Items.Add(new AiSuggestAccountItemResult
                {
                    LineKey = item.LineKey,
                    Debit = NormalizeAccount(suggest.tk_no),
                    Credit = NormalizeAccount(suggest.tk_co),
                    Reason = string.IsNullOrWhiteSpace(suggest.ly_do) ? null : suggest.ly_do.Trim()
                });
            }

            return result;
        }

        public async Task<AiTranslateResponse> TranslateAsync(
            string companyCd,
            AiTranslateRequest request,
            CancellationToken cancellationToken = default)
        {
            var (ok, message, _) = await _licenseAiService.CheckAsync(companyCd, requireInitData: true);
            if (!ok)
            {
                throw new InvalidOperationException(message);
            }

            request ??= new AiTranslateRequest();
            var workItems = BuildTranslateWorkItems(request);
            if (workItems.Count == 0)
            {
                throw new ArgumentException("Text hoặc Items là bắt buộc.");
            }

            var results = new List<AiTranslateItemResult>(workItems.Count);
            foreach (var item in workItems)
            {
                results.Add(await TranslateOneAsync(companyCd, item, cancellationToken));
            }

            var response = new AiTranslateResponse { Items = results };
            if (results.Count == 1 && (request.Items == null || request.Items.Count == 0))
            {
                response.Text = results[0].Text;
                response.TargetLang = results[0].TargetLang;
                response.Result = results[0].Result;
            }

            return response;
        }

        private async Task<AiTranslateItemResult> TranslateOneAsync(
            string companyCd,
            AiTranslateItemRequest item,
            CancellationToken cancellationToken)
        {
            var source = item.Text ?? string.Empty;
            var targetLang = NormalizeTranslateLang(item.TargetLang);
            var result = new AiTranslateItemResult
            {
                Key = item.Key,
                Text = source,
                TargetLang = targetLang,
                Result = source
            };

            if (string.IsNullOrWhiteSpace(source))
            {
                return result;
            }

            var fields = new[]
            {
                new KeyValuePair<string, string>("van_ban", source),
                new KeyValuePair<string, string>("dich_ra", targetLang)
            };

            var (postOk, responseBody, error) = await _aiClient.PostMultipartAsync(
                _options.ResolveDichThuatUrl(),
                fields,
                _options.ApiKey,
                cancellationToken);

            if (!postOk)
            {
                _logger.LogWarning(
                    "AI dich-thuat failed for company {Company}. Error={Error}",
                    companyCd,
                    error);
                return result;
            }

            var ketQua = ParseDichThuatKetQua(responseBody);
            if (!string.IsNullOrEmpty(ketQua))
            {
                result.Result = ketQua;
            }

            return result;
        }

        private static List<AiTranslateItemRequest> BuildTranslateWorkItems(AiTranslateRequest request)
        {
            if (request.Items is { Count: > 0 })
            {
                return request.Items
                    .Where(x => x != null)
                    .Select(x => new AiTranslateItemRequest
                    {
                        Key = x.Key,
                        Text = x.Text ?? string.Empty,
                        TargetLang = x.TargetLang
                    })
                    .ToList();
            }

            if (request.Text == null)
            {
                return [];
            }

            return
            [
                new AiTranslateItemRequest
                {
                    Text = request.Text,
                    TargetLang = request.TargetLang
                }
            ];
        }

        private static string NormalizeTranslateLang(string? lang)
        {
            var value = (lang ?? string.Empty).Trim().ToUpperInvariant();
            return value switch
            {
                "VIET" or "ENG" or "KOR" or "CHINA" => value,
                "EN" or "ENGLISH" => "ENG",
                "KR" or "KO" or "KOREAN" => "KOR",
                "VN" or "VI" or "VIETNAMESE" => "VIET",
                "ZH" or "CN" or "CHN" or "CHINESE" => "CHINA",
                _ => "ENG"
            };
        }

        private static string? ParseDichThuatKetQua(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    foreach (var name in new[] { "ket_qua", "result", "text", "data" })
                    {
                        if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                        {
                            var text = value.GetString();
                            return string.IsNullOrEmpty(text) ? null : text;
                        }
                    }
                }
                else if (root.ValueKind == JsonValueKind.String)
                {
                    return root.GetString();
                }
            }
            catch
            {
            }

            return null;
        }

        private async Task<AiInitStepResultDto> CallNapAsync(
            string url,
            string apiKey,
            object body,
            string step,
            CancellationToken cancellationToken)
        {
            var (ok, _, error) = await _aiClient.PostJsonAsync(url, body, apiKey, cancellationToken);
            return new AiInitStepResultDto
            {
                Step = step,
                Success = ok,
                Message = ok ? "OK" : (error ?? "Lỗi gọi AI")
            };
        }

        private static AiInitResultDto FailInit(List<AiInitStepResultDto> steps, string? message, AiLicenseStatusDto status)
            => new()
            {
                Success = false,
                Message = message ?? "Khởi tạo AI thất bại.",
                Steps = steps,
                Status = status
            };

        private static string? NormalizeAccount(string? value)
        {
            var text = (value ?? string.Empty).Trim();
            return text.Length == 0 ? null : text;
        }

        private static List<SuggestPayload>? ParseSuggestResults(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                var direct = JsonSerializer.Deserialize<List<SuggestPayload>>(json, JsonOptions);
                if (direct != null)
                {
                    return direct;
                }
            }
            catch
            {
                // fall through
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                JsonElement array = default;
                var found = false;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    array = root;
                    found = true;
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    foreach (var name in new[] { "ket_qua", "results", "data", "danh_sach" })
                    {
                        if (root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array)
                        {
                            array = value;
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    return null;
                }

                var list = new List<SuggestPayload>();
                foreach (var item in array.EnumerateArray())
                {
                    var token = item;
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        if (item.TryGetProperty("result", out var resultToken))
                        {
                            token = resultToken;
                        }
                        else if (item.TryGetProperty("ket_qua", out var ketQua) && ketQua.ValueKind == JsonValueKind.Object)
                        {
                            token = ketQua;
                        }
                    }

                    var parsed = JsonSerializer.Deserialize<SuggestPayload>(token.GetRawText(), JsonOptions)
                        ?? new SuggestPayload();
                    list.Add(parsed);
                }

                return list;
            }
            catch
            {
                return null;
            }
        }

        private sealed class SuggestPayload
        {
            public string? tk_no { get; set; }
            public string? tk_co { get; set; }
            public string? ly_do { get; set; }
        }
    }
}
