using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mail;

namespace API_AMNOTE_WEB.Controllers
{
    [ApiController]
    public class UserProfileController : BaseApiController
    {
        private readonly IUserProfileRepository _repository;
        private readonly IUserInfoRepository _userInfoRepository;
        private readonly IPasswordCipher _passwordCipher;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<UserProfileController> _logger;

        public UserProfileController(
            IUserProfileRepository repository,
            IUserInfoRepository userInfoRepository,
            IPasswordCipher passwordCipher,
            IWebHostEnvironment environment,
            ILogger<UserProfileController> logger)
        {
            _repository = repository;
            _userInfoRepository = userInfoRepository;
            _passwordCipher = passwordCipher;
            _environment = environment;
            _logger = logger;
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> GetMyProfile()
        {
            var companyCd = Common.GetCompanyCode();
            var userId = Common.GetUserId();
            var profile = await _repository.GetUserProfileAsync(companyCd, userId);

            if (profile == null)
            {
                return NotFound("Profile not found");
            }

            return Success(MapUserProfileDto(profile));
        }

        [HttpPut("me")]
        [Authorize]
        public async Task<IActionResult> UpdateMyProfile([FromBody] UserProfileUpdateRequest? request, [FromQuery] string? lang = null)
        {
            var currentLang = string.IsNullOrWhiteSpace(lang) ? Common.GetCurrentLanguage() : lang.Trim();
            if (request == null)
            {
                return ValidationError("Request body must be provided");
            }

            var companyCd = Common.GetCompanyCode();
            var userId = Common.GetUserId();
            var existing = await _repository.GetUserProfileAsync(companyCd, userId);
            if (existing == null)
            {
                return NotFound((await Common.getLanguage("USERID", currentLang)) + " " + (await Common.getLanguage("NOT_FOUND", currentLang)));
            }

            var normalizedUserName = Common.NormalizeRequiredText(request.USERNM);
            if (string.IsNullOrWhiteSpace(normalizedUserName))
            {
                return ValidationError((await Common.getLanguage("USERNM", currentLang)) + " " + (await Common.getLanguage("REQUIRED", currentLang)));
            }

            var normalizedEmail = Common.NormalizeNullableText(request.EMAIL);
            if (!string.IsNullOrWhiteSpace(normalizedEmail) && !Common.IsValidEmail(normalizedEmail))
            {
                return ValidationError((await Common.getLanguage("EMAIL", currentLang)) + " is invalid");
            }

            var payload = BuildUpdateRequest(request);
            var result = await _repository.UpdateUserProfileAsync(companyCd, userId, payload);
            if (result <= 0)
            {
                return ServerError((await Common.getLanguage("USERNM", currentLang)) + " " + (await Common.getLanguage("UPDATE_FAILED", currentLang)));
            }

            var updated = await _repository.GetUserProfileAsync(companyCd, userId);
            return Updated(
                MapUserProfileDto(updated ?? existing),
                (await Common.getLanguage("USERNM", currentLang)) + " " + (await Common.getLanguage("UPDATE_SUCCESS", currentLang)));
        }

        [HttpPut("me/password")]
        [Authorize]
        public async Task<IActionResult> ChangeMyPassword([FromBody] ChangeUserProfilePasswordRequest? request, [FromQuery] string? lang = null)
        {
            var currentLang = string.IsNullOrWhiteSpace(lang) ? Common.GetCurrentLanguage() : lang.Trim();
            if (request == null)
            {
                return ValidationError("Request body must be provided");
            }

            var currentPassword = Common.NormalizeRequiredText(request.CURRENT_PASSWORD);
            var newPassword = Common.NormalizeRequiredText(request.NEW_PASSWORD);
            var confirmPassword = Common.NormalizeRequiredText(request.CONFIRM_PASSWORD);

            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                return ValidationError("Current password is required");
            }

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                return ValidationError((await Common.getLanguage("PASSWD", currentLang)) + " " + (await Common.getLanguage("REQUIRED", currentLang)));
            }

            if (newPassword.Length < 6)
            {
                return ValidationError("New password must be at least 6 characters");
            }

            if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
            {
                return ValidationError("Confirm password does not match");
            }

