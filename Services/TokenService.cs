using API_AMNOTE_WEB.Helpers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace API_AMNOTE_WEB.Services
{
    public class TokenService
    {
        private readonly IConfiguration _config;

        public TokenService(IConfiguration config)
        {
            _config = config;
        }

        public int AccessMinutes => int.Parse(_config["Jwt:AccessTokenMinutes"] ?? "30");
        public int RefreshDays => int.Parse(_config["Jwt:RefreshTokenDays"] ?? "14");

        public IReadOnlyCollection<Claim> CreateClaims(long userPkId, string userId, string username, string lang, string[] roles)
        {
            var normalizedLang = Common.NormalizeLanguageCode(lang);
            var normalizedUserId = string.IsNullOrWhiteSpace(userId) ? string.Empty : userId.Trim();
            var normalizedUsername = string.IsNullOrWhiteSpace(username) ? normalizedUserId : username.Trim();
            var userPkIdText = userPkId.ToString();

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, normalizedUserId),
                new(JwtRegisteredClaimNames.UniqueName, normalizedUsername),
                new(ClaimTypes.NameIdentifier, normalizedUserId),
                new(ClaimTypes.Name, normalizedUsername),
                new(AuthClaimTypes.UserPkId, userPkIdText),
                new(AuthClaimTypes.Language, normalizedLang),
                new("Lang", normalizedLang)
            };

            foreach (var role in roles ?? Array.Empty<string>())
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            return claims;
        }

        public ClaimsPrincipal CreatePrincipal(long userPkId, string userId, string username, string lang, string[] roles)
        {
            var identity = new ClaimsIdentity(
                CreateClaims(userPkId, userId, username, lang, roles),
                CookieAuthenticationDefaults.AuthenticationScheme,
                ClaimTypes.Name,
                ClaimTypes.Role);

            return new ClaimsPrincipal(identity);
        }

        public string CreateAccessToken(long userPkId, string userId, string username, string lang, string[] roles)
        {
            var jwt = _config.GetSection("Jwt");
            var issuer = jwt["Issuer"];
            var audience = jwt["Audience"];
            var secret = jwt["Secret"]!;

            var claims = CreateClaims(userPkId, userId, username, lang, roles).ToList();
            claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: DateTime.Now,
                expires: DateTime.Now.AddMinutes(AccessMinutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string CreateRefreshToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes);
        }

        public static string Sha256Hex(string raw)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }
    }
}
