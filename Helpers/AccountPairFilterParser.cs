namespace API_AMNOTE_WEB.Helpers
{
    /// <summary>
    /// Token cặp TK Nợ/Có từ user_setting (vd MATERIAL_ISSUE_ACCOUNT_PAIRS).
    /// DebitPrefix null = không lọc Nợ; CreditPrefix null = không lọc Có.
    /// </summary>
    public sealed class AccountPairToken
    {
        public string? DebitPrefix { get; init; }
        public string? CreditPrefix { get; init; }
    }

    public static class AccountPairFilterParser
    {
        public const string MaterialIssueFilterCode = "MATERIAL_ISSUE";
        public const string FinishGoodsInFilterCode = "FINISH_GOODS_IN";

        /// <summary>
        /// Parse VALUE dạng "621/152,624/,/156". Token không có '/' hoặc cả hai phía trống → bỏ qua.
        /// </summary>
        public static IReadOnlyList<AccountPairToken> Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<AccountPairToken>();
            }

            var results = new List<AccountPairToken>();

            foreach (var rawPart in value.Split(','))
            {
                var part = rawPart.Trim();
                if (part.Length == 0)
                {
                    continue;
                }

                var slashIndex = part.IndexOf('/');
                if (slashIndex < 0)
                {
                    continue;
                }

                var debit = part[..slashIndex].Trim();
                var credit = part[(slashIndex + 1)..].Trim();

                if (debit.Length == 0 && credit.Length == 0)
                {
                    continue;
                }

                results.Add(new AccountPairToken
                {
                    DebitPrefix = debit.Length > 0 ? debit : null,
                    CreditPrefix = credit.Length > 0 ? credit : null,
                });
            }

            return results;
        }
    }
}
