using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Services
{
    public class LicenseAiService : ILicenseAiService
    {
        private readonly ILicenseAiRepository _repository;

        public LicenseAiService(ILicenseAiRepository repository)
        {
            _repository = repository;
        }

        public async Task<AiLicenseStatusDto> GetStatusAsync(string companyCd)
        {
            var license = await _repository.GetLatestAsync(companyCd);
            if (license == null)
            {
                return new AiLicenseStatusDto
                {
                    HasLicense = false,
                    IsActive = false,
                    IsInited = false,
                    CanInit = false,
                    CanUseAi = false,
                    StopMsg = "Công ty chưa được cấp license AI.",
                    UserLimit = 0
                };
            }

            var today = DateTime.Today;
            var configuredStopMsg = (license.STOP_MSG ?? string.Empty).Trim();
            var isActiveFlag = license.IS_ACTIVE != 0;
            var isInited = license.IS_INIT_DATA == 1;
            string? validityError = null;

            if (!isActiveFlag)
            {
                validityError = string.IsNullOrEmpty(configuredStopMsg)
                    ? "License AI đang bị tạm khóa."
                    : configuredStopMsg;
            }
            else if (license.BEGIN_DATE.HasValue && today < license.BEGIN_DATE.Value.Date)
            {
                validityError = string.IsNullOrEmpty(configuredStopMsg)
                    ? $"License AI chưa có hiệu lực (bắt đầu từ ngày {license.BEGIN_DATE.Value:dd/MM/yyyy})."
                    : configuredStopMsg;
            }
            else if (license.END_DATE.HasValue && today > license.END_DATE.Value.Date)
            {
                validityError = string.IsNullOrEmpty(configuredStopMsg)
                    ? $"License AI đã hết hạn (ngày {license.END_DATE.Value:dd/MM/yyyy})."
                    : configuredStopMsg;
            }

            var licenseValid = validityError == null;
            return new AiLicenseStatusDto
            {
                HasLicense = true,
                IsActive = isActiveFlag,
                IsInited = isInited,
                CanInit = licenseValid && !isInited,
                CanUseAi = licenseValid && isInited,
                BeginDate = license.BEGIN_DATE,
                EndDate = license.END_DATE,
                StopMsg = validityError,
                UserLimit = license.USER_LIMIT
            };
        }

        public async Task<(bool Ok, string Message, AiLicenseStatusDto Status)> CheckAsync(string companyCd, bool requireInitData)
        {
            var status = await GetStatusAsync(companyCd);

            if (!status.HasLicense)
            {
                return (false, status.StopMsg ?? "Công ty chưa được cấp license AI.", status);
            }

            if (!string.IsNullOrWhiteSpace(status.StopMsg))
            {
                // Validity blocked (inactive / date)
                return (false, status.StopMsg, status);
            }

            if (requireInitData)
            {
                if (!status.IsInited)
                {
                    return (
                        false,
                        "Chưa khởi tạo dữ liệu AI. Vui lòng vào Thông tin công ty → Khởi tạo AI.",
                        status);
                }

                return (true, string.Empty, status);
            }

            if (status.IsInited)
            {
                return (false, "Hệ thống AI đã được khởi tạo rồi.", status);
            }

            return (true, string.Empty, status);
        }

        public Task MarkInitDataAsync(string companyCd)
            => _repository.MarkInitDataAsync(companyCd);
    }
}
