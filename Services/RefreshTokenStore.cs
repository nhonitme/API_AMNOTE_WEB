using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using System.Globalization;
using System.Text.Json;

namespace API_AMNOTE_WEB.Services
{
    public class RefreshTokenSession
    {
        public DateTimeOffset ExpiresAtUtc { get; set; }
        public long UserPkId { get; set; }
        public string UserId { get; set; } = "";
        public string Username { get; set; } = "";
        public string Lang { get; set; } = "";
        public string[] Roles { get; set; } = Array.Empty<string>();
    }

    public class RefreshTokenStore
    {
        private readonly DapperExecutor _db;
        private readonly ICompanyDatabaseResolver _companyDatabaseResolver;

        public RefreshTokenStore(DapperExecutor db, ICompanyDatabaseResolver companyDatabaseResolver)
        {
            _db = db;
            _companyDatabaseResolver = companyDatabaseResolver;
        }

        public async Task SaveAsync(
            long userPkId,
            string userId,
            string username,
            string[] roles,
            string refreshRaw,
            DateTime expiresAtUtc,
            string companyCd)
        {
            var hash = TokenService.Sha256Hex(refreshRaw);
            var rolesJson = JsonSerializer.Serialize(roles ?? Array.Empty<string>());
            var normalizedCompanyCd = Common.NormalizeRequiredText(companyCd);
            var dbName = await _companyDatabaseResolver.ResolveDatabaseNameAsync(normalizedCompanyCd);
            var normalizedUserId = string.IsNullOrWhiteSpace(userId) ? string.Empty : userId.Trim();
            var normalizedUsername = string.IsNullOrWhiteSpace(username) ? normalizedUserId : username.Trim();

            // USER_NAME stores "userPkId|displayName" so refresh can rebuild claims without schema change.
            var userNamePayload = $"{userPkId.ToString(CultureInfo.InvariantCulture)}|{normalizedUsername}";

            await _db.ExecuteAsync(
                Net_DB.Net_DB_Manager,
                "CALL save_refresh_token_session(@p_USER_ID, @p_USER_NAME, @p_COMPANY_CD, @p_DB_NAME, @p_ROLES_JSON, @p_REFRESH_TOKEN_HASH, @p_CREATE_AT, @p_EXPIRES_AT)",
                new
                {
                    p_USER_ID = normalizedUserId,
                    p_USER_NAME = userNamePayload,
                    p_COMPANY_CD = normalizedCompanyCd,
                    p_DB_NAME = dbName,
                    p_ROLES_JSON = rolesJson,
                    p_REFRESH_TOKEN_HASH = hash,
                    p_CREATE_AT = DateTime.UtcNow,
                    p_EXPIRES_AT = expiresAtUtc
                });
        }

