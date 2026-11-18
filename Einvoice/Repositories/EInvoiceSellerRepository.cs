using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories
{
    public class EInvoiceSellerRepository : IEInvoiceSellerRepository
    {
        private const string SellerCacheScope = "einvoice-seller";
        private const string GetSellersQuery = "CALL getEInvoiceSeller(@p_COMPANY_CD, @p_KHHDON, @p_SELLER_ID, @p_XSL_ID, @p_KEYWORD, @p_INCLUDE_INACTIVE, @p_INCLUDE_XSL, @p_INCLUDE_ALL_TEMPLATES)";

        private readonly API_AMNOTE_WEB.Services.ElectronicDocumentRenderContext _renderContext;
        private readonly DapperExecutor _db;
        private readonly API_AMNOTE_WEB.Services.ElectronicDocumentStorage _files;
        private readonly IMasterDataCacheService _cache;
        private readonly ICompanyInfoRepository _companyInfo;

        public EInvoiceSellerRepository(DapperExecutor db, IMasterDataCacheService cache, ICompanyInfoRepository companyInfo, API_AMNOTE_WEB.Services.ElectronicDocumentStorage files, API_AMNOTE_WEB.Services.ElectronicDocumentRenderContext renderContext)
        {
            _db = db;
            _files = files;
            _renderContext = renderContext;
            _cache = cache;
            _companyInfo = companyInfo;
        }

        public async Task<IEnumerable<EInvoiceSellerInfo>> GetSellersAsync(
            string companyCd,
            string? khhdon = null,
            long? sellerId = null,
            long? xslId = null,
            string? keyword = null,
            bool includeInactive = false,
            bool includeXslContent = false,
            bool includeAllTemplates = false)
        {
            var normalizedKhhdon = Common.NormalizeNullableText(khhdon) ?? string.Empty;
            // Only the unfiltered setting request initializes the company profile.
            var isSettingRequest = includeAllTemplates && includeInactive
                && string.IsNullOrEmpty(normalizedKhhdon) && sellerId.GetValueOrDefault() == 0
                && xslId.GetValueOrDefault() == 0 && string.IsNullOrWhiteSpace(keyword);
            EInvoiceSellerInfo? profile = null;
            if (isSettingRequest)
            {
                profile = await GetOrCreateCompanyProfileAsync(companyCd);
            }
            var cacheKey = BuildCacheKey(normalizedKhhdon, sellerId, xslId, keyword, includeInactive, includeXslContent, includeAllTemplates);
            var rows = await _cache.GetOrCreateAsync(
                SellerCacheScope,
                companyCd,
                cacheKey,
                async () => (await QuerySellersAsync(companyCd, normalizedKhhdon, sellerId, xslId, keyword, includeInactive, includeXslContent, includeAllTemplates)).ToList());

            // getEInvoiceSeller joins templates; a new company may have no template yet.
            return rows.Count == 0 && profile != null ? new[] { profile } : rows;
        }

        private async Task<EInvoiceSellerInfo?> ReadCompanyProfileAsync(string companyCd)
            => (await _db.QueryAsync<EInvoiceSellerInfo>(Net_DB.Net_DB_Company,
                "SELECT * FROM einvoice_seller_info WHERE COMPANY_CD = @company LIMIT 1",
                new { company = companyCd })).FirstOrDefault();

        private async Task<EInvoiceSellerInfo?> GetOrCreateCompanyProfileAsync(string companyCd)
        {
            var existing = await ReadCompanyProfileAsync(companyCd);
            if (existing != null) return existing;

            var company = await _companyInfo.GetCompanyInfoAsync(companyCd);
            if (company == null) return null;

            // MySQL 8.4.11: never overwrite a profile created/saved by a concurrent request.
            await _db.ExecuteAsync(Net_DB.Net_DB_Company, """
                INSERT INTO einvoice_seller_info
                    (COMPANY_CD, SELLER_CD, SELLER_NM, SELLER_TAX_CD, SELLER_ADDRESS,
                     SDTHOAI, DCTDTU, FAX, WEBSITE, CREATE_BY, CREATE_AT)
                VALUES (@company, @company, @name, @tax, @address,
                        @phone, @email, @fax, @website, @user, NOW())
                ON DUPLICATE KEY UPDATE COMPANY_CD = @company
                """, new
            {
                company = companyCd,
                name = company.COMPANY_NM?.Trim() ?? "",
                tax = company.TAX_CD?.Trim() ?? "",
                address = company.ADDRESS?.Trim() ?? "",
                phone = Common.NormalizeNullableText(company.TEL),
                email = Common.NormalizeNullableText(company.EMAIL),
                fax = Common.NormalizeNullableText(company.FAX) is { } fax ? fax[..Math.Min(fax.Length, 20)] : null,
                website = Common.NormalizeNullableText(company.WEBSITE),
                user = Common.GetUserId()
            });
            await ClearSellersCacheAsync(companyCd);
            return await ReadCompanyProfileAsync(companyCd);
        }

        public async Task<EInvoiceSellerInfo?> GetSellerWithXslAsync(
            string companyCd,
            long? sellerId = null,
            long? xslId = null,
            string? khhdon = null,
            bool includeInactive = false)
        {
            var rows = await GetSellersAsync(
                companyCd,
                khhdon: khhdon,
                sellerId: sellerId,
                xslId: xslId,
                includeInactive: includeInactive,
                includeXslContent: true);

            var selected = rows.FirstOrDefault();
            // Cached catalog instances are shared. A historical render must not mutate
            // their active template/asset references for other requests.
            var row = selected == null ? null : System.Text.Json.JsonSerializer.Deserialize<EInvoiceSellerInfo>(
                System.Text.Json.JsonSerializer.Serialize(selected));
            if (row != null)
            {
                if (_renderContext.Template is { } snapshot)
                {
                    row.XSL_FTP_PATH = snapshot.XslPath;
                    row.LOGO_PATH = snapshot.Logo; row.BACKGROUND_PATH = snapshot.Background;
                    row.INVOICE_BACKGROUND_PATH = snapshot.Nen; row.INVOICE_BORDER_PATH = snapshot.Border;
                }
                row.XSL_CONTENT = _files.ReadXsl(row.XSL_FTP_PATH);
                if (_renderContext.Template == null && !string.IsNullOrWhiteSpace(row.XSL_DESIGN_FTP_PATH))
                    row.XSL_DESIGN_CONTENT = _files.ReadXsl(row.XSL_DESIGN_FTP_PATH);
            }
            return row;
        }

        public async Task<EInvoiceSellerInfo?> UpdateSellerInfoAsync(
            string companyCd,
            string userId,
            long sellerId,
            EInvoiceSellerInfoSaveRequest request)
        {
            if (sellerId <= 0 || string.IsNullOrWhiteSpace(companyCd) || request == null)
            {
                return null;
            }

            // Seller profile save only touches einvoice_seller_info — never join XSL templates.
            var existing = await GetSellerInfoRowAsync(companyCd, sellerId);
            if (existing == null || existing.SELLER_ID <= 0)
            {
                return null;
            }

            var sellerName = Common.NormalizeNullableText(request.SELLER_NM);
            var sellerAddress = Common.NormalizeNullableText(request.SELLER_ADDRESS);
            if (string.IsNullOrWhiteSpace(sellerName) || string.IsNullOrWhiteSpace(sellerAddress))
            {
                throw new ArgumentException(
                    string.IsNullOrWhiteSpace(sellerName) ? "SELLER_NM is required" : "SELLER_ADDRESS is required");
            }

            const string sql = "CALL setEInvoiceSellerInfo(@p_SELLER_ID, @p_COMPANY_CD, @p_SELLER_CD, @p_SELLER_NM, @p_SELLER_TAX_CD, @p_SELLER_ADDRESS, @p_MDDKDOANH, @p_TDDKDOANH, @p_DCDDKDOANH, @p_MCHANG, @p_TCHANG, @p_SDTHOAI, @p_DCTDTU, @p_STKNHANG, @p_TNHANG, @p_FAX, @p_WEBSITE, @p_USERID);";
            await _db.QueryFirstAsync<long>(Net_DB.Net_DB_Company, sql, new
            {
                p_SELLER_ID = sellerId,
                p_COMPANY_CD = companyCd,
                p_SELLER_CD = existing.SELLER_CD,
                p_SELLER_NM = sellerName,
                // Tax code is locked on the web setting screen — always keep the stored value.
                p_SELLER_TAX_CD = existing.SELLER_TAX_CD,
                p_SELLER_ADDRESS = sellerAddress,
                p_MDDKDOANH = Common.NormalizeNullableText(request.MDDKDOANH),
                p_TDDKDOANH = Common.NormalizeNullableText(request.TDDKDOANH),
                p_DCDDKDOANH = Common.NormalizeNullableText(request.DCDDKDOANH),
                p_MCHANG = Common.NormalizeNullableText(request.MCHANG),
                p_TCHANG = Common.NormalizeNullableText(request.TCHANG),
                p_SDTHOAI = Common.NormalizeNullableText(request.SDTHOAI),
                p_DCTDTU = Common.NormalizeNullableText(request.DCTDTU),
                p_STKNHANG = Common.NormalizeNullableText(request.STKNHANG),
                p_TNHANG = Common.NormalizeNullableText(request.TNHANG),
                p_FAX = Common.NormalizeNullableText(request.FAX),
                p_WEBSITE = Common.NormalizeNullableText(request.WEBSITE),
                p_USERID = userId
            });

            await ClearSellersCacheAsync(companyCd);

            return await GetSellerInfoRowAsync(companyCd, sellerId);
        }

        private async Task<EInvoiceSellerInfo?> GetSellerInfoRowAsync(string companyCd, long sellerId)
        {
            const string sql = "CALL getEInvoiceSellerInfo(@p_COMPANY_CD, @p_SELLER_ID);";
            var rows = await _db.QueryAsync<EInvoiceSellerInfo>(Net_DB.Net_DB_Company, sql, new
            {
                p_COMPANY_CD = companyCd,
                p_SELLER_ID = sellerId
            });
            return rows.FirstOrDefault();
        }

        public Task ClearSellersCacheAsync(string companyCd)
            => _cache.ClearAsync(SellerCacheScope, companyCd);

        private async Task<IEnumerable<EInvoiceSellerInfo>> QuerySellersAsync(
            string companyCd,
            string khhdon,
            long? sellerId,
            long? xslId,
            string? keyword,
            bool includeInactive,
            bool includeXslContent,
            bool includeAllTemplates)
        {
            return await _db.QueryAsync<EInvoiceSellerInfo>(Net_DB.Net_DB_Company, GetSellersQuery, new
            {
                p_COMPANY_CD = companyCd,
                p_KHHDON = string.IsNullOrEmpty(khhdon) ? null : khhdon,
                p_SELLER_ID = sellerId ?? 0,
                p_XSL_ID = xslId ?? 0,
                p_KEYWORD = keyword,
                p_INCLUDE_INACTIVE = includeInactive ? 1 : 0,
                p_INCLUDE_XSL = includeXslContent ? 1 : 0,
                p_INCLUDE_ALL_TEMPLATES = includeAllTemplates ? 1 : 0
            });
        }

        private static string BuildCacheKey(string khhdon, long? sellerId, long? xslId, string? keyword, bool includeInactive, bool includeXslContent, bool includeAllTemplates)
        {
            return string.Join('|',
                $"khhdon={khhdon}",
                $"sellerId={sellerId ?? 0}",
                $"xslId={xslId ?? 0}",
                $"keyword={keyword ?? string.Empty}",
                $"includeInactive={(includeInactive ? 1 : 0)}",
                $"includeXsl={(includeXslContent ? 1 : 0)}",
                $"includeAllTemplates={(includeAllTemplates ? 1 : 0)}");
        }
    }
}
