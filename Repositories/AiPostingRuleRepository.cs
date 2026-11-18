using API_AMNOTE_WEB.Data;
using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Repositories
{
    public class AiPostingRuleRepository : IAiPostingRuleRepository
    {
        private readonly DapperExecutor _db;

        public AiPostingRuleRepository(DapperExecutor db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<IReadOnlyList<AiPostingRuleListItemDto>> GetListAsync(string companyCd)
        {
            const string query = "CALL getai_posting_rule_list(@p_COMPANY_CD)";
            var rows = await _db.QueryAsync<AiPostingRuleListItemDto>(
                Net_DB.Net_DB_Company,
                query,
                new { p_COMPANY_CD = companyCd });
            return rows.ToList();
        }

        public async Task<AiPostingRuleDetailDto?> GetDetailAsync(string companyCd, long ruleId)
        {
            const string query = "CALL getai_posting_rule_detail(@p_COMPANY_CD, @p_RULE_ID)";
            var (headers, conditions, actions) = await _db.QueryMultipleAsync<
                AiPostingRuleDetailDto,
                AiPostingRuleConditionDto,
                AiPostingRuleActionDto>(
                Net_DB.Net_DB_Company,
                query,
                new { p_COMPANY_CD = companyCd, p_RULE_ID = ruleId });

            var rule = headers.FirstOrDefault();
            if (rule == null)
            {
                return null;
            }

            rule.Conditions = conditions.ToList();
            rule.Actions = actions.ToList();
            return rule;
        }

        public async Task<bool> ExistsRuleCodeAsync(string companyCd, string ruleCd, long exceptRuleId)
        {
            const string query = "CALL existsai_posting_rule_cd(@p_COMPANY_CD, @p_RULE_CD, @p_EXCEPT_ID)";
            var count = await _db.QueryFirstOrDefaultAsync<long>(
                Net_DB.Net_DB_Company,
                query,
                new
                {
                    p_COMPANY_CD = companyCd,
                    p_RULE_CD = ruleCd,
                    p_EXCEPT_ID = exceptRuleId
                });
            return count > 0;
        }

        public async Task<long> SaveAsync(string companyCd, string userId, AiPostingRuleDetailDto rule)
        {
            await using var session = await _db.CreateSessionAsync(Net_DB.Net_DB_Company);
            try
            {
                const string setRuleQuery = """
                    CALL setai_posting_rule(
                        @p_COMPANY_CD, @p_RULE_ID, @p_RULE_CD, @p_RULE_NM, @p_RULE_SCOPE, @p_RULE_LEVEL,
                        @p_BUSINESS_TYPE, @p_PRIORITY, @p_CONFIDENCE_MIN, @p_PROCESS_INVENTORY_FLAG,
                        @p_NEED_ASSET_PROFILE_FLAG, @p_NOTE, @p_USER_ID)
                    """;

                var ruleId = await session.QuerySingleAsync<long>(setRuleQuery, new
                {
                    p_COMPANY_CD = companyCd,
                    p_RULE_ID = rule.RuleId,
                    p_RULE_CD = rule.RuleCd,
                    p_RULE_NM = rule.RuleNm,
                    p_RULE_SCOPE = rule.RuleScope,
                    p_RULE_LEVEL = rule.RuleLevel,
                    p_BUSINESS_TYPE = rule.BusinessType,
                    p_PRIORITY = rule.Priority,
                    p_CONFIDENCE_MIN = rule.ConfidenceMin,
                    p_PROCESS_INVENTORY_FLAG = NormalizeFlag(rule.ProcessInventoryFlag),
                    p_NEED_ASSET_PROFILE_FLAG = NormalizeFlag(rule.NeedAssetProfileFlag),
                    p_NOTE = rule.Note,
                    p_USER_ID = userId
                });

                var sort = 0;
                foreach (var condition in rule.Conditions ?? [])
                {
                    sort++;
                    _ = await session.QuerySingleAsync<long>(
                        """
                        CALL setai_posting_rule_condition(
                            @p_COMPANY_CD, @p_RULE_ID, @p_FIELD_NM, @p_OPERATOR, @p_VALUE1, @p_VALUE2,
                            @p_MATCH_CASE_YN, @p_REQUIRED_YN, @p_SCORE, @p_SORT, @p_DESCRIPTION)
                        """,
                        new
                        {
                            p_COMPANY_CD = companyCd,
                            p_RULE_ID = ruleId,
                            p_FIELD_NM = condition.FieldNm,
                            p_OPERATOR = condition.Operator,
                            p_VALUE1 = condition.Value1,
                            p_VALUE2 = condition.Value2,
                            p_MATCH_CASE_YN = NormalizeFlag(condition.MatchCaseYn),
                            p_REQUIRED_YN = NormalizeFlag(condition.RequiredYn),
                            p_SCORE = condition.Score,
                            p_SORT = condition.Sort > 0 ? condition.Sort : sort,
                            p_DESCRIPTION = condition.Description
                        });
                }

                sort = 0;
                foreach (var action in rule.Actions ?? [])
                {
                    sort++;
                    _ = await session.QuerySingleAsync<long>(
                        """
                        CALL setai_posting_rule_action(
                            @p_COMPANY_CD, @p_RULE_ID, @p_ACTION_TYPE, @p_DEBIT_ACC_CD, @p_CREDIT_ACC_CD,
                            @p_DESCRIPTION, @p_SORT)
                        """,
                        new
                        {
                            p_COMPANY_CD = companyCd,
                            p_RULE_ID = ruleId,
                            p_ACTION_TYPE = action.ActionType,
                            p_DEBIT_ACC_CD = action.DebitAccCd,
                            p_CREDIT_ACC_CD = action.CreditAccCd,
                            p_DESCRIPTION = action.Description,
                            p_SORT = action.Sort > 0 ? action.Sort : sort
                        });
                }

                session.Commit();
                return ruleId;
            }
            catch
            {
                session.Rollback();
                throw;
            }
        }

        public async Task SoftDeleteAsync(string companyCd, string userId, long ruleId)
        {
            const string query = "CALL delai_posting_rule(@p_COMPANY_CD, @p_RULE_ID, @p_USER_ID)";
            _ = await _db.QueryFirstOrDefaultAsync<int>(
                Net_DB.Net_DB_Company,
                query,
                new { p_COMPANY_CD = companyCd, p_RULE_ID = ruleId, p_USER_ID = userId });
        }

        private static string NormalizeFlag(string? value)
            => string.Equals(value?.Trim(), "1", StringComparison.OrdinalIgnoreCase) ? "1" : "0";
    }
}
