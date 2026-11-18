using API_AMNOTE_WEB.Models;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface ILicenseAiRepository
    {
        Task<LicenseAiInfo?> GetLatestAsync(string companyCd);
        Task<int> MarkInitDataAsync(string companyCd);
    }
}