        public async Task<RefreshTokenSession?> ValidateAsync(string refreshRaw)
        {
            if (!refreshRaw.StartsWith(DailySession.RefreshPrefix, StringComparison.Ordinal)) return null;
            var hash = TokenService.Sha256Hex(refreshRaw);

            var rows = await _db.QueryAsync<dynamic>(
                Net_DB.Net_DB_Manager,
                "CALL get_refresh_token_session(@p_REFRESH_TOKEN_HASH)",
                new { p_REFRESH_TOKEN_HASH = hash });

            var row = rows.FirstOrDefault();
            if (row == null) return null;
            if (row.REVOKED_AT != null) return null;
            if (row.REPLACED_BY_HASH != null) return null;
            var deadline = new DateTimeOffset(DateTime.SpecifyKind((DateTime)row.EXPIRES_AT, DateTimeKind.Utc));
            if (deadline <= DateTimeOffset.UtcNow) return null;
            if (deadline > DailySession.NextMidnight(DateTimeOffset.UtcNow)) return null;

            var roles = Array.Empty<string>();
            try
            {
                if (row.ROLES_JSON != null)
                    roles = JsonSerializer.Deserialize<string[]>((string)row.ROLES_JSON) ?? Array.Empty<string>();
            }
            catch { }

            var userId = (string)row.USER_ID;
            var userNameRaw = (string)row.USER_NAME;
            var userPkId = 0L;
            var username = userId;

            if (!string.IsNullOrWhiteSpace(userNameRaw))
            {
                var separatorIndex = userNameRaw.IndexOf('|');
                if (separatorIndex > 0
                    && long.TryParse(userNameRaw[..separatorIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedPk)
                    && parsedPk > 0)
                {
                    userPkId = parsedPk;
                    username = userNameRaw[(separatorIndex + 1)..];
                    if (string.IsNullOrWhiteSpace(username))
                    {
                        username = userId;
                    }
                }
                else if (long.TryParse(userNameRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var legacyPk) && legacyPk > 0)
                {
                    userPkId = legacyPk;
                }
                else
                {
                    username = userNameRaw;
                }
            }

            return new RefreshTokenSession
            {
                ExpiresAtUtc = deadline,
                UserPkId = userPkId,
                UserId = userId,
                Username = username,
                Roles = roles
            };
        }

        public async Task<bool> RotateAsync(string oldRefreshRaw, string newRefreshRaw, DateTime newExpiresUtc)
        {
            var oldHash = TokenService.Sha256Hex(oldRefreshRaw);
            var newHash = TokenService.Sha256Hex(newRefreshRaw);

            var result = await _db.QueryAsync<int>(
                Net_DB.Net_DB_Manager,
                "CALL rotate_refresh_token_session(@p_OLD_REFRESH_TOKEN_HASH, @p_NEW_REFRESH_TOKEN_HASH, @p_CREATE_AT, @p_EXPIRES_AT)",
                new
                {
                    p_OLD_REFRESH_TOKEN_HASH = oldHash,
                    p_NEW_REFRESH_TOKEN_HASH = newHash,
                    p_CREATE_AT = DateTime.UtcNow,
                    p_EXPIRES_AT = newExpiresUtc
                });
            var results = result.ToArray();
            var rotated = results.FirstOrDefault() == 1;
            if (!rotated)
            {
                // Verify committed state when the deployed procedure returns no result row.
                try
                {
                    var state = await _db.QueryAsync<dynamic>(
                        Net_DB.Net_DB_Manager,
                        @"SELECT UTC_TIMESTAMP() AS DB_NOW_UTC,
                            EXPIRES_AT, (EXPIRES_AT = @deadline) AS DEADLINE_MATCHES,
                            (EXPIRES_AT > UTC_TIMESTAMP()) AS DB_CONSIDERS_VALID,
                            (REVOKED_AT IS NOT NULL) AS IS_REVOKED,
                            (REPLACED_BY_HASH IS NOT NULL) AS IS_REPLACED,
                            (REPLACED_BY_HASH = @newHash) AS REPLACED_BY_THIS_REQUEST,
                            EXISTS(SELECT 1 FROM refresh_token_session n WHERE n.REFRESH_TOKEN_HASH = @newHash) AS NEW_TOKEN_EXISTS,
                            EXISTS(SELECT 1 FROM refresh_token_session n
                                WHERE n.REFRESH_TOKEN_HASH = @newHash
                                  AND n.REVOKED_AT IS NULL AND n.REPLACED_BY_HASH IS NULL
                                  AND n.EXPIRES_AT = @deadline AND n.EXPIRES_AT > UTC_TIMESTAMP()) AS NEW_TOKEN_VALID
                          FROM refresh_token_session WHERE REFRESH_TOKEN_HASH = @oldHash LIMIT 1",
                        new { deadline = newExpiresUtc, oldHash, newHash });
                    var row = state.FirstOrDefault();
                    // Some deployed procedures commit rotation without returning a result row.
                    // Accept only an empty result whose committed state belongs to this exact request.
                    rotated = results.Length == 0 && row != null
                        && Convert.ToInt32(row.IS_REVOKED) == 0
                        && Convert.ToInt32(row.DEADLINE_MATCHES) == 1
                        && Convert.ToInt32(row.REPLACED_BY_THIS_REQUEST) == 1
                        && Convert.ToInt32(row.NEW_TOKEN_VALID) == 1;
                }
                catch
                {
                }
            }
            return rotated;
        }

        public async Task RevokeAsync(string refreshRaw)
        {
            var hash = TokenService.Sha256Hex(refreshRaw);
            await _db.ExecuteAsync(
                Net_DB.Net_DB_Manager,
                "CALL revoke_refresh_token_session(@p_REFRESH_TOKEN_HASH, @p_NOW)",
                new { p_REFRESH_TOKEN_HASH = hash, p_NOW = DateTime.UtcNow });
        }
    }
}
