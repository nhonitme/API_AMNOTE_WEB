using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using Dapper;
using Microsoft.Extensions.Caching.Memory;
using MySqlConnector;

namespace API_AMNOTE_WEB.Services
{
    public class CompanyDatabaseResolver : ICompanyDatabaseResolver
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<CompanyDatabaseResolver> _logger;
        private readonly int _commandTimeoutSeconds;

        public CompanyDatabaseResolver(
            IConfiguration configuration,
            IMemoryCache cache,
            ILogger<CompanyDatabaseResolver> logger)
        {
            _cache = cache;
            _logger = logger;
            _commandTimeoutSeconds = configuration.GetValue("Database:CommandTimeoutSeconds", 300);
        }

        public string ResolveDatabaseName(string companyCd)
        {
            var normalizedCompanyCd = NormalizeCompanyCd(companyCd);
            var cacheKey = BuildCacheKey(normalizedCompanyCd);

            return _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
                return LoadDatabaseName(normalizedCompanyCd);
            }) ?? throw new InvalidOperationException($"Database is not configured for company {normalizedCompanyCd}");
        }

        public async Task<string> ResolveDatabaseNameAsync(string companyCd)
        {
            var normalizedCompanyCd = NormalizeCompanyCd(companyCd);
            var cacheKey = BuildCacheKey(normalizedCompanyCd);

            if (_cache.TryGetValue<string>(cacheKey, out var cached) && !string.IsNullOrWhiteSpace(cached))
            {
                return cached;
            }

            var databaseName = await LoadDatabaseNameAsync(normalizedCompanyCd);
            _cache.Set(cacheKey, databaseName, TimeSpan.FromMinutes(30));
            return databaseName;
        }

        private string LoadDatabaseName(string companyCd)
        {
            using var connection = new MySqlConnection(Common.getManagerDBConnectStr(_commandTimeoutSeconds));
            var databaseName = connection.QueryFirstOrDefault<string>(
                "CALL get_company_database_name(@p_COMPANY_CD)",
                new { p_COMPANY_CD = companyCd });

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                _logger.LogWarning("Company database was not found. CompanyCd={CompanyCd}", companyCd);
                throw new InvalidOperationException($"Database is not configured for company {companyCd}");
            }

            return databaseName.Trim();
        }

        private async Task<string> LoadDatabaseNameAsync(string companyCd)
        {
            await using var connection = new MySqlConnection(Common.getManagerDBConnectStr(_commandTimeoutSeconds));
            var databaseName = await connection.QueryFirstOrDefaultAsync<string>(
                "CALL get_company_database_name(@p_COMPANY_CD)",
                new { p_COMPANY_CD = companyCd });

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                _logger.LogWarning("Company database was not found. CompanyCd={CompanyCd}", companyCd);
                throw new InvalidOperationException($"Database is not configured for company {companyCd}");
            }

            return databaseName.Trim();
        }

        private static string NormalizeCompanyCd(string companyCd)
        {
            var normalized = Common.NormalizeRequiredText(companyCd);
            if (normalized.Length == 0)
            {
                throw new ArgumentException("COMPANY_CD is required", nameof(companyCd));
            }

            return normalized;
        }

        private static string BuildCacheKey(string companyCd)
            => $"company-database:{companyCd.Trim().ToUpperInvariant()}";
    }
}
