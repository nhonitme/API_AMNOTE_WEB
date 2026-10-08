using API_AMNOTE_WEB.Helpers;
using API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace API_AMNOTE_WEB.Services.BackgroundJobs.ExcelImport.Handlers
{
    public static class ExcelImportHandlerHelper
    {
        private static readonly Dictionary<string, string> DefaultMessages = new(StringComparer.OrdinalIgnoreCase)
        {
            ["EXCEL_IMPORT_QUEUED"] = "Đang chờ xử lý.",
            ["EXCEL_IMPORT_PROCESSING"] = "Đang xử lý file Excel.",
            ["EXCEL_IMPORT_CANCELLED"] = "Import Excel đã bị hủy.",
            ["EXCEL_IMPORT_CANCEL_NOT_FOUND"] = "Không tìm thấy job import để hủy.",
            ["EXCEL_IMPORT_FILE_REQUIRED"] = "Vui lòng chọn file Excel cần import.",
            ["EXCEL_IMPORT_MODULE_REQUIRED"] = "Thiếu moduleCd.",
            ["EXCEL_IMPORT_INVALID_FILE_EXTENSION"] = "Chỉ cho phép import file Excel .xlsx hoặc .xls.",
            ["COMPANY_CD_REQUIRED"] = "Vui lòng chọn mã công ty.",
            ["EXCEL_IMPORT_FILE_NOT_FOUND"] = "Không tìm thấy file tạm để xử lý import.",
            ["EXCEL_IMPORT_NO_DATA_IN_FILE"] = "Không có dữ liệu trong file excel.",
            ["EXCEL_IMPORT_FILE_READ_PROGRESS"] = "Đang đọc file Excel...",
            ["EXCEL_IMPORT_FILE_READ_COMPLETE"] = "Đã đọc xong file Excel. Tổng số dòng: {0}.",
            ["EXCEL_IMPORT_VALIDATING_DATA"] = "Đang kiểm tra dữ liệu {0}/{1} dòng...",
            ["EXCEL_IMPORT_FILE_HAS_ERRORS"] = "File có {0} dòng lỗi. Không thực hiện ghi DB.",
            ["EXCEL_IMPORT_JOB_COMPLETE"] = "Import Excel hoàn tất.",
            ["EXCEL_IMPORT_FAILED"] = "Import thất bại.",
            ["EXCEL_IMPORT_PROGRESS_NOT_FOUND"] = "Không tìm thấy tiến trình import.",
            ["EXCEL_IMPORT_TRANSFER"] = "Đang chuyển dữ liệu Excel",
            ["EXCEL_IMPORT_START_INSERT"] = "Bắt đầu ghi dữ liệu vào database...",
            ["EXCEL_IMPORT_COMPLETE"] = "Đã import thành công {0}/{1} dòng.",
            ["EXCEL_IMPORT_TRANSFER_SUFFIX"] = "dòng...",
            ["REQUIRED"] = "không được bỏ trống",
            ["OR"] = "hoặc",
            ["IMPORT_DUPLICATE_IN_FILE"] = "{0} trùng trong tệp import: {1}",
            ["IMPORT_UNSUPPORTED_MODULE"] = "Không hỗ trợ module import: {0}",
            ["INPUTS_REQUIRED"] = "INPUTS là bắt buộc.",
            ["OUTPUTS_REQUIRED"] = "OUTPUTS là bắt buộc.",
            ["DETAILS_REQUIRED"] = "DETAILS là bắt buộc.",
            ["DEBIT_CREDIT_EXCLUSIVE"] = "Không được nhập đồng thời cả Nợ và Có",
            ["AMOUNT_NOT_NEGATIVE"] = "Số tiền không được âm",
            ["FC_AMOUNT_NOT_NEGATIVE"] = "Số tiền ngoại tệ không được âm",
            ["EXCHANGE_RATE_NOT_NEGATIVE"] = "Tỷ giá không được âm",
            ["ACC_CD_INVALID_TYPE"] = "Mã tài khoản không hợp lệ cho số dư tài khoản (ISABLETYPE phải = 1)",
            ["ACC_CD_EXCLUDED_PREFIX"] = "Không được import tài khoản có mã bắt đầu bằng 5, 6, 7, 8, 9",
            ["FC_TYPE_REQUIRED"] = "Loại tiền không được để trống",
        };

        public static string GetRequestLanguage(ExcelImportJobRequest request)
        {
            return Common.NormalizeLanguageCode(request.Lang ?? Common.GetCurrentLanguage());
        }

        public static async Task<string> GetLocalizedMessageAsync(string key, string lang = "VIET")
        {
            var message = await Common.getLanguage(key, lang);
            if (string.IsNullOrWhiteSpace(message) || string.Equals(message, key, System.StringComparison.OrdinalIgnoreCase))
            {
                if (DefaultMessages.TryGetValue(key, out var fallback))
                {
                    return fallback;
                }
            }
            return message;
        }

        public static string GetLocalizedMessage(string key, string lang = "VIET")
        {
            return GetLocalizedMessageAsync(key, lang).GetAwaiter().GetResult();
        }

        public static string BuildRowAlreadyExistsMessage(int rowNo, string fieldKey, string fieldValue, string lang = "VIET")
        {
            var fieldLabel = Common.getLanguage(fieldKey, lang).GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(fieldLabel) || string.Equals(fieldLabel, fieldKey, System.StringComparison.OrdinalIgnoreCase))
            {
                fieldLabel = fieldKey;
            }

            var alreadyExistsText = Common.getLanguage("ALREADY_EXISTS", lang).GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(alreadyExistsText) || string.Equals(alreadyExistsText, "ALREADY_EXISTS", System.StringComparison.OrdinalIgnoreCase))
            {
                alreadyExistsText = "đã tồn tại";
            }

            return $"{Common.getLanguageV2("ROW", lang)} {rowNo}: {fieldLabel} {fieldValue} {alreadyExistsText}";
        }

        /// <summary>
        /// Key lưu số dòng Excel thật (1-based) khi đọc file import.
        /// </summary>
        public const string ExcelRowNoKey = ExcelHelper.ExcelImportRowNoKey;

        /// <summary>
        /// Lấy số dòng theo vị trí trên file Excel. Fallback: index + 2 (header ở dòng 1).
        /// </summary>
        public static int GetExcelRowNo(IReadOnlyDictionary<string, object>? row, int index)
        {
            if (row != null)
            {
                if (row.TryGetValue(ExcelRowNoKey, out var value) && value != null)
                {
                    if (value is int intValue && intValue > 0)
                    {
                        return intValue;
                    }

                    if (int.TryParse(value.ToString(), out var parsed) && parsed > 0)
                    {
                        return parsed;
                    }
                }
            }

            return index + 2;
        }

        public static string BuildDuplicateFieldExceptionMessage(string fieldKey, IEnumerable<string> duplicateValues, string lang = "VIET")
        {
            var fieldLabel = Common.getLanguage(fieldKey, lang).GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(fieldLabel) || string.Equals(fieldLabel, fieldKey, System.StringComparison.OrdinalIgnoreCase))
            {
                fieldLabel = fieldKey;
            }

            var template = GetLocalizedMessage("IMPORT_DUPLICATE_IN_FILE", lang);
            return string.Format(template, fieldLabel, string.Join(", ", duplicateValues));
        }

        public static string GetUnsupportedModuleMessage(string moduleCd, string lang = "VIET")
        {
            var template = GetLocalizedMessage("IMPORT_UNSUPPORTED_MODULE", lang);
            return string.Format(template, moduleCd);
        }

        public static string GetImportFailedMessage(string lang = "VIET")
        {
            return GetLocalizedMessage("IMPORT_FAILED", lang);
        }

        public static string GetInputsRequiredMessage(string lang = "VIET")
        {
            return GetLocalizedMessage("INPUTS_REQUIRED", lang);
        }

        public static string GetOutputsRequiredMessage(string lang = "VIET")
        {
            return GetLocalizedMessage("OUTPUTS_REQUIRED", lang);
        }

        public static string GetDetailsRequiredMessage(string lang = "VIET")
        {
            return GetLocalizedMessage("DETAILS_REQUIRED", lang);
        }

        public static ExcelImportResultRowDto BuildError(int rowNo, string message)
        {
            return new ExcelImportResultRowDto
            {
                RowNo = rowNo,
                Status = "ERROR",
                Message = message
            };
        }

        public static ExcelImportResultRowDto BuildRequiredFieldError(int rowNo, string fieldKey, string lang = "VIET")
        {
            var fieldLabel = Common.getLanguage(fieldKey, lang).GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(fieldLabel) || string.Equals(fieldLabel, fieldKey, System.StringComparison.OrdinalIgnoreCase))
            {
                fieldLabel = fieldKey;
            }

            var requiredMessage = Common.getLanguage("REQUIRED", lang).GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(requiredMessage) || string.Equals(requiredMessage, "REQUIRED", System.StringComparison.OrdinalIgnoreCase))
            {
                requiredMessage = DefaultMessages["REQUIRED"];
            }

            return new ExcelImportResultRowDto
            {
                RowNo = rowNo,
                Status = "ERROR",
                Message = $"{fieldLabel} {requiredMessage}"
            };
        }

        public static ExcelImportResultRowDto BuildReferenceNotFoundError(int rowNo, string fieldKey, string? fieldValue, string lang = "VIET")
        {
            var normalizedValue = Common.NormalizeNullableText(fieldValue) ?? string.Empty;
            var column_mm = Common.getLanguageV2(fieldKey, lang);
            return new ExcelImportResultRowDto
            {
                RowNo = rowNo,
                Status = "ERROR",
                Message = $"Cột {column_mm}, giá trị '{normalizedValue}' không tồn tại trong danh mục."
            };
        }

        public static ExcelImportResultRowDto BuildEitherRequiredFieldError(int rowNo, string firstFieldKey, string secondFieldKey, string lang = "VIET")
        {
            var firstLabel = Common.getLanguage(firstFieldKey, lang).GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(firstLabel) || string.Equals(firstLabel, firstFieldKey, System.StringComparison.OrdinalIgnoreCase))
            {
                firstLabel = firstFieldKey;
            }

            var secondLabel = Common.getLanguage(secondFieldKey, lang).GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(secondLabel) || string.Equals(secondLabel, secondFieldKey, System.StringComparison.OrdinalIgnoreCase))
            {
                secondLabel = secondFieldKey;
            }

            var requiredMessage = Common.getLanguage("REQUIRED", lang).GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(requiredMessage) || string.Equals(requiredMessage, "REQUIRED", System.StringComparison.OrdinalIgnoreCase))
            {
                requiredMessage = DefaultMessages["REQUIRED"];
            }

            var orLabel = Common.getLanguage("OR", lang).GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(orLabel) || string.Equals(orLabel, "OR", System.StringComparison.OrdinalIgnoreCase))
            {
                orLabel = DefaultMessages["OR"];
            }

            return new ExcelImportResultRowDto
            {
                RowNo = rowNo,
                Status = "ERROR",
                Message = $"{firstLabel} {orLabel} {secondLabel} {requiredMessage}"
            };
        }

        public static async Task<string> GetProgressTransferTextAsync(int current, int total, string lang = "VIET")
        {
            var template = await GetLocalizedMessageAsync("EXCEL_IMPORT_TRANSFER", lang);
            var suffix = await GetLocalizedMessageAsync("EXCEL_IMPORT_TRANSFER_SUFFIX", lang);
            return $"{template} {current}/{total} {suffix}";
        }

        public static async Task<string> GetStartInsertTextAsync(string lang = "VIET")
        {
            return await GetLocalizedMessageAsync("EXCEL_IMPORT_START_INSERT", lang);
        }

        public static async Task<string> GetCompleteTextAsync(int inserted, int total, string lang = "VIET")
        {
            var template = await GetLocalizedMessageAsync("EXCEL_IMPORT_COMPLETE", lang);
            return string.Format(template, inserted, total);
        }
    }
}
