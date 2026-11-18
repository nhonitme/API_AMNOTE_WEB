using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface ILicenseAiService
    {
        Task<AiLicenseStatusDto> GetStatusAsync(string companyCd);
        Task<(bool Ok, string Message, AiLicenseStatusDto Status)> CheckAsync(string companyCd, bool requireInitData);
        Task MarkInitDataAsync(string companyCd);
    }
}
