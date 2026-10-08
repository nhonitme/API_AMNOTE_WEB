using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using System.Collections.Generic;

namespace API_AMNOTE_WEB.Repositories
{
    public class OpeningBalanceRepository : IOpeningBalanceRepository
    {
        /// <summary>
        /// etcType = 5: tài khoản số dư đầu kỳ tài khoản.
        /// etcType = 6: tài khoản số dư đầu kỳ khách hàng.
        /// etcType = 7: tài khoản số dư đầu kỳ ngân hàng.
        /// etcType = 8: tài khoản số dư đầu kỳ đối tượng THCP (department/cost-object).
        /// </summary>
        private const string OpeningBalanceAccountEtcType = "5";
        private const string OpeningBalanceCustomerAccountEtcType = "6";
        private const string OpeningBalanceBankAccountEtcType = "7";
        private const string OpeningBalanceDepartmentAccountEtcType = "8";

        private readonly DapperExecutor _db;
        private readonly ISystemRepository _systemRepository;

        public OpeningBalanceRepository(DapperExecutor db, ISystemRepository systemRepository)
        {
            _db = db;
            _systemRepository = systemRepository;
        }

        public async Task<IEnumerable<BeforeStateDepartment>> GetDepartmentsAsync(string companyCd, string? openYmd = null, string? keyWORD = "")
        {
            const string query = "CALL get_before_states_department(@p_COMPANY_CD, @p_OPEN_YMD, @p_KEYWORD)";
            return await _db.QueryAsync<BeforeStateDepartment>(Net_DB.Net_DB_Company, query, new { p_COMPANY_CD = companyCd, p_OPEN_YMD = openYmd, p_KEYWORD = keyWORD });
        }

