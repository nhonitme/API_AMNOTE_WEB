using API_AMNOTE_WEB.TaxWithholding;

namespace API_AMNOTE_WEB.TaxWithholding.Interfaces;

public interface IPitIncomePayerRepository
{
    Task<PitIncomePayer?> GetAsync(string companyCd);

    Task<PitIncomePayer> SaveAsync(
        string companyCd,
        string userId,
        PitIncomePayerSaveRequest request);
}
