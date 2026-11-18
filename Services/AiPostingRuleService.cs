using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models.DTOs;
using Microsoft.Extensions.Options;

namespace API_AMNOTE_WEB.Services
{
    public class AiPostingRuleService : IAiPostingRuleService
    {
        private readonly IAiPostingRuleRepository _repository;
        private readonly ILicenseAiService _licenseAiService;
        private readonly AiExternalClient _aiClient;
        private readonly AmnoteAiOptions _options;
        private readonly ILogger<AiPostingRuleService> _logger;

        public AiPostingRuleService(
            IAiPostingRuleRepository repository,
            ILicenseAiService licenseAiService,
            AiExternalClient aiClient,
            IOptions<AmnoteAiOptions> options,
            ILogger<AiPostingRuleService> logger)
        {
            _repository = repository;
            _licenseAiService = licenseAiService;
            _aiClient = aiClient;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<IReadOnlyList<AiPostingRuleListItemDto>> GetListAsync(string companyCd)
        {
            await EnsureCanUseAiAsync(companyCd);
            return await _repository.GetListAsync(companyCd);
        }

        public async Task<AiPostingRuleDetailDto?> GetDetailAsync(string companyCd, long ruleId)
        {
            await EnsureCanUseAiAsync(companyCd);
            return await _repository.GetDetailAsync(companyCd, ruleId);
        }

        public async Task<long> SaveAsync(string companyCd, string userId, AiPostingRuleDetailDto rule)
        {
            await EnsureCanUseAiAsync(companyCd);
            ValidateRule(rule);

            if (await _repository.ExistsRuleCodeAsync(companyCd, rule.RuleCd.Trim(), rule.RuleId))
            {
                throw new InvalidOperationException("Mã quy tắc đã tồn tại.");
            }

            var ruleId = await _repository.SaveAsync(companyCd, userId, rule);
            await TrySyncRuleAsync(companyCd, ruleId, isDelete: false);
            return ruleId;
        }

        public async Task DeleteAsync(string companyCd, string userId, long ruleId)
        {
            await EnsureCanUseAiAsync(companyCd);
            await _repository.SoftDeleteAsync(companyCd, userId, ruleId);
            await TrySyncRuleAsync(companyCd, ruleId, isDelete: true);
        }

        private async Task EnsureCanUseAiAsync(string companyCd)
        {
            var (ok, message, _) = await _licenseAiService.CheckAsync(companyCd, requireInitData: true);
            if (!ok)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void ValidateRule(AiPostingRuleDetailDto rule)
        {
            if (rule == null)
            {
                throw new InvalidOperationException("Không có dữ liệu quy tắc.");
            }

            if (string.IsNullOrWhiteSpace(rule.RuleCd))
            {
                throw new InvalidOperationException("Mã quy tắc bắt buộc.");
            }

            if (string.IsNullOrWhiteSpace(rule.RuleNm))
            {
                throw new InvalidOperationException("Tên quy tắc bắt buộc.");
            }

            if ((rule.Conditions?.Count ?? 0) == 0)
            {
                throw new InvalidOperationException("Cần ít nhất một điều kiện.");
            }

            if ((rule.Actions?.Count ?? 0) == 0)
            {
                throw new InvalidOperationException("Cần ít nhất một action TK Nợ/Có.");
            }
        }

        private async Task TrySyncRuleAsync(string companyCd, long ruleId, bool isDelete)
        {
            try
            {
                var url = isDelete
                    ? _options.ResolveXoaRuleMoiCtyUrl()
                    : _options.ResolveThemRuleMoiCtyUrl();

                var body = new
                {
                    client_id = companyCd,
                    rule_id = ruleId
                };

                var (ok, _, error) = await _aiClient.PostJsonAsync(url, body, _options.ApiKey);
                if (!ok)
                {
                    _logger.LogWarning(
                        "AI rule sync failed. Company={CompanyCd}, RuleId={RuleId}, Delete={IsDelete}, Error={Error}",
                        companyCd,
                        ruleId,
                        isDelete,
                        error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "AI rule sync exception. Company={CompanyCd}, RuleId={RuleId}, Delete={IsDelete}",
                    companyCd,
                    ruleId,
                    isDelete);
            }
        }
    }
}