        public async Task<int> SaveDepartmentsAsync(
            List<BeforeStateDepartment> records,
            string? DBName = null,
            string? companyCd = null,
            string? userId = null)
        {
            if (records == null || records.Count == 0) return 0;

            companyCd = companyCd ?? Common.GetCompanyCode();
            userId = userId ?? Common.GetUserId();

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company, DBName);
            try
            {
                string proc = @"
                CALL save_before_states_department(
                    @p_ID,
                    @p_COMPANY_CD,
                    @p_OPEN_YMD,
                    @p_DEPARTMENT_ID,
                    @p_DEPARTMENT_NM,
                    @p_ACC_ID,
                    @p_ACC_CD,
                    @p_FC_TYPE,
                    @p_DEBIT,
                    @p_CREDIT,
                    @p_DEBIT_FC,
                    @p_CREDIT_FC,
                    @p_EXCHANGE_RATE,
                    @p_SUMMARY,
                    @p_NOTE,
                    @p_ROW_STATE,
                    @p_USER_ID
                );";

                foreach (var rec in records)
                {
                    rec.COMPANY_CD = companyCd;
                    var param = BuildBeforeStateDepartmentParam(rec, userId);

                    await session.ExecuteAsync(proc, param);
                }

                session.Commit();
                return records.Count;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<IEnumerable<BeforeStateCustomer>> GetCustomersAsync(string companyCd, string? openYmd = null, string? keyWORD = null)
        {
            const string query = "CALL get_before_states_customer(@p_COMPANY_CD, @p_OPEN_YMD, @p_KEYWORD)";
            return await _db.QueryAsync<BeforeStateCustomer>(Net_DB.Net_DB_Company, query, new { p_COMPANY_CD = companyCd, p_OPEN_YMD = openYmd, p_KEYWORD = keyWORD });
        }

        public async Task<int> SaveCustomersAsync(
            List<BeforeStateCustomer> records,
            string? DBName = null,
            string? companyCd = null,
            string? userId = null)
        {
            if (records == null || records.Count == 0) return 0;

            companyCd = companyCd ?? Common.GetCompanyCode();
            userId = userId ?? Common.GetUserId();

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company, DBName);
            try
            {
                string proc = @"
                CALL save_before_states_customer(
                    @p_ID,
                    @p_COMPANY_CD,
                    @p_OPEN_YMD,
                    @p_CUSTOMER_ID,
                    @p_ACC_CD,
                    @p_FC_TYPE,
                    @p_DEBIT,
                    @p_CREDIT,
                    @p_DEBIT_FC,
                    @p_CREDIT_FC,
                    @p_EXCHANGE_RATE,
                    @p_SUMMARY,
                    @p_NOTE,
                    @p_ROW_STATE,
                    @p_USER_ID
                );";

                foreach (var rec in records)
                {
                    rec.COMPANY_CD = companyCd;
                    var param = BuildBeforeStateCustomerParam(rec, userId);

                    await session.ExecuteAsync(proc, param);
                }

                session.Commit();
                return records.Count;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<IEnumerable<BeforeStateBank>> GetBanksAsync(string companyCd, string? openYmd = null, string? keyWORD = "")
        {
            const string query = "CALL get_before_states_bank(@p_COMPANY_CD, @p_OPEN_YMD, @p_KEYWORD)";
            return await _db.QueryAsync<BeforeStateBank>(Net_DB.Net_DB_Company, query, new { p_COMPANY_CD = companyCd, p_OPEN_YMD = openYmd, p_KEYWORD = keyWORD });
        }

        public async Task<int> SaveBanksAsync(
            List<BeforeStateBank> records,
            string? DBName = null,
            string? companyCd = null,
            string? userId = null)
        {
            if (records == null || records.Count == 0) return 0;

            companyCd = companyCd ?? Common.GetCompanyCode();
            userId = userId ?? Common.GetUserId();

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company, DBName);
            try
            {
                string proc = @"
                CALL save_before_states_bank(
                    @p_ID,
                    @p_COMPANY_CD,
                    @p_OPEN_YMD,
                    @p_BANK_ID,
                    @p_BANK_NM,
                    @p_BANK_ACCOUNT_NO,
                    @p_ACC_ID,
                    @p_ACC_CD,
                    @p_FC_TYPE,
                    @p_DEBIT,
                    @p_CREDIT,
                    @p_DEBIT_FC,
                    @p_CREDIT_FC,
                    @p_EXCHANGE_RATE,
                    @p_SUMMARY,
                    @p_NOTE,
                    @p_ROW_STATE,
                    @p_USER_ID
                );";

                foreach (var rec in records)
                {
                    rec.COMPANY_CD = companyCd;
                    var param = BuildBeforeStateBankParam(rec, userId);

                    await session.ExecuteAsync(proc, param);
                }

                session.Commit();
                return records.Count;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<IEnumerable<BeforeState>> GetBeforeStatesAsync(string companyCd, string? openYmd = null, string? keyWORD = "")
        {
            const string query = "CALL get_before_states(@p_COMPANY_CD, @p_OPEN_YMD, @p_KEYWORD)";
            return await _db.QueryAsync<BeforeState>(Net_DB.Net_DB_Company, query, new { p_COMPANY_CD = companyCd, p_OPEN_YMD = openYmd, p_KEYWORD = keyWORD });
        }

        public Task<IReadOnlyList<EtcInfo>> GetEligibleAccountOptionsAsync(
            string companyCd,
            string? lang = null,
            string? databaseName = null)
            => GetEligibleAccountOptionsByEtcTypeAsync(companyCd, OpeningBalanceAccountEtcType, lang, databaseName);

        public Task<IReadOnlyList<EtcInfo>> GetEligibleCustomerAccountOptionsAsync(
            string companyCd,
            string? lang = null,
            string? databaseName = null)
            => GetEligibleAccountOptionsByEtcTypeAsync(companyCd, OpeningBalanceCustomerAccountEtcType, lang, databaseName);

        public Task<IReadOnlyList<EtcInfo>> GetEligibleBankAccountOptionsAsync(
            string companyCd,
            string? lang = null,
            string? databaseName = null)
            => GetEligibleAccountOptionsByEtcTypeAsync(companyCd, OpeningBalanceBankAccountEtcType, lang, databaseName);

        public Task<IReadOnlyList<EtcInfo>> GetEligibleDepartmentAccountOptionsAsync(
            string companyCd,
            string? lang = null,
            string? databaseName = null)
            => GetEligibleAccountOptionsByEtcTypeAsync(companyCd, OpeningBalanceDepartmentAccountEtcType, lang, databaseName);

        private async Task<IReadOnlyList<EtcInfo>> GetEligibleAccountOptionsByEtcTypeAsync(
            string companyCd,
            string etcType,
            string? lang = null,
            string? databaseName = null)
        {
            var normalizedLang = Common.NormalizeLanguageCode(lang ?? Common.GetCurrentLanguage());
            var accounts = await _systemRepository.GetEtcInfoAsync(
                companyCd,
                etcType,
                normalizedLang,
                string.Empty,
                string.Empty,
                databaseName);

            return accounts.ToList();
        }

        public async Task<int> SaveBeforeStatesAsync(List<BeforeState> records,string ? DBName = null, string? companyCd = null, string? userId = null)
        {
            if (records == null || records.Count == 0) return 0;

            companyCd = companyCd ?? Common.GetCompanyCode();
            userId = userId ?? Common.GetUserId();

            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company, DBName);
            try
            {
                const string proc = @"
                CALL save_before_states(
                    @p_ID,
                    @p_COMPANY_CD,
                    @p_OPEN_YMD,
                    @p_ACC_CD,
                    @p_FC_TYPE,
                    @p_DEBIT,
                    @p_CREDIT,
                    @p_DEBIT_FC,
                    @p_CREDIT_FC,
                    @p_EXCHANGE_RATE,
                    @p_SUMMARY,
                    @p_NOTE,
                    @p_ROW_STATE,
                    @p_USER_ID
                );";

                foreach (var rec in records)
                {
                    rec.COMPANY_CD = companyCd;
                    var param = BuildBeforeStateParam(rec, userId);

                    await session.ExecuteAsync(proc, param);
                }

                session.Commit();
                return records.Count;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task<IEnumerable<OpeningBalanceSummary>> GetOpeningBalanceSummaryAsync(string companyCd, string? openYmd = null)
        {
            var language = Common.GetCurrentLanguage();

            const string query = "CALL get_opening_balance_summary(@p_COMPANY_CD, @p_OPEN_YMD, @p_LANGUAGE)";
            return await _db.QueryAsync<OpeningBalanceSummary>(Net_DB.Net_DB_Company, query, new { p_COMPANY_CD = companyCd, p_OPEN_YMD = openYmd, p_LANGUAGE = language });
        }

        public object BuildBeforeStateParam(BeforeState rec, string userId)
        {
            return new
            {
                p_ID = rec.ID,
                p_COMPANY_CD = rec.COMPANY_CD,
                p_OPEN_YMD = rec.OPEN_YMD,

                p_ACC_CD = rec.ACC_CD,

                p_FC_TYPE = string.IsNullOrWhiteSpace(rec.FC_TYPE) ? "VND" : rec.FC_TYPE,

                p_DEBIT = rec.DEBIT,
                p_CREDIT = rec.CREDIT,
                p_DEBIT_FC = rec.DEBIT_FC,
                p_CREDIT_FC = rec.CREDIT_FC,
                p_EXCHANGE_RATE = rec.EXCHANGE_RATE <= 0 ? 1m : rec.EXCHANGE_RATE,

                p_SUMMARY = rec.SUMMARY ?? string.Empty,
                p_NOTE = rec.NOTE ?? string.Empty,

                p_ROW_STATE = rec.ROW_STATE,
                p_USER_ID = userId
            };
        }

        public object BuildBeforeStateCustomerParam(BeforeStateCustomer rec, string userId)
        {
            return new
            {
                p_ID = rec.ID,
                p_COMPANY_CD = rec.COMPANY_CD,
                p_OPEN_YMD = rec.OPEN_YMD,

                p_CUSTOMER_ID = rec.CUSTOMER_ID,

                p_ACC_CD = rec.ACC_CD,

                p_FC_TYPE = string.IsNullOrWhiteSpace(rec.FC_TYPE) ? "VND" : rec.FC_TYPE,

                p_DEBIT = rec.DEBIT,
                p_CREDIT = rec.CREDIT,
                p_DEBIT_FC = rec.DEBIT_FC,
                p_CREDIT_FC = rec.CREDIT_FC,
                p_EXCHANGE_RATE = rec.EXCHANGE_RATE <= 0 ? 1m : rec.EXCHANGE_RATE,

                p_SUMMARY = rec.SUMMARY ?? string.Empty,
                p_NOTE = rec.NOTE ?? string.Empty,

                p_ROW_STATE = rec.ROW_STATE,
                p_USER_ID = userId
            };
        }
        public object BuildBeforeStateBankParam(BeforeStateBank rec, string userId)
        {
            var bankNm = FirstNonEmpty(
                rec.BANK_NM,
                rec.BANK_NM_VIET,
                rec.BANK_NM_ENG,
                rec.BANK_NM_KOR,
                rec.BANK_NM_CHINA);

            return new
            {
                p_ID = rec.ID,
                p_COMPANY_CD = rec.COMPANY_CD,
                p_OPEN_YMD = rec.OPEN_YMD,

                p_BANK_ID = rec.BANK_ID,
                p_BANK_NM = bankNm,
                p_BANK_ACCOUNT_NO = rec.BANK_ACCOUNT_NO ?? string.Empty,

                p_ACC_ID = rec.ACC_ID ?? 0,
                p_ACC_CD = rec.ACC_CD,

                p_FC_TYPE = string.IsNullOrWhiteSpace(rec.FC_TYPE) ? "VND" : rec.FC_TYPE,

                p_DEBIT = rec.DEBIT,
                p_CREDIT = rec.CREDIT,
                p_DEBIT_FC = rec.DEBIT_FC,
                p_CREDIT_FC = rec.CREDIT_FC,
                p_EXCHANGE_RATE = rec.EXCHANGE_RATE <= 0 ? 1m : rec.EXCHANGE_RATE,

                p_SUMMARY = rec.SUMMARY ?? string.Empty,
                p_NOTE = rec.NOTE ?? string.Empty,

                p_ROW_STATE = rec.ROW_STATE,
                p_USER_ID = userId
            };
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }
        public object BuildBeforeStateDepartmentParam(BeforeStateDepartment rec, string userId)
        {
            var departmentNm = FirstNonEmpty(
                rec.DEPARTMENT_NM,
                rec.DEP_NM_VIET,
                rec.DEP_NM_ENG,
                rec.DEP_NM_KOR,
                rec.DEP_NM_CHINA);

            return new
            {
                p_ID = rec.ID,
                p_COMPANY_CD = rec.COMPANY_CD,
                p_OPEN_YMD = rec.OPEN_YMD,

                p_DEPARTMENT_ID = rec.DEPARTMENT_ID,
                p_DEPARTMENT_NM = departmentNm,

                p_ACC_ID = rec.ACC_ID ?? 0,
                p_ACC_CD = rec.ACC_CD,

                p_FC_TYPE = string.IsNullOrWhiteSpace(rec.FC_TYPE) ? "VND" : rec.FC_TYPE,

                p_DEBIT = rec.DEBIT,
                p_CREDIT = rec.CREDIT,
                p_DEBIT_FC = rec.DEBIT_FC,
                p_CREDIT_FC = rec.CREDIT_FC,
                p_EXCHANGE_RATE = rec.EXCHANGE_RATE <= 0 ? 1m : rec.EXCHANGE_RATE,

                p_SUMMARY = rec.SUMMARY ?? string.Empty,
                p_NOTE = rec.NOTE ?? string.Empty,

                p_ROW_STATE = rec.ROW_STATE,
                p_USER_ID = userId
            };
        }

        /// <summary>
        /// Lấy ngày đầu kỳ kế toán của công ty từ sp_period_lock_fiscal_start_year_get.
        /// Lưu ý: store trả CARRYFORWARD_YMD dạng yyyyMMdd, không phải chỉ yyyy.
        /// Backend luôn lấy giá trị này từ DB, không dựa vào frontend.
        /// </summary>
        public async Task<int> GetFiscalStartYmdAsync(string companyCd, string? sDBName = null)
        {
            const string query = "CALL sp_period_lock_fiscal_start_year_get(@p_COMPANY_CD)";

            var rows = await _db.QueryAsync<PeriodLockFiscalStartYmdResult>(
                Net_DB.Net_DB_Company,
                query,
                new { p_COMPANY_CD = companyCd },
                sDBName
            );

            var row = rows.FirstOrDefault();
            return NormalizeFiscalStartYmd(row?.FiscalStartYmd);
        }

        private static int NormalizeFiscalStartYmd(string? fiscalStartValue)
        {
            var digits = new string((fiscalStartValue ?? string.Empty).Where(char.IsDigit).ToArray());

            if (digits.Length >= 8)
            {
                digits = digits.Substring(0, 8);

                if (DateTime.TryParseExact(
                        digits,
                        "yyyyMMdd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out _))
                {
                    return int.Parse(digits);
                }
            }

            return BuildDefaultFiscalStartYmd(DateTime.Now.Year);
        }

        private static int BuildDefaultFiscalStartYmd(int year)
        {
            return int.Parse($"{year}0101");
        }
    }
}
