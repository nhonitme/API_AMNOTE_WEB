using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IAiSystemService
    {
        Task<AiLicenseStatusDto> GetStatusAsync(string companyCd);
        Task<AiInitResultDto> InitAsync(string companyCd, CancellationToken cancellationToken = default);
        Task<AiSuggestAccountsResponse> SuggestAccountsAsync(
            string companyCd,
            AiSuggestAccountsRequest request,
            CancellationToken cancellationToken = default);

        Task<AiTranslateResponse> TranslateAsync(
            string companyCd,
            AiTranslateRequest request,
            CancellationToken cancellationToken = default);
    }
}
