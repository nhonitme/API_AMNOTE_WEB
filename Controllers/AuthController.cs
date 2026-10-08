using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;
using API_AMNOTE_WEB.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Claims;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : BaseApiController
    {
        private const string BffSessionScheme = "BffSession";
        private const string RefreshCookieName = "refresh_token";
        private const string LanguageCookieName = "amnote_lang";

        private readonly ILoginRepository _loginRepository;
        private readonly ILogger<AuthController> _logger;
        private readonly IPasswordCipher _passwordCipher;
        private readonly RefreshTokenStore _store;
        private readonly TokenService _tokenService;

        public AuthController(
            TokenService tokenService,
            RefreshTokenStore store,
            ILoginRepository loginRepository,
            IPasswordCipher passwordCipher,
            ILogger<AuthController> logger)
        {
            _tokenService = tokenService;
            _loginRepository = loginRepository;
            _store = store;
            _passwordCipher = passwordCipher;
            _logger = logger;
        }

        public record LoginRequest(string? CompanyCD, string UserName, string Password, string Lang);

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.UserName) || string.IsNullOrWhiteSpace(req.Password))
            {
                return BadRequest("UserName and Password are required");
            }

            var companyKey = Common.NormalizeNullableText(req.CompanyCD);
            if (companyKey == null)
            {
                return BadRequest("Company code or tax code is required");
            }

            var currentLogin = await _loginRepository.LoginAsync(companyKey, req.UserName.Trim());
            if (currentLogin == null)
            {
                _logger.LogWarning(
                    "Login failed because user was not found. CompanyKey: {CompanyKey}, UserName: {UserName}",
                    companyKey,
                    req.UserName);
                return Unauthorized("Invalid company, username or password");
            }

            if (!_passwordCipher.Matches(req.Password, currentLogin.PASSWD))
            {
                _logger.LogWarning(
                    "Login failed because password verification failed. CompanyKey: {CompanyKey}, UserName: {UserName}",
                    companyKey,
                    req.UserName);
                return Unauthorized("Invalid company, username or password");
            }

            var companies = (await _loginRepository.GetUserCompaniesAsync(currentLogin.USER_PK_ID)).ToList();
            if (!companies.Any())
            {
                _logger.LogWarning(
                    "Login failed because user has no active companies. UserPkId: {UserPkId}, UserName: {UserName}",
                    currentLogin.USER_PK_ID,
                    req.UserName);
                return Unauthorized("User is not assigned to any active company");
            }

            var loginCompanyCd = Common.NormalizeNullableText(currentLogin.COMPANY_CD) ?? companyKey;
            if (!companies.Any(item => item.COMPANY_CD.Equals(loginCompanyCd, StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogWarning(
                    "Login failed because login company is missing from membership. UserPkId: {UserPkId}, CompanyCd: {CompanyCd}",
                    currentLogin.USER_PK_ID,
                    loginCompanyCd);
                return Unauthorized("User is not assigned to the requested company");
            }

            var userId = currentLogin.USERID;
            var userNm = string.IsNullOrWhiteSpace(currentLogin.USERNM) ? userId : currentLogin.USERNM.Trim();
            var roles = BuildRoles(companies);
            var lang = Common.NormalizeLanguageCode(req.Lang);
            var refreshToken = _tokenService.CreateRefreshToken();
            var defaultCompanyCd = ResolveDefaultCompanyCd(companies, loginCompanyCd);

            await _store.SaveAsync(
                currentLogin.USER_PK_ID,
                userId,
                userNm,
                roles,
                refreshToken,
                DateTime.Now.AddDays(_tokenService.RefreshDays),
                defaultCompanyCd);

            var principal = _tokenService.CreatePrincipal(currentLogin.USER_PK_ID, userId, userNm, lang, roles);
            await HttpContext.SignInAsync(BffSessionScheme, principal, BuildAuthProperties());

            SetRefreshCookie(refreshToken, _tokenService.RefreshDays);
            SetLanguageCookie(lang, _tokenService.RefreshDays);

            return Success(BuildSessionDto(principal, companies, loginCompanyCd));
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            var oldRefresh = Request.Cookies[RefreshCookieName];
            if (string.IsNullOrWhiteSpace(oldRefresh))
            {
                return Unauthorized("Refresh token is missing");
            }

            var session = await _store.ValidateAsync(oldRefresh);
            if (session == null)
            {
                return Unauthorized("Refresh token is invalid");
            }

            if (session.UserPkId <= 0)
            {
                return Unauthorized("Refresh token is missing user identity");
            }

            var lang = Common.NormalizeLanguageCode(Request.Cookies[LanguageCookieName]);
            var newRefresh = _tokenService.CreateRefreshToken();

            await _store.RotateAsync(oldRefresh, newRefresh, DateTime.Now.AddDays(_tokenService.RefreshDays));

            var principal = _tokenService.CreatePrincipal(session.UserPkId, session.UserId, session.Username, lang, session.Roles);
            await HttpContext.SignInAsync(BffSessionScheme, principal, BuildAuthProperties());

            SetRefreshCookie(newRefresh, _tokenService.RefreshDays);
            SetLanguageCookie(lang, _tokenService.RefreshDays);

            return Success(await BuildSessionDtoAsync(principal));
        }

        [HttpGet("session")]
        public async Task<IActionResult> Session()
        {
            if (User?.Identity?.IsAuthenticated != true)
            {
                return Success(new AuthSessionDto { IsAuthenticated = false });
            }

            return Success(await BuildSessionDtoAsync(User));
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var refresh = Request.Cookies[RefreshCookieName];
            if (!string.IsNullOrWhiteSpace(refresh))
            {
                await _store.RevokeAsync(refresh);
            }

            await HttpContext.SignOutAsync(BffSessionScheme);
            Response.Cookies.Delete(RefreshCookieName, BuildRefreshCookieOptions(0));
            Response.Cookies.Delete(LanguageCookieName, BuildLanguageCookieOptions(0));

            return Success(new AuthSessionDto { IsAuthenticated = false });
        }

        private AuthenticationProperties BuildAuthProperties()
        {
            return new AuthenticationProperties
            {
                AllowRefresh = true,
                IsPersistent = true,
                IssuedUtc = DateTimeOffset.UtcNow,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(_tokenService.AccessMinutes)
            };
        }

        private static CookieOptions BuildRefreshCookieOptions(int days)
        {
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTimeOffset.UtcNow.AddDays(days),
                Path = "/api/auth"
            };
        }

        private static CookieOptions BuildLanguageCookieOptions(int days)
        {
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTimeOffset.UtcNow.AddDays(days),
                Path = "/"
            };
        }

        private void SetRefreshCookie(string token, int days)
        {
            Response.Cookies.Append(RefreshCookieName, token, BuildRefreshCookieOptions(days));
        }

        private void SetLanguageCookie(string lang, int days)
        {
            Response.Cookies.Append(LanguageCookieName, lang, BuildLanguageCookieOptions(days));
        }

        private async Task<AuthSessionDto> BuildSessionDtoAsync(ClaimsPrincipal? principal)
        {
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return new AuthSessionDto { IsAuthenticated = false };
            }

            var userPkId = ResolveUserPkId(principal);
            var companies = userPkId <= 0
                ? new List<UserCompanyAccess>()
                : (await _loginRepository.GetUserCompaniesAsync(userPkId)).ToList();

            return BuildSessionDto(principal, companies, null);
        }

        private static AuthSessionDto BuildSessionDto(
            ClaimsPrincipal? principal,
            IReadOnlyList<UserCompanyAccess> companies,
            string? preferredCompanyCd)
        {
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return new AuthSessionDto { IsAuthenticated = false };
            }

            var defaultCompanyCd = ResolveDefaultCompanyCd(companies, preferredCompanyCd);
            var companyScopedUserId = ResolveCompanyUserId(companies, defaultCompanyCd)
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? string.Empty;

            return new AuthSessionDto
            {
                IsAuthenticated = true,
                DefaultCompanyCd = defaultCompanyCd,
                UserPkId = ResolveUserPkId(principal),
                UserId = companyScopedUserId,
                Username = principal.FindFirst(ClaimTypes.Name)?.Value ?? companyScopedUserId,
                Lang = principal.FindFirst(AuthClaimTypes.Language)?.Value
                    ?? principal.FindFirst("Lang")?.Value
                    ?? "VIET",
                Roles = principal.FindAll(ClaimTypes.Role).Select(item => item.Value).ToArray(),
                Companies = companies.Select(MapCompanyDto).ToList()
            };
        }

        private static long ResolveUserPkId(ClaimsPrincipal principal)
        {
            var raw = principal.FindFirst(AuthClaimTypes.UserPkId)?.Value;
            if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userPkId) && userPkId > 0)
            {
                return userPkId;
            }

            return 0;
        }

        private static string? ResolveCompanyUserId(IReadOnlyList<UserCompanyAccess> companies, string? companyCd)
        {
            if (string.IsNullOrWhiteSpace(companyCd))
            {
                return companies.FirstOrDefault()?.USERID;
            }

            return companies
                .FirstOrDefault(item => item.COMPANY_CD.Equals(companyCd, StringComparison.OrdinalIgnoreCase))
                ?.USERID
                ?? companies.FirstOrDefault()?.USERID;
        }

        private static string[] BuildRoles(IEnumerable<UserCompanyAccess> companies)
        {
            return companies
                .Select(item => Common.NormalizeNullableText(item.ROLE_CODE))
                .Where(item => item != null)
                .Select(item => item!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string ResolveDefaultCompanyCd(IReadOnlyList<UserCompanyAccess> companies, string? preferredCompanyCd)
        {
            if (!string.IsNullOrWhiteSpace(preferredCompanyCd) &&
                companies.Any(item => item.COMPANY_CD.Equals(preferredCompanyCd, StringComparison.OrdinalIgnoreCase)))
            {
                return preferredCompanyCd.Trim();
            }

            return companies.FirstOrDefault(item => item.DEFAULT_YN == "1")?.COMPANY_CD
                ?? companies.FirstOrDefault()?.COMPANY_CD
                ?? string.Empty;
        }

        private static AuthCompanyDto MapCompanyDto(UserCompanyAccess company)
        {
            return new AuthCompanyDto
            {
                COMPANY_CD = company.COMPANY_CD,
                COMPANY_NM = company.COMPANY_NM,
                USERID = company.USERID,
                USERLV = company.USERLV,
                ROLE_CODE = company.ROLE_CODE ?? string.Empty,
                DEFAULT_YN = company.DEFAULT_YN
            };
        }
    }
}
