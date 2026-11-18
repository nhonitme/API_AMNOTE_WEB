using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IAiPostingRuleRepository
    {
        Task<IReadOnlyList<AiPostingRuleListItemDto>> GetListAsync(string companyCd);
        Task<AiPostingRuleDetailDto?> GetDetailAsync(string companyCd, long ruleId);
        Task<long> SaveAsync(string companyCd, string userId, AiPostingRuleDetailDto rule);
        Task SoftDeleteAsync(string companyCd, string userId, long ruleId);
        Task<bool> ExistsRuleCodeAsync(string companyCd, string ruleCd, long exceptRuleId);
    }
}
