using System.Linq;
using API_AMNOTE_WEB.Models.DTOs;

namespace API_AMNOTE_WEB.Helpers
{
    /// <summary>
    /// Builds a human-readable formula/rule summary for template rows,
    /// including effective defaults used by report procedures when fields are empty.
    /// </summary>
    public static class ReportFormulaDisplayBuilder
    {
        private static readonly IReadOnlyDictionary<string, string> B03IndirectDefaultAccountRules =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["01"] = "+511,+515,-632,-635,-641,-642,-711,-811",
                ["02"] = "-214",
                ["03"] = "-229,-352",
                ["04"] = "-413",
                ["05"] = "+515,+711,+811",
                ["06"] = "+635",
                ["09"] = "-131,-136,-138,-141",
                ["10"] = "-151,-152,-153,-154,-155,-156,-157",
                ["11"] = "-331,-333,-334,-335,-336,-337,-338",
                ["12"] = "-242",
                ["13"] = "-121",
            };

        public static string Build(CashFlowFormulaOptionRowDto row, string reportCode)
        {
            var sourceType = (row.DATA_SOURCE_TYPE ?? string.Empty).Trim().ToUpperInvariant();
            var itemCode = (row.ITEM_CODE ?? string.Empty).Trim();

            if (string.Equals(reportCode, "GTGT_01", StringComparison.OrdinalIgnoreCase))
            {
                return BuildVatDeclarationDisplay(row);
            }

            if (sourceType == "FORMULA")
            {
                return NormalizeFormulaExpression(row.FORMULA_EXPR, itemCode);
            }

            if (sourceType == "MANUAL" || sourceType == "HEADER" || sourceType == "SECTION")
            {
                return string.Empty;
            }

            if (string.Equals(reportCode, "B03_DN_GT", StringComparison.OrdinalIgnoreCase))
            {
                return BuildIndirectAccountRuleDisplay(row, itemCode);
            }

            if (string.Equals(reportCode, "B03_DN_TT", StringComparison.OrdinalIgnoreCase))
            {
                return BuildDirectRuleDisplay(row);
            }

            if (string.Equals(reportCode, "B01_DN", StringComparison.OrdinalIgnoreCase))
            {
                return BuildBalanceSheetDisplay(row);
            }

            if (string.Equals(reportCode, "B02_DN", StringComparison.OrdinalIgnoreCase))
            {
                return BuildProfitLossDisplay(row);
            }

            if (!string.IsNullOrWhiteSpace(row.ACCOUNT_RULE))
            {
                return row.ACCOUNT_RULE.Trim();
            }

            if (!string.IsNullOrWhiteSpace(row.FORMULA_EXPR))
            {
                return NormalizeFormulaExpression(row.FORMULA_EXPR, itemCode);
            }

            return sourceType switch
            {
                "PROCEDURE" => "Lấy số từ sổ cái",
                "ACCOUNT_RULE" => string.Empty,
                _ => sourceType
            };
        }

        private static string BuildVatDeclarationDisplay(CashFlowFormulaOptionRowDto row)
        {
            var parts = new List<string>();
            AppendVatFormulaPart(parts, row.CODE_NO1, row.CALC_METHOD_NO1, row.FORMULA_NO1);
            AppendVatFormulaPart(parts, row.CODE_NO2, row.CALC_METHOD_NO2, row.FORMULA_NO2);
            return parts.Count > 0 ? string.Join(" · ", parts) : string.Empty;
        }

        private static void AppendVatFormulaPart(
            ICollection<string> parts,
            string? code,
            string? calcMethod,
            string? formulaExpr)
        {
            var method = (calcMethod ?? string.Empty).Trim().ToUpperInvariant();
            if (method is not ("FORMULA" or "FORMULA_POSITIVE" or "FORMULA_NEGATIVE"))
            {
                return;
            }

            var expression = NormalizeFormulaExpression(formulaExpr, code ?? string.Empty);
            if (string.IsNullOrWhiteSpace(expression))
            {
                return;
            }

            var suffix = method switch
            {
                "FORMULA_POSITIVE" => " (≥0)",
                "FORMULA_NEGATIVE" => " (≤0)",
                _ => string.Empty
            };
            parts.Add($"{expression}{suffix}");
        }

