using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Interfaces
{
    public interface IAiPostingRuleService
    {
        Task<IReadOnlyList<AiPostingRuleListItemDto>> GetListAsync(string companyCd);
        Task<AiPostingRuleDetailDto?> GetDetailAsync(string companyCd, long ruleId);
        Task<long> SaveAsync(string companyCd, string userId, AiPostingRuleDetailDto rule);
        Task DeleteAsync(string companyCd, string userId, long ruleId);
    }
}
