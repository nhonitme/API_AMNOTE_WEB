using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport;
using API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers;

namespace API_AMNOTE_WEB.Services.OpeningBalance;

/// <summary>
/// Shared opening-balance amount rules for Excel import and form Save.
/// Required fields come from excel template IS_REQUIRED / FE RequiredRule — not here.
/// </summary>
public static class OpeningBalanceValidation
{
    public static IReadOnlyList<string> GetAmountErrorKeys(
        decimal debit,
        decimal credit,
        decimal debitFc,
        decimal creditFc,
        decimal exchangeRate)
    {
        var errors = new List<string>();
        if (debit > 0 && credit > 0)
            errors.Add("DEBIT_CREDIT_EXCLUSIVE");
        if (debit < 0 || credit < 0)
            errors.Add("AMOUNT_NOT_NEGATIVE");
        if (debitFc < 0 || creditFc < 0)
            errors.Add("FC_AMOUNT_NOT_NEGATIVE");
        if (exchangeRate < 0)
            errors.Add("EXCHANGE_RATE_NOT_NEGATIVE");
        return errors;
    }

    public static void AppendExcelAmountErrors(
        Dictionary<string, object> row,
        int rowNo,
        string lang,
        List<ExcelImportResultRowDto> validateResults)
    {
        foreach (var key in GetAmountErrorKeys(
                     Common.GetDecimal(row, "DEBIT"),
                     Common.GetDecimal(row, "CREDIT"),
                     Common.GetDecimal(row, "DEBIT_FC"),
                     Common.GetDecimal(row, "CREDIT_FC"),
                     Common.GetDecimal(row, "EXCHANGE_RATE")))
        {
            validateResults.Add(ExcelImportHandlerHelper.BuildError(
                rowNo,
                ExcelImportHandlerHelper.GetLocalizedMessage(key, lang)));
        }
    }

    public static void EnsureAmountsValid(
        decimal debit,
        decimal credit,
        decimal debitFc,
        decimal creditFc,
        decimal exchangeRate)
    {
        var keys = GetAmountErrorKeys(debit, credit, debitFc, creditFc, exchangeRate);
        if (keys.Count == 0)
            return;

        var lang = Common.NormalizeLanguageCode(Common.GetCurrentLanguage());
        throw new InvalidOperationException(
            ExcelImportHandlerHelper.GetLocalizedMessage(keys[0], lang));
    }

    public static void EnsureAmountsValid(BeforeState row)
        => EnsureAmountsValid(row.DEBIT, row.CREDIT, row.DEBIT_FC, row.CREDIT_FC, row.EXCHANGE_RATE);

    public static void EnsureAmountsValid(BeforeStateBank row)
        => EnsureAmountsValid(row.DEBIT, row.CREDIT, row.DEBIT_FC, row.CREDIT_FC, row.EXCHANGE_RATE);

    public static void EnsureAmountsValid(BeforeStateCustomer row)
        => EnsureAmountsValid(row.DEBIT, row.CREDIT, row.DEBIT_FC, row.CREDIT_FC, row.EXCHANGE_RATE);

    public static void EnsureAmountsValid(BeforeStateDepartment row)
        => EnsureAmountsValid(row.DEBIT, row.CREDIT, row.DEBIT_FC, row.CREDIT_FC, row.EXCHANGE_RATE);
}