        private static string BuildIndirectAccountRuleDisplay(CashFlowFormulaOptionRowDto row, string itemCode)
        {
            var accountRule = (row.ACCOUNT_RULE ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(accountRule))
            {
                return FormatSignedAccountRuleForDisplay(accountRule);
            }

            if (B03IndirectDefaultAccountRules.TryGetValue(itemCode, out var defaultRule))
            {
                return $"{FormatSignedAccountRuleForDisplay(defaultRule)} (mẫu chuẩn)";
            }

            return "Chưa khai báo tài khoản";
        }

        private static string FormatSignedAccountRuleForDisplay(string accountRule)
        {
            var plus = new List<string>();
            var minus = new List<string>();
            var raw = accountRule.Trim();

            if (raw.StartsWith("DIRECT_RULE|", StringComparison.OrdinalIgnoreCase))
            {
                var parts = raw.Split('|');
                var flowSign = parts.Length > 1 && int.TryParse(parts[1], out var parsedSign) ? parsedSign : 1;
                var accounts = parts.Length > 2
                    ? parts[2].Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    : Array.Empty<string>();

                foreach (var account in accounts)
                {
                    var code = account.Trim().TrimStart('+', '-');
                    if (string.IsNullOrWhiteSpace(code))
                    {
                        continue;
                    }

                    if (flowSign == -1)
                    {
                        minus.Add(code);
                    }
                    else
                    {
                        plus.Add(code);
                    }
                }
            }
            else
            {
                foreach (var term in raw.Replace(';', ',').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (term.Contains('|', StringComparison.Ordinal) ||
                        term.Equals("DIRECT_RULE", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var isNegative = term.StartsWith('-');
                    var code = term.TrimStart('+', '-').Trim();
                    if (string.IsNullOrWhiteSpace(code) || !code.All(char.IsLetterOrDigit))
                    {
                        continue;
                    }

                    if (isNegative)
                    {
                        minus.Add(code);
                    }
                    else
                    {
                        plus.Add(code);
                    }
                }
            }

            var partsDisplay = new List<string>();
            if (plus.Count > 0)
            {
                partsDisplay.Add($"Cộng: {string.Join(", ", plus)}");
            }

            if (minus.Count > 0)
            {
                partsDisplay.Add($"Trừ: {string.Join(", ", minus)}");
            }

            return partsDisplay.Count > 0 ? string.Join(" · ", partsDisplay) : string.Empty;
        }

        private static string BuildBalanceSheetDisplay(CashFlowFormulaOptionRowDto row)
        {
            var sourceType = (row.DATA_SOURCE_TYPE ?? string.Empty).Trim().ToUpperInvariant();
            if (sourceType == "ACCOUNT_RULE" || sourceType == "PROCEDURE")
            {
                if (string.IsNullOrWhiteSpace(row.ACCOUNT_RULE))
                {
                    return "Lấy số từ sổ cái theo tài khoản";
                }

                var calcMethodLabel = FormatCalcMethodLabel(row.CALC_METHOD, isProfitLoss: false);
                return string.IsNullOrWhiteSpace(calcMethodLabel)
                    ? row.ACCOUNT_RULE.Trim()
                    : $"{row.ACCOUNT_RULE.Trim()} ({calcMethodLabel})";
            }

            return sourceType switch
            {
                "PROCEDURE" or "ACCOUNT_RULE" => "Lấy số từ sổ cái theo tài khoản",
                _ => sourceType
            };
        }

        private static string BuildProfitLossDisplay(CashFlowFormulaOptionRowDto row)
        {
            var sourceType = (row.DATA_SOURCE_TYPE ?? string.Empty).Trim().ToUpperInvariant();
            if (sourceType == "ACCOUNT_RULE" || sourceType == "PROCEDURE")
            {
                if (string.IsNullOrWhiteSpace(row.ACCOUNT_RULE))
                {
                    return "Lấy số từ sổ cái theo tài khoản";
                }

                var calcMethodLabel = FormatCalcMethodLabel(row.CALC_METHOD, isProfitLoss: true);
                return string.IsNullOrWhiteSpace(calcMethodLabel)
                    ? row.ACCOUNT_RULE.Trim()
                    : $"{row.ACCOUNT_RULE.Trim()} ({calcMethodLabel})";
            }

            return sourceType switch
            {
                "PROCEDURE" or "ACCOUNT_RULE" => "Lấy số từ sổ cái theo tài khoản",
                _ => sourceType
            };
        }

        private static string FormatCalcMethodLabel(string? calcMethod, bool isProfitLoss)
        {
            var normalized = (calcMethod ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            if (isProfitLoss)
            {
                return normalized switch
                {
                    "CREDIT_MINUS_DEBIT" => "Phát sinh Có − Nợ",
                    "DEBIT_MINUS_CREDIT" => "Phát sinh Nợ − Có",
                    "CREDIT_ONLY" => "Chỉ lấy phát sinh Có",
                    "DEBIT_ONLY" => "Chỉ lấy phát sinh Nợ",
                    _ => normalized
                };
            }

            return normalized switch
            {
                "DEBIT_MINUS_CREDIT" => "Dư Nợ (Nợ − Có)",
                "CREDIT_MINUS_DEBIT" => "Dư Có (Có − Nợ)",
                "DEBIT_ONLY" => "Chỉ lấy dư Nợ",
                "CREDIT_ONLY" => "Chỉ lấy dư Có",
                _ => normalized
            };
        }

        private static string BuildDirectRuleDisplay(CashFlowFormulaOptionRowDto row)
        {
            var parts = new List<string>();

            AppendDirectRulePart(parts, row.DIRECT_RULE_FLOW_SIGN, row.DIRECT_RULE_ACC_PREFIX, row.DIRECT_RULE_PRIORITY, 1);
            AppendDirectRulePart(parts, row.DIRECT_RULE_FLOW_SIGN_2, row.DIRECT_RULE_ACC_PREFIX_2, row.DIRECT_RULE_PRIORITY_2, 2);

            if (parts.Count > 0)
            {
                return string.Join(" | ", parts);
            }

            if (!string.IsNullOrWhiteSpace(row.FORMULA_EXPR))
            {
                return NormalizeFormulaExpression(row.FORMULA_EXPR, row.ITEM_CODE ?? string.Empty);
            }

            return "Chưa khai báo tài khoản đối ứng";
        }

        private static void AppendDirectRulePart(
            ICollection<string> parts,
            int? flowSign,
            string? accountPrefixes,
            int? priority,
            int ruleIndex)
        {
            var prefixes = (accountPrefixes ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(prefixes))
            {
                return;
            }

            var direction = flowSign == -1 ? "Chi tiền" : "Thu tiền";
            var priorityText = priority.HasValue ? $", ưu tiên {priority.Value}" : string.Empty;
            parts.Add($"Quy tắc {ruleIndex}: {direction} — TK đối ứng {prefixes}{priorityText}");
        }

        private static string NormalizeFormulaExpression(string? formulaExpr, string itemCode)
        {
            var formula = (formulaExpr ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(formula))
            {
                return string.IsNullOrWhiteSpace(itemCode) ? string.Empty : $"{itemCode} =";
            }

            if (formula.Contains('='))
            {
                return formula;
            }

            return string.IsNullOrWhiteSpace(itemCode) ? formula : $"{itemCode} = {formula}";
        }
    }
}
