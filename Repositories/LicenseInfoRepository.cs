using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Repositories
{
    /// <summary>
    /// Read-only information for the current company. License administration is performed
    /// outside the customer-facing AMnote Web application.
    /// </summary>
    public sealed class LicenseInfoRepository
    {
        private readonly DapperExecutor _db;
        private readonly ICompanyInfoRepository _companyInfoRepository;
        private readonly IUserInfoRepository _userInfoRepository;

        public LicenseInfoRepository(
            DapperExecutor db,
            ICompanyInfoRepository companyInfoRepository,
            IUserInfoRepository userInfoRepository)
        {
            _db = db;
            _companyInfoRepository = companyInfoRepository;
            _userInfoRepository = userInfoRepository;
        }

        public async Task<LicenseInfo?> GetCurrentAsync(string companyCd)
        {
            // License data is tenant metadata and belongs in the manager database.
            // No client-supplied company code is accepted by the controller.
            const string licenseQuery = @"
                SELECT LICENSE_KEY, PLAN_CODE, LICENSE_TYPE, START_DATE, END_DATE,
                       MAX_USERS, MAX_CONCURRENT_USERS, LICENSE_STATUS
                FROM company_license
                WHERE COMPANY_CD = @COMPANY_CD
                LIMIT 1";

            var row = (await _db.QueryAsync<LicenseRow>(
                Net_DB.Net_DB_Manager, licenseQuery, new { COMPANY_CD = companyCd }))
                .FirstOrDefault();

            if (row == null)
                return null;

            // Vietnam does not use daylight saving time. Treat license dates as
            // Vietnam-local calendar dates, independent of the API server timezone.
            var today = DateTime.UtcNow.AddHours(7).Date;
            var daysRemaining = row.END_DATE.HasValue
                ? (int?)(row.END_DATE.Value.Date - today).TotalDays
                : null;

            var storedStatus = (row.LICENSE_STATUS ?? "ACTIVE").Trim().ToUpperInvariant();
            var status = storedStatus != "ACTIVE"
                ? storedStatus
                : daysRemaining < 0 ? "EXPIRED"
                : daysRemaining <= 30 ? "EXPIRING"
                : "ACTIVE";

            int? activeUsers = null;
            if (row.MAX_USERS.HasValue)
            {
                // Reuse the same company-scoped procedure as User Management, rather
                // than assuming a join schema between user_info and user_company.
                var users = await _userInfoRepository.GetUserInfoAsync(companyCd);
                activeUsers = users
                    .Where(x => x.ISDEL != "1" && x.IS_ACTIVE != "0" && !string.IsNullOrWhiteSpace(x.USERID))
                    .Select(x => x.USERID.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
            }

            const string historyQuery = @"
                SELECT ID, RENEWED_AT, OLD_END_DATE, NEW_END_DATE, NOTE
                FROM company_license_renewal
                WHERE COMPANY_CD = @COMPANY_CD
                ORDER BY RENEWED_AT DESC, ID DESC
                LIMIT 20";

            var history = (await _db.QueryAsync<LicenseRenewalHistory>(
                Net_DB.Net_DB_Manager, historyQuery, new { COMPANY_CD = companyCd })).ToList();

            var company = await _companyInfoRepository.GetCompanyInfoAsync(companyCd);

            return new LicenseInfo
            {
                COMPANY_CD = companyCd,
                COMPANY_NM = company?.COMPANY_NM,
                LICENSE_KEY_MASKED = MaskLicenseKey(row.LICENSE_KEY),
                PLAN_CODE = row.PLAN_CODE ?? string.Empty,
                LICENSE_TYPE = row.LICENSE_TYPE ?? string.Empty,
                START_DATE = row.START_DATE,
                END_DATE = row.END_DATE,
                MAX_USERS = row.MAX_USERS,
                ACTIVE_USERS = activeUsers,
                MAX_CONCURRENT_USERS = row.MAX_CONCURRENT_USERS,
                LICENSE_STATUS = status,
                DAYS_REMAINING = daysRemaining.HasValue ? Math.Max(0, daysRemaining.Value) : null,
                RENEWAL_HISTORY = history
            };
        }

        private static string MaskLicenseKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;

            var normalized = key.Trim();
            return normalized.Length <= 4
                ? "****"
                : "****-****-" + normalized.Substring(normalized.Length - 4);
        }

        private sealed class LicenseRow
        {
            public string? LICENSE_KEY { get; set; }
            public string? PLAN_CODE { get; set; }
            public string? LICENSE_TYPE { get; set; }
            public DateTime? START_DATE { get; set; }
            public DateTime? END_DATE { get; set; }
            public int? MAX_USERS { get; set; }
            public int? MAX_CONCURRENT_USERS { get; set; }
            public string? LICENSE_STATUS { get; set; }
        }
    }
}
