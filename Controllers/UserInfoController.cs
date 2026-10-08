using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    public class UserInfoController : BaseApiController
    {
        private const string MenuCode = "MD_USER";
        private readonly IUserInfoRepository _repository;
        private readonly IPasswordCipher _passwordCipher;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<UserInfoController> _logger;

        public UserInfoController(
            IUserInfoRepository repository,
            IPasswordCipher passwordCipher,
            IWebHostEnvironment environment,
            ILogger<UserInfoController> logger)
        {
            _repository = repository;
            _passwordCipher = passwordCipher;
            _environment = environment;
            _logger = logger;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetUserInfo(
            [FromQuery(Name = "userPkId")] long? userPkId = null,
            [FromQuery(Name = "USER_PK_ID")] long? legacyUserPkId = null,
            [FromQuery(Name = "userId")] string? userId = null,
            [FromQuery(Name = "USERID")] string? legacyUserId = null)
        {
            await EnsurePermissionAsync(MenuCode, "VIEW");

            var companyCd = Common.GetCompanyCode();
            var targetUserPkId = userPkId ?? legacyUserPkId;
            var targetUserId = Common.NormalizeNullableText(userId) ?? Common.NormalizeNullableText(legacyUserId);
            var data = (await _repository.GetUserInfoAsync(companyCd, targetUserPkId, targetUserId))
                .Select(MapUserInfoDto)
                .ToList();

            if ((targetUserPkId.HasValue && targetUserPkId.Value > 0) || !string.IsNullOrWhiteSpace(targetUserId))
            {
                if (!data.Any())
                {
                    return NotFound("User not found");
                }
            }

            return Success(data);
        }

        [HttpGet("check-exists")]
        [Authorize]
        public async Task<IActionResult> CheckExists(
            [FromQuery(Name = "userPkId")] long? userPkId = null,
            [FromQuery(Name = "USER_PK_ID")] long? legacyUserPkId = null,
            [FromQuery(Name = "userId")] string? userId = null,
            [FromQuery(Name = "USERID")] string? legacyUserId = null)
        {
            await EnsurePermissionAsync(MenuCode, "VIEW");

            var targetUserId = Common.NormalizeNullableText(userId) ?? Common.NormalizeNullableText(legacyUserId);
            if (string.IsNullOrWhiteSpace(targetUserId))
            {
                return Success(false);
            }

            var companyCd = Common.GetCompanyCode();
            var exists = await _repository.UserIdExistsAsync(companyCd, targetUserId, userPkId ?? legacyUserPkId);
            return Success(exists);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateUserInfo([FromBody] UserInfoRequest? request, [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "ADD");
            var currentLang = string.IsNullOrWhiteSpace(lang) ? Common.GetCurrentLanguage() : lang.Trim();
            if (request == null)
            {
                return ValidationError("Request body must be provided");
            }

            var normalizedUserId = Common.NormalizeRequiredText(request.USERID);
            if (string.IsNullOrWhiteSpace(normalizedUserId))
            {
                return ValidationError((await Common.getLanguage("USERID", currentLang)) + " " + (await Common.getLanguage("REQUIRED", currentLang)));
            }

            if (string.IsNullOrWhiteSpace(Common.NormalizeRequiredText(request.USERNM)))
            {
                return ValidationError((await Common.getLanguage("USERNM", currentLang)) + " " + (await Common.getLanguage("REQUIRED", currentLang)));
            }

            if (string.IsNullOrWhiteSpace(Common.NormalizeRequiredText(request.PASSWD)))
            {
                return ValidationError((await Common.getLanguage("PASSWD", currentLang)) + " " + (await Common.getLanguage("REQUIRED", currentLang)));
            }

            var companyCd = Common.GetCompanyCode();
            if (await _repository.UserIdExistsAsync(companyCd, normalizedUserId))
            {
                return BusinessError((await Common.getLanguage("USERID", currentLang)) + " " + (await Common.getLanguage("ALREADY_EXISTS", currentLang)));
            }

            var payload = BuildCreateRequest(request, normalizedUserId);
            var result = await _repository.SetUserInfoAsync(companyCd, Common.GetUserId(), payload);
            if (result <= 0)
            {
                return ServerError((await Common.getLanguage("USERID", currentLang)) + " " + (await Common.getLanguage("CREATE_FAILED", currentLang)));
            }

            var created = (await _repository.GetUserInfoAsync(companyCd, null, payload.USERID)).FirstOrDefault();
            var response = created != null
                ? MapUserInfoDto(created)
                : new UserInfoDto
                {
                    COMPANY_CD = companyCd,
                    USERID = payload.USERID ?? string.Empty,
                    USERNM = payload.USERNM ?? string.Empty,
                    EMAIL = payload.EMAIL,
                    MOBILE_NO = payload.MOBILE_NO,
                    AVATAR_URL = payload.AVATAR_URL,
                    USERLV = payload.USERLV,
                    ROLE_CODE = payload.ROLE_CODE,
                    DEFAULT_YN = payload.DEFAULT_YN ?? "0",
                    IS_ACTIVE = payload.IS_ACTIVE ?? "1",
                    ISDEL = payload.ISDEL ?? "0"
                };

            return Created(response, (await Common.getLanguage("USERID", currentLang)) + " " + (await Common.getLanguage("CREATE_SUCCESS", currentLang)));
        }

        [HttpPut("{id:long}")]
        [Authorize]
        public async Task<IActionResult> UpdateUserInfo([FromRoute] long id, [FromBody] UserInfoRequest? request, [FromQuery] string? lang = null)
        {
            await EnsurePermissionAsync(MenuCode, "EDIT");
            var currentLang = string.IsNullOrWhiteSpace(lang) ? Common.GetCurrentLanguage() : lang.Trim();
            if (id <= 0)
            {
                return ValidationError("USER_PK_ID is required");
            }

            if (request == null)
            {
                return ValidationError("Request body must be provided");
            }

            var companyCd = Common.GetCompanyCode();
            var existing = (await _repository.GetUserInfoAsync(companyCd, id, null)).FirstOrDefault();
            if (existing == null)
            {
                return NotFound((await Common.getLanguage("USERID", currentLang)) + " " + (await Common.getLanguage("NOT_FOUND", currentLang)));
            }

            var nextUserName = request.USERNM == null ? existing.USERNM : Common.NormalizeRequiredText(request.USERNM);
            if (string.IsNullOrWhiteSpace(nextUserName))
            {
                return ValidationError((await Common.getLanguage("USERNM", currentLang)) + " " + (await Common.getLanguage("REQUIRED", currentLang)));
            }

            var nextUserId = request.USERID == null ? existing.USERID : Common.NormalizeRequiredText(request.USERID);
            if (string.IsNullOrWhiteSpace(nextUserId))
            {
                return ValidationError((await Common.getLanguage("USERID", currentLang)) + " " + (await Common.getLanguage("REQUIRED", currentLang)));
            }

            if (await _repository.UserIdExistsAsync(companyCd, nextUserId, id))
            {
                return BusinessError((await Common.getLanguage("USERID", currentLang)) + " " + (await Common.getLanguage("ALREADY_EXISTS", currentLang)));
            }

            var payload = BuildUpdateRequest(existing, request);
            var result = await _repository.SetUserInfoAsync(companyCd, Common.GetUserId(), payload);
            if (result <= 0)
            {
                return ServerError((await Common.getLanguage("USERID", currentLang)) + " " + (await Common.getLanguage("UPDATE_FAILED", currentLang)));
            }

            var updated = (await _repository.GetUserInfoAsync(companyCd, id, null)).FirstOrDefault();
            return Updated(MapUserInfoDto(updated ?? existing), (await Common.getLanguage("USERID", currentLang)) + " " + (await Common.getLanguage("UPDATE_SUCCESS", currentLang)));
        }

        [HttpPost("bulk-delete")]
        [Authorize]
        public async Task<IActionResult> BulkDeleteUserInfo([FromBody] DeleteUserInfosRequest? request)
        {
            if (request == null || request.UserPkIds == null || !request.UserPkIds.Any())
            {
                return ValidationError("UserPkIds is required");
            }

            var companyCd = Common.GetCompanyCode();
            var currentUserPkId = Common.GetUserPkId();
            var userPkIds = request.UserPkIds.Distinct().Where(id => id > 0).ToList();

            if (currentUserPkId > 0 && userPkIds.Contains(currentUserPkId))
            {
                return BusinessError("You cannot delete your own account.");
            }

            if (!userPkIds.Any())
            {
                return ValidationError("UserPkIds is required");
            }

            var result = await _repository.DeleteUserInfoAsync(companyCd, Common.GetUserId(), userPkIds);
            return Success(new { deleted = result }, "Deleted successfully");
        }

        [HttpPost("{id:long}/avatar")]
        [Authorize]
        [RequestSizeLimit(2 * 1024 * 1024)]
        public async Task<IActionResult> UploadUserAvatar(
            [FromRoute] long id,
            IFormFile? file,
            CancellationToken cancellationToken)
        {
            await EnsurePermissionAsync(MenuCode, "EDIT");

            if (id <= 0)
            {
                return ValidationError("USER_PK_ID is required");
            }

            if (file == null || file.Length <= 0)
            {
                return ValidationError("Image file is required");
            }

            var companyCd = Common.GetCompanyCode();
            var existing = (await _repository.GetUserInfoAsync(companyCd, id, null)).FirstOrDefault();
            if (existing == null)
            {
                return NotFound("User not found");
            }

            try
            {
                var publicPath = await UserAvatarImageStorage.SaveAsync(
                    _environment,
                    companyCd,
                    id,
                    file,
                    cancellationToken);

                var previousPath = existing.AVATAR_URL;
                var payload = BuildUpdateRequest(
                    existing,
                    new UserInfoRequest
                    {
                        AVATAR_URL = publicPath
                    });

                var result = await _repository.SetUserInfoAsync(companyCd, Common.GetUserId(), payload);
                if (result <= 0)
                {
                    UserAvatarImageStorage.TryDeleteStoredPath(_environment, publicPath);
                    return ServerError("Update failed");
                }

                if (!string.Equals(previousPath, publicPath, StringComparison.OrdinalIgnoreCase))
                {
                    UserAvatarImageStorage.TryDeleteStoredPath(_environment, previousPath);
                }

                var updated = (await _repository.GetUserInfoAsync(companyCd, id, null)).FirstOrDefault();
                return Updated(MapUserInfoDto(updated ?? existing), "Uploaded successfully");
            }
            catch (InvalidOperationException ex)
            {
                return ValidationError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload user avatar failed. UserPkId={UserPkId}", id);
                return ServerError("Upload failed");
            }
        }

        [HttpGet("{id:long}/avatar-file")]
        [Authorize]
        public async Task<IActionResult> GetUserAvatarFileById(
            [FromRoute] long id,
            CancellationToken cancellationToken)
        {
            await EnsurePermissionAsync(MenuCode, "VIEW");

            if (id <= 0)
            {
                return ValidationError("USER_PK_ID is required");
            }

            var companyCd = Common.GetCompanyCode();
            var existing = (await _repository.GetUserInfoAsync(companyCd, id, null)).FirstOrDefault();
            if (existing == null)
            {
                return NotFound("User not found");
            }

            var storedPath = Common.NormalizeNullableText(existing.AVATAR_URL);
            if (storedPath == null)
            {
                return NotFound("Avatar image not found");
            }

            return await LoadAvatarImageAsync(storedPath, cancellationToken);
        }

        private async Task<IActionResult> LoadAvatarImageAsync(string storedPath, CancellationToken cancellationToken)
        {
            try
            {
                byte[]? bytes = null;
                var contentType = "application/octet-stream";
                await Task.Run(
                    () =>
                    {
                        if (UserAvatarImageStorage.TryGetImageBytes(
                                _environment,
                                storedPath,
                                out var loaded,
                                out var type))
                        {
                            bytes = loaded;
                            contentType = type;
                        }
                    },
                    cancellationToken);

                if (bytes == null || bytes.Length == 0)
                {
                    return NotFound("Avatar image not found");
                }

                Response.Headers.CacheControl = "private, max-age=300";
                return File(bytes, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Load user avatar failed. Path={Path}", storedPath);
                return ServerError("Cannot load avatar image");
            }
        }

        private static UserInfoDto MapUserInfoDto(UserInfo user)
        {
            return new UserInfoDto
            {
                USER_PK_ID = user.USER_PK_ID,
                USER_COMPANY_ID = user.USER_COMPANY_ID,
                COMPANY_CD = user.COMPANY_CD,
                USERID = user.USERID,
                USERNM = user.USERNM,
                EMAIL = user.EMAIL,
                MOBILE_NO = user.MOBILE_NO,
                AVATAR_URL = user.AVATAR_URL,
                USERLV = user.USERLV,
                ROLE_CODE = user.ROLE_CODE,
                DEFAULT_YN = Common.NormalizeFlagString(user.DEFAULT_YN, "0"),
                IS_ACTIVE = Common.NormalizeFlagString(user.IS_ACTIVE, "1"),
                ISDEL = Common.NormalizeFlagString(user.ISDEL, "0"),
            };
        }

        private UserInfoRequest BuildCreateRequest(UserInfoRequest request, string normalizedUserId)
        {
            var currentUserId = Common.GetUserId();

            return new UserInfoRequest
            {
                USER_PK_ID = 0,
                USERID = normalizedUserId,
                PASSWD = _passwordCipher.Encrypt(Common.NormalizeRequiredText(request.PASSWD)),
                USERNM = Common.NormalizeRequiredText(request.USERNM),
                EMAIL = Common.NormalizeNullableText(request.EMAIL),
                MOBILE_NO = Common.NormalizeNullableText(request.MOBILE_NO),
                AVATAR_URL = Common.NormalizeNullableText(request.AVATAR_URL),
                USERLV = Common.NormalizeUserLevel(request.USERLV),
                ROLE_CODE = Common.DeriveRoleCode(Common.NormalizeUserLevel(request.USERLV)),
                DEFAULT_YN = Common.NormalizeFlagString(request.DEFAULT_YN, "0"),
                IS_ACTIVE = Common.NormalizeFlagString(request.IS_ACTIVE, "1"),
                ISDEL = Common.NormalizeFlagString(request.ISDEL, "0"),
                CREATE_BY = currentUserId,
                UPDATE_BY = currentUserId,
                CREATE_AT = DateTime.Now,
                UPDATE_AT = DateTime.Now
            };
        }

        private UserInfoRequest BuildUpdateRequest(UserInfo existing, UserInfoRequest request)
        {
            return new UserInfoRequest
            {
                USER_PK_ID = existing.USER_PK_ID,
                USER_COMPANY_ID = existing.USER_COMPANY_ID,
                USERID = request.USERID == null ? existing.USERID : Common.NormalizeRequiredText(request.USERID),
                PASSWD = string.IsNullOrWhiteSpace(request.PASSWD)
                    ? existing.PASSWD
                    : _passwordCipher.Encrypt(Common.NormalizeRequiredText(request.PASSWD)),
                USERNM = request.USERNM == null ? existing.USERNM : Common.NormalizeRequiredText(request.USERNM),
                EMAIL = request.EMAIL == null ? existing.EMAIL : Common.NormalizeNullableText(request.EMAIL),
                MOBILE_NO = request.MOBILE_NO == null ? existing.MOBILE_NO : Common.NormalizeNullableText(request.MOBILE_NO),
                AVATAR_URL = request.AVATAR_URL == null ? existing.AVATAR_URL : Common.NormalizeNullableText(request.AVATAR_URL),
                USERLV = Common.NormalizeUserLevel(request.USERLV, existing.USERLV),
                ROLE_CODE = Common.DeriveRoleCode(Common.NormalizeUserLevel(request.USERLV, existing.USERLV)),
                DEFAULT_YN = request.DEFAULT_YN == null ? Common.NormalizeFlagString(existing.DEFAULT_YN, "0") : Common.NormalizeFlagString(request.DEFAULT_YN, "0"),
                IS_ACTIVE = request.IS_ACTIVE == null ? Common.NormalizeFlagString(existing.IS_ACTIVE, "1") : Common.NormalizeFlagString(request.IS_ACTIVE, "1"),
                ISDEL = request.ISDEL == null ? Common.NormalizeFlagString(existing.ISDEL, "0") : Common.NormalizeFlagString(request.ISDEL, "0"),
                CREATE_BY = existing.CREATE_BY,
                UPDATE_BY = Common.GetUserId(),
                CREATE_AT = existing.CREATE_AT,
                UPDATE_AT = DateTime.Now
            };
        }
    }
}