            if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
            {
                return BusinessError("New password must be different from the current password");
            }

            var companyCd = Common.GetCompanyCode();
            var userId = Common.GetUserId();
            var userInfo = (await _userInfoRepository.GetUserInfoAsync(companyCd, null, userId)).FirstOrDefault();
            if (userInfo == null)
            {
                return NotFound((await Common.getLanguage("USERID", currentLang)) + " " + (await Common.getLanguage("NOT_FOUND", currentLang)));
            }

            if (!_passwordCipher.Matches(currentPassword, userInfo.PASSWD))
            {
                return BusinessError("Current password is invalid");
            }

            var result = await _repository.ChangePasswordAsync(
                companyCd,
                userId,
                userId,
                _passwordCipher.Encrypt(newPassword));

            if (result <= 0)
            {
                return ServerError((await Common.getLanguage("PASSWD", currentLang)) + " " + (await Common.getLanguage("UPDATE_FAILED", currentLang)));
            }

            return Updated(new { USERID = userId }, (await Common.getLanguage("PASSWD", currentLang)) + " " + (await Common.getLanguage("UPDATE_SUCCESS", currentLang)));
        }

        [HttpPost("me/avatar")]
        [Authorize]
        [RequestSizeLimit(2 * 1024 * 1024)]
        public async Task<IActionResult> UploadMyAvatar(IFormFile? file, CancellationToken cancellationToken)
        {
            if (file == null || file.Length <= 0)
            {
                return ValidationError("Image file is required");
            }

            var companyCd = Common.GetCompanyCode();
            var userId = Common.GetUserId();
            var existing = await _repository.GetUserProfileAsync(companyCd, userId);
            if (existing == null || existing.USER_PK_ID <= 0)
            {
                return NotFound("Profile not found");
            }

            try
            {
                var publicPath = await UserAvatarImageStorage.SaveAsync(
                    _environment,
                    companyCd,
                    existing.USER_PK_ID,
                    file,
                    cancellationToken);

                var previousPath = existing.AVATAR_URL;
                var payload = BuildUpdateRequest(new UserProfileUpdateRequest
                {
                    USERNM = existing.USERNM,
                    EMPLOYEE_CD = existing.EMPLOYEE_CD,
                    EMPLOYEE_NM = existing.EMPLOYEE_NM,
                    EMPLOYEE_NM_ENG = existing.EMPLOYEE_NM_ENG,
                    DEPARTMENT_ID = existing.DEPARTMENT_ID,
                    DEPARTMENT_CD = existing.DEPARTMENT_CD,
                    POSITION_ID = existing.POSITION_ID,
                    POSITION_CD = existing.POSITION_CD,
                    BRANCH_ID = existing.BRANCH_ID,
                    BRANCH_CD = existing.BRANCH_CD,
                    EMAIL = existing.EMAIL,
                    MOBILE_NO = existing.MOBILE_NO,
                    TEL_NO = existing.TEL_NO,
                    ADDRESS = existing.ADDRESS,
                    BIRTHDAY = existing.BIRTHDAY,
                    GENDER = existing.GENDER,
                    AVATAR_URL = publicPath,
                    SIGN_IMAGE_URL = existing.SIGN_IMAGE_URL,
                    NOTE = existing.NOTE
                });

                var result = await _repository.UpdateUserProfileAsync(companyCd, userId, payload);
                if (result <= 0)
                {
                    UserAvatarImageStorage.TryDeleteStoredPath(_environment, publicPath);
                    return ServerError("Update failed");
                }

                if (!string.Equals(previousPath, publicPath, StringComparison.OrdinalIgnoreCase))
                {
                    UserAvatarImageStorage.TryDeleteStoredPath(_environment, previousPath);
                }

                var updated = await _repository.GetUserProfileAsync(companyCd, userId);
                return Updated(MapUserProfileDto(updated ?? existing), "Uploaded successfully");
            }
            catch (InvalidOperationException ex)
            {
                return ValidationError(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload my avatar failed. UserId={UserId}", userId);
                return ServerError("Upload failed");
            }
        }

        [HttpGet("me/avatar-file")]
        [Authorize]
        public async Task<IActionResult> GetMyAvatarFile(CancellationToken cancellationToken)
        {
            var companyCd = Common.GetCompanyCode();
            var userId = Common.GetUserId();
            var existing = await _repository.GetUserProfileAsync(companyCd, userId);
            if (existing == null)
            {
                return NotFound("Profile not found");
            }

            var storedPath = Common.NormalizeNullableText(existing.AVATAR_URL);
            if (storedPath == null)
            {
                return NotFound("Avatar image not found");
            }

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
                _logger.LogError(ex, "Load my avatar failed. Path={Path}", storedPath);
                return ServerError("Cannot load avatar image");
            }
        }

