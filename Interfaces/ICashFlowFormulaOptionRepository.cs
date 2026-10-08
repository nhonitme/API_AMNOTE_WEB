using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface ICashFlowFormulaOptionRepository
    {
        Task<CashFlowFormulaOptionsDto> GetAsync(
            string companyCd,
            string? reportCode,
            string? reportVersion,
            CancellationToken cancellationToken = default);

        Task<CashFlowFormulaOptionsDto> SaveAsync(
            string companyCd,
            SaveCashFlowFormulaOptionsRequest request,
            string userId,
            CancellationToken cancellationToken = default);

        Task<CashFlowFormulaOptionsDto> ResetToDefaultAsync(
            string companyCd,
            string? reportCode,
            string? reportVersion,
            string userId,
            CancellationToken cancellationToken = default);

        Task<FormulaOptionPreviewDto> PreviewDraftAsync(
            string companyCd,
            PreviewCashFlowFormulaOptionsRequest request,
            string userId,
            IConfiguredReportService configuredReportService,
            CancellationToken cancellationToken = default);
    }
}
