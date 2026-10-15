using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models.DTOs;
using API_AMNOTE_WEB.Services.Tax;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GdtImportController : BaseApiController
    {
        private readonly IGdtImportService _service;
        private readonly IGdtImportRepository _repository;
        private readonly IChitInfoWriteService _chitWriteService;
        private readonly IChitInfoRepository _chitRepository;

        public GdtImportController(
            IGdtImportService service,
            IGdtImportRepository repository,
            IChitInfoWriteService chitWriteService,
            IChitInfoRepository chitRepository)
        {
            _service = service;
            _repository = repository;
            _chitWriteService = chitWriteService;
            _chitRepository = chitRepository;
        }

        /// <summary>Lưu danh sách summary trước (C99 SaveInvoiceList). Trả NeedDetail từng HĐ.</summary>
        [HttpPost("upsert-list")]
        [RequestSizeLimit(32 * 1024 * 1024)]
        public Task<IActionResult> UpsertList(
            [FromBody] GdtImportUpsertRequest? request,
            CancellationToken cancellationToken)
            => ExecuteUpsert(request, listOnly: true, cancellationToken);

        /// <summary>Lưu JSON chi tiết sau khi kéo detail (batch).</summary>
        [HttpPost("upsert-json")]
        [RequestSizeLimit(32 * 1024 * 1024)]
        public Task<IActionResult> UpsertJson(
            [FromBody] GdtImportUpsertRequest? request,
            CancellationToken cancellationToken)
            => ExecuteUpsert(request, listOnly: false, cancellationToken);

        /// <summary>Upsert gộp header + json (tương thích cũ).</summary>
        [HttpPost("upsert")]
        [RequestSizeLimit(32 * 1024 * 1024)]
        public async Task<IActionResult> Upsert(
            [FromBody] GdtImportUpsertRequest? request,
            CancellationToken cancellationToken)
        {
            var gate = await ValidateAndAuthorizeAsync(request);
            if (gate != null)
            {
                return gate;
            }

            try
            {
                var result = await _service.UpsertAsync(
                    Common.GetCompanyCode(),
                    Common.GetUserId(),
                    request!,
                    cancellationToken);
                return Success(result, $"Đã lưu {result.Upserted}/{result.Received} hóa đơn");
            }
            catch (InvalidOperationException ex)
            {
                return BusinessError(ex.Message);
            }
        }

        /// <summary>Danh sách tài khoản GDT đã lưu (InitSavedLogin).</summary>
        [HttpGet("logins")]
        public async Task<IActionResult> ListLogins(CancellationToken cancellationToken)
        {
            await EnsureVatListPermissionAsync();
            var data = await _service.ListSavedLoginsAsync(Common.GetCompanyCode(), cancellationToken);
            return Success(data);
        }

        /// <summary>Đọc JSON GDT đầy đủ của hóa đơn để hiển thị chi tiết hàng hóa/dịch vụ.</summary>
        [HttpGet("detail")]
        public async Task<IActionResult> GetInvoiceDetail(
            [FromQuery] string? type,
            [FromQuery] string? mhdon,
            CancellationToken cancellationToken)
        {
            await EnsureVatListPermissionAsync();
            var invoiceType = NormalizeInvoiceType(type);
            var invoiceKey = (mhdon ?? "").Trim();
            if (invoiceType == null)
            {
                return ValidationError("Loại hóa đơn không hợp lệ.");
            }
            if (invoiceKey.Length == 0 || invoiceKey.Length > 128)
            {
                return ValidationError("Mã hóa đơn không hợp lệ.");
            }

            var json = await _repository.ReadInvoiceJsonAsync(invoiceType, invoiceKey, cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return NotFound("Không tìm thấy JSON chi tiết của hóa đơn.");
            }

            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(json);
                return Success(document.RootElement.Clone());
            }
            catch (System.Text.Json.JsonException)
            {
                return BusinessError("JSON chi tiết của hóa đơn không hợp lệ.");
            }
        }

        /// <summary>Lưu tài khoản GDT sau đồng bộ (SaveGdtLoginInfo).</summary>
        [HttpPost("save-login")]
        public async Task<IActionResult> SaveLogin(
            [FromBody] GdtSaveLoginRequest? request,
            CancellationToken cancellationToken)
        {
            await EnsureVatListPermissionAsync();
            if (request == null)
            {
                return ValidationError("Body không hợp lệ");
            }

            try
            {
                await _service.SaveLoginAsync(
                    Common.GetCompanyCode(),
                    request.Username,
                    request.Password,
                    request.Token,
                    cancellationToken);
                return Success(true, "Đã lưu tài khoản GDT");
            }
            catch (InvalidOperationException ex)
            {
                return BusinessError(ex.Message);
            }
        }

        [HttpPost("link-vouchers")]
        public async Task<IActionResult> LinkVouchers(
            [FromBody] GdtInvoiceVoucherLinkRequest? request,
            CancellationToken cancellationToken)
        {
            await EnsureVatListWritePermissionAsync();
            if (request == null || request.Items == null || request.Items.Count == 0)
            {
                return ValidationError("Cần danh sách hóa đơn/chứng từ để liên kết.");
            }
            if (request.Items.Count > 1000)
            {
                return ValidationError("Số hóa đơn vượt giới hạn 1000.");
            }

            var invoiceType = NormalizeInvoiceType(request.Type);
            if (invoiceType == null)
            {
                return ValidationError("Loại hóa đơn không hợp lệ.");
            }

            try
            {
                var results = await _repository.LinkInvoicesToVouchersAsync(invoiceType, request.Items, cancellationToken);
                return Success(results, "Đã xử lý liên kết hóa đơn với chứng từ.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return BusinessError(ex.Message);
            }
        }

        [HttpPost("create-vouchers")]
        [RequestSizeLimit(32 * 1024 * 1024)]
        public async Task<IActionResult> CreateVouchers(
            [FromBody] GdtVoucherCreateBatchRequest? request,
            CancellationToken cancellationToken)
        {
            await EnsureVatListWritePermissionAsync();
            if (request?.Items == null || request.Items.Count == 0)
            {
                return ValidationError("Cần ít nhất một chứng từ để lưu.");
            }
            if (request.Items.Count > 100)
            {
                return ValidationError("Mỗi lần chỉ được lưu tối đa 100 chứng từ.");
            }

            var invoiceType = NormalizeInvoiceType(request.Type);
            if (invoiceType == null)
            {
                return ValidationError("Loại hóa đơn không hợp lệ.");
            }

            var voucherType = NormalizeVoucherType(invoiceType, request.VoucherType);
            if (voucherType == null)
            {
                return ValidationError("Loại phiếu không hợp lệ hoặc không tương thích với loại hóa đơn.");
            }

            var inputType = invoiceType == "BUY" ? "AP" : "AR";
            var chitType = voucherType;
            var companyCd = Common.GetCompanyCode();
            var userId = Common.GetUserId();
            var totalInvoiceCount = request.Items.Sum(item => (item.Mhdon ?? new List<string>())
                .Select(value => (value ?? "").Trim())
                .Where(value => value.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());
            if (totalInvoiceCount > 1000)
            {
                return ValidationError("Mỗi lần chỉ được xử lý tối đa 1000 hóa đơn.");
            }

            var invoiceOwner = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var duplicatedItems = new HashSet<int>();
            for (var index = 0; index < request.Items.Count; index++)
            {
                var item = request.Items[index];
                foreach (var mhdon in (item.Mhdon ?? new List<string>()).Select(value => (value ?? "").Trim()).Where(value => value.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (invoiceOwner.TryGetValue(mhdon, out var ownerIndex) && ownerIndex != index)
                    {
                        duplicatedItems.Add(ownerIndex);
                        duplicatedItems.Add(index);
                    }
                    else
                    {
                        invoiceOwner[mhdon] = index;
                    }
                }
            }

            var results = new List<GdtVoucherCreateBatchItemResult>(request.Items.Count);
            for (var index = 0; index < request.Items.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = request.Items[index];
                var invoiceKeys = (item.Mhdon ?? new List<string>())
                    .Select(value => (value ?? "").Trim())
                    .Where(value => value.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var result = new GdtVoucherCreateBatchItemResult { Index = index };
                if (duplicatedItems.Contains(index))
                {
                    result.Status = "INVALID";
                    result.Error = "Một hóa đơn không được gán cho nhiều chứng từ trong cùng một lần lưu.";
                }
                else if (invoiceKeys.Count == 0 || item.Voucher == null)
                {
                    result.Status = "INVALID";
                    result.Error = "Chứng từ phải có hóa đơn nguồn và dữ liệu hợp lệ.";
                }
                else if (!string.IsNullOrWhiteSpace(item.Voucher.CHIT_TYPE)
                    && !string.Equals(item.Voucher.CHIT_TYPE.Trim(), chitType, StringComparison.OrdinalIgnoreCase))
                {
                    result.Status = "INVALID";
                    result.Error = "Loại chứng từ không khớp với loại hóa đơn.";
                }
                else
                {
                    try
                    {
                        var chitId = await _chitWriteService.CreateAsync(
                            companyCd,
                            inputType,
                            chitType,
                            userId,
                            item.Voucher);
                        result.ChitId = chitId;
                        var created = (await _chitRepository.GetChitInfosAsync(companyCd, inputType, null, chitId, null)).FirstOrDefault();
                        if (created == null || string.IsNullOrWhiteSpace(created.CHIT_CD))
                        {
                            result.Status = "CREATED_LINK_PENDING";
                            result.Error = "Đã tạo chứng từ nhưng không đọc lại được mã chứng từ để liên kết hóa đơn.";
                        }
                        else
                        {
                            result.ChitCd = created.CHIT_CD;
                            result.ChitNo = created.CHIT_NO;
                            result.Links = (await _repository.LinkInvoicesToVouchersAsync(
                                invoiceType,
                                invoiceKeys.Select(mhdon => new GdtInvoiceVoucherLinkItem
                                {
                                    Mhdon = mhdon,
                                    ChitCd = created.CHIT_CD,
                                }).ToList(),
                                cancellationToken)).ToList();

                            var failedLinks = result.Links.Where(link => link.Status != "LINKED" && link.Status != "ALREADY_LINKED").ToList();
                            if (failedLinks.Count == 0 && result.Links.Count == invoiceKeys.Count)
                            {
                                result.Status = "SUCCESS";
                            }
                            else
                            {
                                result.Status = "CREATED_LINK_PENDING";
                                result.Error = string.Join("; ", failedLinks.Select(link => $"{link.Mhdon}: {link.Error ?? link.Status}"));
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        result.Status = result.ChitId.HasValue ? "CREATED_LINK_PENDING" : "FAILED";
                        result.Error = result.ChitId.HasValue
                            ? "Đã tạo chứng từ nhưng chưa liên kết xong hóa đơn."
                            : ex is ArgumentException or InvalidOperationException
                                ? ex.Message
                                : "Không tạo được chứng từ. Vui lòng kiểm tra dữ liệu và thử lại.";
                    }
                }
                results.Add(result);
            }

            return Success(results, "Đã xử lý lưu lô chứng từ hóa đơn VAT.");
        }

        private async Task EnsureVatListPermissionAsync()
        {
            try
            {
                await EnsurePermissionAsync("TAX_VAT_INOUT_LIST", "VIEW");
            }
            catch (UnauthorizedAccessException)
            {
                try
                {
                    await EnsurePermissionAsync("TAX_VAT_INOUT_LIST", "ADD");
                }
                catch (UnauthorizedAccessException)
                {
                    await EnsurePermissionAsync("TAX_VAT_INOUT_LIST", "EDIT");
                }
            }
        }

        private async Task EnsureVatListWritePermissionAsync()
        {
            try
            {
                await EnsurePermissionAsync("TAX_VAT_INOUT_LIST", "ADD");
            }
            catch (UnauthorizedAccessException)
            {
                await EnsurePermissionAsync("TAX_VAT_INOUT_LIST", "EDIT");
            }
        }

        private static string? NormalizeInvoiceType(string? value)
        {
            var normalized = (value ?? "").Trim().ToUpperInvariant();
            return normalized switch
            {
                "BUY" or "IN" or "1" => "BUY",
                "SELL" or "OUT" or "2" or "0" => "SELL",
                _ => null,
            };
        }

        private static string? NormalizeVoucherType(string invoiceType, string? value)
        {
            var normalized = (value ?? "").Trim().ToUpperInvariant();
            if (normalized.Length == 0)
            {
                return invoiceType == "BUY" ? "PO" : "SO";
            }

            return (invoiceType, normalized) switch
            {
                ("BUY", "PO") => "PO",
                ("BUY", "PS") => "PS",
                ("SELL", "SO") => "SO",
                _ => null,
            };
        }

        private async Task<IActionResult> ExecuteUpsert(
            GdtImportUpsertRequest? request,
            bool listOnly,
            CancellationToken cancellationToken)
        {
            var gate = await ValidateAndAuthorizeAsync(request);
            if (gate != null)
            {
                return gate;
            }

            try
            {
                var result = listOnly
                    ? await _service.UpsertListAsync(
                        Common.GetCompanyCode(),
                        Common.GetUserId(),
                        request!,
                        cancellationToken)
                    : await _service.UpsertJsonAsync(
                        Common.GetCompanyCode(),
                        Common.GetUserId(),
                        request!,
                        cancellationToken);

                var message = listOnly
                    ? $"Đã lưu list {result.ListSaved}/{result.Received}; cần detail {result.NeedDetailCount}"
                    : $"Đã lưu JSON {result.JsonSaved}/{result.Received}";
                return Success(result, message);
            }
            catch (InvalidOperationException ex)
            {
                return BusinessError(ex.Message);
            }
        }

        private async Task<IActionResult?> ValidateAndAuthorizeAsync(GdtImportUpsertRequest? request)
        {
            if (request == null)
            {
                return ValidationError("Body không hợp lệ");
            }

            if (request.Items == null)
            {
                return ValidationError("Thiếu items");
            }

            if (request.Items.Count > GdtImportService.MaxItems)
            {
                return ValidationError($"Số hóa đơn vượt giới hạn {GdtImportService.MaxItems}");
            }

            try
            {
                await EnsurePermissionAsync("TAX_VAT_INOUT_LIST", "ADD");
            }
            catch (UnauthorizedAccessException)
            {
                await EnsurePermissionAsync("TAX_VAT_INOUT_LIST", "EDIT");
            }

            return null;
        }
    }
}