        private static UserProfileDto MapUserProfileDto(UserProfile profile)
        {
            return new UserProfileDto
            {
                USER_PK_ID = profile.USER_PK_ID,
                USER_DETAIL_ID = profile.USER_DETAIL_ID,
                COMPANY_CD = profile.COMPANY_CD,
                USERID = profile.USERID,
                USERNM = profile.USERNM,
                USERLV = profile.USERLV,
                EMPLOYEE_CD = profile.EMPLOYEE_CD,
                EMPLOYEE_NM = profile.EMPLOYEE_NM,
                EMPLOYEE_NM_ENG = profile.EMPLOYEE_NM_ENG,
                DEPARTMENT_ID = profile.DEPARTMENT_ID,
                DEPARTMENT_CD = profile.DEPARTMENT_CD,
                POSITION_ID = profile.POSITION_ID,
                POSITION_CD = profile.POSITION_CD,
                BRANCH_ID = profile.BRANCH_ID,
                BRANCH_CD = profile.BRANCH_CD,
                EMAIL = profile.EMAIL,
                MOBILE_NO = profile.MOBILE_NO,
                TEL_NO = profile.TEL_NO,
                ADDRESS = profile.ADDRESS,
                BIRTHDAY = profile.BIRTHDAY,
                GENDER = profile.GENDER,
                AVATAR_URL = profile.AVATAR_URL,
                SIGN_IMAGE_URL = profile.SIGN_IMAGE_URL,
                NOTE = profile.NOTE,
                IS_ACTIVE = Common.NormalizeFlagString(profile.IS_ACTIVE, "1"),
                ISDEL = Common.NormalizeFlagString(profile.ISDEL, "0"),
            };
        }

        private static UserProfileUpdateRequest BuildUpdateRequest(UserProfileUpdateRequest request)
        {
            return new UserProfileUpdateRequest
            {
                USERNM = Common.NormalizeRequiredText(request.USERNM),
                EMPLOYEE_CD = Common.NormalizeNullableText(request.EMPLOYEE_CD),
                EMPLOYEE_NM = Common.NormalizeNullableText(request.EMPLOYEE_NM),
                EMPLOYEE_NM_ENG = Common.NormalizeNullableText(request.EMPLOYEE_NM_ENG),
                DEPARTMENT_ID = Common.NormalizeNullablePositiveLong(request.DEPARTMENT_ID),
                DEPARTMENT_CD = Common.NormalizeNullableText(request.DEPARTMENT_CD),
                POSITION_ID = Common.NormalizeNullablePositiveLong(request.POSITION_ID),
                POSITION_CD = Common.NormalizeNullableText(request.POSITION_CD),
                BRANCH_ID = Common.NormalizeNullablePositiveLong(request.BRANCH_ID),
                BRANCH_CD = Common.NormalizeNullableText(request.BRANCH_CD),
                EMAIL = Common.NormalizeNullableText(request.EMAIL),
                MOBILE_NO = Common.NormalizeNullableText(request.MOBILE_NO),
                TEL_NO = Common.NormalizeNullableText(request.TEL_NO),
                ADDRESS = Common.NormalizeNullableText(request.ADDRESS),
                BIRTHDAY = request.BIRTHDAY?.Date,
                GENDER = Common.NormalizeNullableText(request.GENDER),
                AVATAR_URL = Common.NormalizeNullableText(request.AVATAR_URL),
                SIGN_IMAGE_URL = Common.NormalizeNullableText(request.SIGN_IMAGE_URL),
                NOTE = Common.NormalizeNullableText(request.NOTE)
            };
        }

    }
}
