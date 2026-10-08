using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Interfaces;
using API_AMNOTE_WEB.Models;
using API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers;

public sealed class EInvoiceInfoImportHandler : IExcelImportModuleHandler, IExcelImportCustomProcessorHandler
{
    public string ModuleCd => "EInvoiceInfo";

    // Custom processor: template IS_REQUIRED is not applied. Business rules live in
    // EInvoiceExcelImportHelper parse + EInvoiceService.CreateAsync (NormalizeAndValidate).

    private readonly IEInvoiceService _service;
    private readonly IEInvoiceSellerRepository _sellerRepository;

    public EInvoiceInfoImportHandler(IEInvoiceService service, IEInvoiceSellerRepository sellerRepository)
    {
        _service = service;
        _sellerRepository = sellerRepository;
    }

    public Task SaveAsync(
        List<Dictionary<string, object>> rows,
        ExcelImportJobRequest request,
        IExcelImportJobProgressWriter progressWriter,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("EInvoiceInfo import must use the custom Excel processor.");
    }

    public async Task<ExcelImportResultDto> ProcessImportAsync(
        ExcelImportJobRequest request,
        IExcelImportJobProgressWriter progressWriter,
        CancellationToken cancellationToken = default)
    {
        var lang = request.Lang ?? Common.GetCurrentLanguage();

        if (string.IsNullOrWhiteSpace(request.TempFilePath) || !File.Exists(request.TempFilePath))
        {
            return BuildError(ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_FILE_NOT_FOUND", lang));
        }

        await progressWriter.ReportPercentAsync(
            10,
            ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_FILE_READ_PROGRESS", lang),
            cancellationToken);

        var sellers = (await _sellerRepository.GetSellersAsync(
            request.CompanyCd,
            includeInactive: false,
            includeAllTemplates: true)).ToList();
        if (sellers.Count == 0)
        {
            return BuildError("E-invoice seller is not configured");
        }

        await using var stream = File.OpenRead(request.TempFilePath);
        var parseResult = await EInvoiceExcelImportHelper.ParseInvoicesAsync(
            stream,
            request.CompanyCd,
            sellers,
            cancellationToken);

        if (parseResult.Errors.Count > 0 && parseResult.Invoices.Count == 0)
        {
            return new ExcelImportResultDto
            {
                Success = false,
                TotalRows = parseResult.Errors.Count,
                SuccessRows = 0,
                WarningRows = 0,
                ErrorRows = parseResult.Errors.Count,
                Message = parseResult.Errors[0].Message,
                Rows = parseResult.Errors
            };
        }

        var invoices = parseResult.Invoices;
        if (invoices.Count == 0)
        {
            return new ExcelImportResultDto
            {
                Success = false,
                TotalRows = 0,
                SuccessRows = 0,
                WarningRows = parseResult.SkippedInvoices,
                ErrorRows = 1,
                Message = ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_NO_DATA_IN_FILE", lang),
                Rows = parseResult.Errors
            };
        }

        await progressWriter.ReportAsync(
            0,
            invoices.Count,
            30,
            string.Format(
                ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_FILE_READ_COMPLETE", lang),
                invoices.Count),
            cancellationToken);

        var userId = request.UserId ?? string.Empty;
        var errorRows = new List<ExcelImportResultRowDto>(parseResult.Errors);
        var successCount = 0;
        var failedCount = 0;

        for (var index = 0; index < invoices.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await _service.CreateAsync(request.CompanyCd, userId, invoices[index]);
                successCount++;
            }
            catch (Exception ex)
            {
                failedCount++;
                errorRows.Add(new ExcelImportResultRowDto
                {
                    RowNo = ExcelImportHandlerHelper.GetExcelRowNo(null, index),
                    Status = "ERROR",
                    Message = ex.Message
                });
            }

            var percent = 30 + (int)Math.Round((index + 1) * 70m / invoices.Count);
            await progressWriter.ReportAsync(
                index + 1,
                invoices.Count,
                percent,
                await ExcelImportHandlerHelper.GetProgressTransferTextAsync(index + 1, invoices.Count, lang),
                cancellationToken);
        }

        var success = failedCount == 0;
        var message = success
            ? string.Format(ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_COMPLETE", lang), successCount, invoices.Count)
            : $"Failed {failedCount} e-invoice(s). {errorRows.LastOrDefault(x => x.Status == "ERROR")?.Message}";

        if (parseResult.SkippedInvoices > 0 && success)
        {
            message = $"{message} Skipped {parseResult.SkippedInvoices} invalid e-invoice(s).";
        }

        await progressWriter.ReportAsync(
            invoices.Count,
            invoices.Count,
            100,
            ExcelImportHandlerHelper.GetLocalizedMessage("EXCEL_IMPORT_JOB_COMPLETE", lang),
            cancellationToken);

        return new ExcelImportResultDto
        {
            Success = success && successCount > 0,
            TotalRows = invoices.Count,
            SuccessRows = successCount,
            WarningRows = parseResult.SkippedInvoices,
            ErrorRows = failedCount + parseResult.Errors.Count,
            Message = message,
            Rows = errorRows
        };
    }

    private static ExcelImportResultDto BuildError(string message)
    {
        return new ExcelImportResultDto
        {
            Success = false,
            TotalRows = 0,
            SuccessRows = 0,
            WarningRows = 0,
            ErrorRows = 1,
            Message = message,
            Rows =
            [
                new ExcelImportResultRowDto
                {
                    RowNo = 0,
                    Status = "ERROR",
                    Message = message
                }
            ]
        };
    }
}
